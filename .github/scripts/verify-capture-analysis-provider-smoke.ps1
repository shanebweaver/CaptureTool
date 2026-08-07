param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,

    [Parameter(Mandatory = $true)]
    [string]$AppBinRoot,

    [Parameter(Mandatory = $true)]
    [string]$RunManifest
)

$ErrorActionPreference = 'Stop'

$resolvedPackageRoot = (Resolve-Path -LiteralPath $PackageRoot).Path
$resolvedAppBinRoot = (Resolve-Path -LiteralPath $AppBinRoot).Path
$resolvedRunManifest = (Resolve-Path -LiteralPath $RunManifest).Path
$temporaryParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$temporaryRoot = Join-Path $temporaryParent ("CaptureTool-Analysis-Smoke-" + [Guid]::NewGuid().ToString('N'))
$uploadRoot = Join-Path $temporaryRoot 'upload'
$bundleRoot = Join-Path $temporaryRoot 'bundle'

try {
    New-Item -ItemType Directory -Path $uploadRoot, $bundleRoot -Force | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $upload = Get-ChildItem -LiteralPath $resolvedPackageRoot -Recurse -File -Filter '*_bundle.msixupload' |
        Sort-Object Length -Descending |
        Select-Object -First 1
    if (-not $upload) {
        throw "No combined .msixupload was found under $resolvedPackageRoot."
    }

    [System.IO.Compression.ZipFile]::ExtractToDirectory($upload.FullName, $uploadRoot)
    $bundle = Get-ChildItem -LiteralPath $uploadRoot -Recurse -File -Filter '*.msixbundle' |
        Select-Object -First 1
    if (-not $bundle) {
        throw "$($upload.Name) does not contain an .msixbundle."
    }

    [System.IO.Compression.ZipFile]::ExtractToDirectory($bundle.FullName, $bundleRoot)
    $run = Get-Content -Raw -LiteralPath $resolvedRunManifest | ConvertFrom-Json
    if ($run.processingBoundary -ne 'on-device') {
        throw 'The release-gate run manifest is not local-only.'
    }

    $declaredSmoke = @{}
    foreach ($smoke in $run.packagedAotSmoke) {
        $declaredSmoke[$smoke.architecture.ToLowerInvariant()] = [bool]$smoke.passed
    }

    $expectedAnalyzerIds = @($run.analyzers |
        Where-Object { $_.providerId -eq $run.providerId } |
        ForEach-Object {
            if ($_.processingBoundary -ne 'on-device') {
                throw "Evaluated analyzer '$($_.analyzerId)' is not on-device."
            }
            $_.analyzerId
        } |
        Sort-Object)
    if ($expectedAnalyzerIds.Count -eq 0) {
        throw "The run manifest does not declare analyzers for provider '$($run.providerId)'."
    }
    foreach ($architecture in @('x64', 'arm64')) {
        if (-not $declaredSmoke.ContainsKey($architecture) -or -not $declaredSmoke[$architecture]) {
            throw "The run manifest does not declare a passing $architecture packaged AOT smoke."
        }

        $appPackage = Get-ChildItem -LiteralPath $bundleRoot -Recurse -File -Filter "*_${architecture}.msix" |
            Select-Object -First 1
        if (-not $appPackage) {
            throw "The Store bundle does not contain an $architecture app package."
        }

        $appRoot = Join-Path $temporaryRoot ("app-" + $architecture)
        New-Item -ItemType Directory -Path $appRoot -Force | Out-Null
        [System.IO.Compression.ZipFile]::ExtractToDirectory($appPackage.FullName, $appRoot)

        $appExecutable = Get-ChildItem -LiteralPath $appRoot -Recurse -File -Filter 'CaptureTool.Presentation.Windows.WinUI.exe' |
            Select-Object -First 1
        if (-not $appExecutable) {
            throw "$($appPackage.Name) does not contain the Native AOT application executable."
        }

        $providerManifestFile = Get-ChildItem -LiteralPath $appRoot -Recurse -File -Filter 'CaptureAnalysisProviders.json' |
            Select-Object -First 1
        if (-not $providerManifestFile) {
            throw "$($appPackage.Name) does not contain CaptureAnalysisProviders.json."
        }

        $providerManifest = Get-Content -Raw -LiteralPath $providerManifestFile.FullName | ConvertFrom-Json
        if ($providerManifest.schemaVersion -ne 1 -or $providerManifest.processingBoundary -ne 'on-device') {
            throw "$($appPackage.Name) contains an invalid or non-local provider manifest."
        }

        $provider = @($providerManifest.providers) |
            Where-Object { $_.providerId -eq $run.providerId } |
            Select-Object -First 1
        if (-not $provider) {
            throw "$($appPackage.Name) does not declare evaluated provider '$($run.providerId)'."
        }

        $actualAnalyzerIds = @($provider.analyzers | ForEach-Object { $_.analyzerId } | Sort-Object)
        if (($actualAnalyzerIds -join '|') -ne ($expectedAnalyzerIds -join '|')) {
            throw "$($appPackage.Name) does not contain the expected provider adapter set."
        }

        $platformDirectory = if ($architecture -eq 'arm64') { 'ARM64' } else { 'x64' }
        $nativePdb = Get-ChildItem -LiteralPath (Join-Path $resolvedAppBinRoot $platformDirectory) `
            -Recurse -File -Filter 'CaptureTool.Presentation.Windows.WinUI.pdb' |
            Where-Object { $_.FullName -like '*\native\CaptureTool.Presentation.Windows.WinUI.pdb' } |
            Select-Object -First 1
        if (-not $nativePdb) {
            throw "No $architecture Native AOT PDB was produced for the packaged application."
        }

        Write-Host "PASS $architecture packaged Native AOT provider smoke: $($run.providerId)"
    }
}
finally {
    $resolvedTemporaryRoot = [System.IO.Path]::GetFullPath($temporaryRoot)
    if ($resolvedTemporaryRoot.StartsWith($temporaryParent, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedTemporaryRoot).StartsWith('CaptureTool-Analysis-Smoke-', [StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
