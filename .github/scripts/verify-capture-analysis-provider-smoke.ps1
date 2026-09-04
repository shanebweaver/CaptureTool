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
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$projectAssetsPath = Join-Path $repoRoot 'src\CaptureTool.Presentation.Windows.WinUI\obj\project.assets.json'
$temporaryParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$temporaryRoot = Join-Path $temporaryParent ("CaptureTool-Analysis-Smoke-" + [Guid]::NewGuid().ToString('N'))
$uploadRoot = Join-Path $temporaryRoot 'upload'
$bundleRoot = Join-Path $temporaryRoot 'bundle'

function Assert-PackagedNativeAsset {
    param(
        [Parameter(Mandatory = $true)][string]$AppRoot,
        [Parameter(Mandatory = $true)][string]$FileName,
        [Parameter(Mandatory = $true)][string]$ExpectedPath,
        [Parameter(Mandatory = $true)][string]$PackageName
    )

    if (-not (Test-Path -LiteralPath $ExpectedPath -PathType Leaf)) {
        throw "Resolved package asset was not found: $ExpectedPath."
    }

    $packaged = @(Get-ChildItem -LiteralPath $AppRoot -Recurse -File -Filter $FileName)
    if ($packaged.Count -ne 1) {
        throw "Expected exactly one $FileName from $PackageName, found $($packaged.Count)."
    }

    $expectedHash = (Get-FileHash -LiteralPath $ExpectedPath -Algorithm SHA256).Hash
    $packagedHash = (Get-FileHash -LiteralPath $packaged[0].FullName -Algorithm SHA256).Hash
    if ($packagedHash -ne $expectedHash) {
        throw "$FileName does not match the runtime selected from $PackageName."
    }
}

try {
    New-Item -ItemType Directory -Path $uploadRoot, $bundleRoot -Force | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    if (-not (Test-Path -LiteralPath $projectAssetsPath -PathType Leaf)) {
        throw "The Store application project assets file was not found at $projectAssetsPath."
    }

    $projectAssets = Get-Content -Raw -LiteralPath $projectAssetsPath | ConvertFrom-Json
    $prereleaseWindowsAppSdkPackages = @($projectAssets.libraries.PSObject.Properties.Name |
        Where-Object {
            $_ -match '^Microsoft\.WindowsAppSDK(?:\.|/)' -and
            ($_ -split '/', 2)[1] -match '-'
        })
    if ($prereleaseWindowsAppSdkPackages.Count -gt 0) {
        throw "The Store build resolved prerelease Windows App SDK packages: $($prereleaseWindowsAppSdkPackages -join ', ')."
    }

    $resolvedFoundryPackages = @($projectAssets.libraries.PSObject.Properties.Name |
        Where-Object { $_ -match '^Microsoft\.AI\.Foundry\.Local(?:\.|/)' })
    $prereleaseFoundryPackages = @($resolvedFoundryPackages |
        Where-Object { ($_ -split '/', 2)[1] -match '-' })
    if ($prereleaseFoundryPackages.Count -gt 0) {
        throw "The Store build resolved prerelease Foundry Local packages: $($prereleaseFoundryPackages -join ', ')."
    }
    if (-not ($resolvedFoundryPackages | Where-Object {
        $_ -match '^Microsoft\.AI\.Foundry\.Local\.WinML/'
    })) {
        throw 'The Store build did not resolve the supported in-process Microsoft.AI.Foundry.Local.WinML SDK.'
    }

    $resolvedFoundryCorePackages = @($resolvedFoundryPackages |
        Where-Object { $_ -match '^Microsoft\.AI\.Foundry\.Local\.Core\.WinML/' })
    $resolvedWinMlPackages = @($projectAssets.libraries.PSObject.Properties.Name |
        Where-Object { $_ -match '^Microsoft\.Windows\.AI\.MachineLearning/' })
    $resolvedFoundryInferencePackages = @($projectAssets.libraries.PSObject.Properties.Name |
        Where-Object {
            $_ -match '^Microsoft\.ML\.OnnxRuntime(?:GenAI)?\.Foundry/'
        })
    $prereleaseFoundryRuntimePackages = @(
        $resolvedWinMlPackages + $resolvedFoundryInferencePackages |
            Where-Object { ($_ -split '/', 2)[1] -match '-' })
    if ($prereleaseFoundryRuntimePackages.Count -gt 0) {
        throw "The Store build resolved prerelease Foundry runtime packages: $($prereleaseFoundryRuntimePackages -join ', ')."
    }
    if ($resolvedFoundryCorePackages.Count -ne 1 -or $resolvedWinMlPackages.Count -ne 1) {
        throw 'The Store build must resolve exactly one Foundry Core WinML and one Windows AI MachineLearning package.'
    }

    $nugetPackageRoot = @($projectAssets.packageFolders.PSObject.Properties.Name) |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($nugetPackageRoot)) {
        throw 'The Store project assets file does not declare a NuGet package root.'
    }
    $foundryCoreVersion = ($resolvedFoundryCorePackages[0] -split '/', 2)[1]
    $winMlVersion = ($resolvedWinMlPackages[0] -split '/', 2)[1]

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

        $experimentalPayload = @(Get-ChildItem -LiteralPath $appRoot -Recurse -File |
            Where-Object {
                $_.Name -eq 'CaptureTool.Infrastructure.Analysis.Windows.Experimental.dll' -or
                $_.Name -eq 'ExperimentalWindowsAiCaptureAnalysisProviders.json'
            })
        if ($experimentalPayload.Count -gt 0) {
            throw "$($appPackage.Name) contains experimental Windows AI payload: $($experimentalPayload.Name -join ', ')."
        }

        $foundryCliPayload = @(Get-ChildItem -LiteralPath $appRoot -Recurse -File |
            Where-Object {
                $_.Name -in @('foundry.exe', 'foundry.cmd', 'foundry-local.exe')
            })
        if ($foundryCliPayload.Count -gt 0) {
            throw "$($appPackage.Name) contains a Foundry Local CLI payload: $($foundryCliPayload.Name -join ', ')."
        }

        $runtimeIdentifier = "win-$architecture"
        Assert-PackagedNativeAsset `
            -AppRoot $appRoot `
            -FileName 'Microsoft.AI.Foundry.Local.Core.dll' `
            -ExpectedPath (Join-Path $nugetPackageRoot "microsoft.ai.foundry.local.core.winml\$foundryCoreVersion\runtimes\$runtimeIdentifier\native\Microsoft.AI.Foundry.Local.Core.dll") `
            -PackageName "Microsoft.AI.Foundry.Local.Core.WinML/$foundryCoreVersion"
        Assert-PackagedNativeAsset `
            -AppRoot $appRoot `
            -FileName 'Microsoft.Windows.AI.MachineLearning.dll' `
            -ExpectedPath (Join-Path $nugetPackageRoot "microsoft.windows.ai.machinelearning\$winMlVersion\runtimes\$runtimeIdentifier\native\Microsoft.Windows.AI.MachineLearning.dll") `
            -PackageName "Microsoft.Windows.AI.MachineLearning/$winMlVersion"

        $appExecutable = Get-ChildItem -LiteralPath $appRoot -Recurse -File -Filter 'CaptureTool.Presentation.Windows.WinUI.exe' |
            Select-Object -First 1
        if (-not $appExecutable) {
            throw "$($appPackage.Name) does not contain the Native AOT application executable."
        }

        $providerManifestFiles = @(Get-ChildItem -LiteralPath $appRoot -Recurse -File `
            -Filter '*CaptureAnalysisProviders.json')
        if ($providerManifestFiles.Count -eq 0) {
            throw "$($appPackage.Name) does not contain a Capture Analysis provider manifest."
        }

        $packagedProviders = @()
        foreach ($providerManifestFile in $providerManifestFiles) {
            $providerManifest = Get-Content -Raw -LiteralPath $providerManifestFile.FullName |
                ConvertFrom-Json
            if ($providerManifest.schemaVersion -ne 1 -or
                $providerManifest.processingBoundary -ne 'on-device') {
                throw "$($appPackage.Name) contains an invalid or non-local provider manifest '$($providerManifestFile.Name)'."
            }

            $packagedProviders += @($providerManifest.providers)
        }

        $foundryProvider = @($packagedProviders) |
            Where-Object { $_.providerId -eq 'microsoft-foundry-local' } |
            Select-Object -First 1
        if (-not $foundryProvider) {
            throw "$($appPackage.Name) does not declare the Microsoft Foundry Local provider."
        }
        if (-not (@($foundryProvider.analyzers).analyzerId -contains
            'foundry-local-speech-transcript')) {
            throw "$($appPackage.Name) does not declare the Foundry Local speech adapter."
        }
        if (-not (@($foundryProvider.analyzers).analyzerId -contains
            'foundry-local-nemotron-multilingual-speech-transcript')) {
            throw "$($appPackage.Name) does not declare the preferred Foundry Local multilingual speech adapter."
        }

        $provider = @($packagedProviders) |
            Where-Object { $_.providerId -eq $run.providerId } |
            Select-Object -First 1
        if (-not $provider) {
            throw "$($appPackage.Name) does not declare evaluated provider '$($run.providerId)'."
        }

        $actualAnalyzerIds = @($provider.analyzers | ForEach-Object { $_.analyzerId } | Sort-Object)
        $missingAnalyzerIds = @($expectedAnalyzerIds | Where-Object { $_ -notin $actualAnalyzerIds })
        if ($missingAnalyzerIds.Count -gt 0) {
            throw "$($appPackage.Name) is missing evaluated provider adapters: $($missingAnalyzerIds -join ', ')."
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
