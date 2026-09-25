param(
    [Parameter(Mandatory)][string]$BundlePath,
    [string]$MakePriPath
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$MakePriPath) {
    $MakePriPath = Get-ChildItem "${env:ProgramFiles(x86)}/Windows Kits/10/bin/*/x64/makepri.exe" |
        Sort-Object { [version]$_.Directory.Parent.Name } -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}
if (!$MakePriPath) { throw 'Windows SDK makepri.exe is required.' }
$resources = @(Get-ChildItem "$repo/src/CaptureTool.Presentation.Windows.WinUI/Strings" -Filter Resources.resw -Recurse)
if (!$resources.Count) { throw 'No supported language resources found.' }
$keys = @('CaptureMemory_SettingsHeader.Text', 'CaptureMemory_Scanning.OnContent', 'CaptureMemory_Scanning.OffContent')
$root = Join-Path ([IO.Path]::GetTempPath()) ('CaptureToolPackageLanguages-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$bundle = $null
try {
    $bundle = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($BundlePath))
    foreach ($architecture in 'x64', 'arm64') {
        $entries = @($bundle.Entries | Where-Object FullName -Like "*_${architecture}.msix")
        if ($entries.Count -ne 1) { throw "Expected one $architecture package in $BundlePath." }
        $memory = [IO.MemoryStream]::new()
        $stream = $entries[0].Open()
        try { $stream.CopyTo($memory) } finally { $stream.Dispose() }
        $memory.Position = 0
        $package = [IO.Compression.ZipArchive]::new($memory, [IO.Compression.ZipArchiveMode]::Read)
        try {
            $pri = $package.GetEntry('resources.pri')
            if (!$pri) { throw "Missing $architecture resource index." }
            $priPath = Join-Path $root "$architecture.pri"
            [IO.Compression.ZipFileExtensions]::ExtractToFile($pri, $priPath)
        }
        finally { $package.Dispose(); $memory.Dispose() }
        $dumpPath = Join-Path $root "$architecture.xml"
        & $MakePriPath dump /if $priPath /of $dumpPath /dt detailed /o *> (Join-Path $root "$architecture.log")
        if ($LASTEXITCODE -ne 0) { throw "Could not inspect the $architecture resource index." }
        $dump = [xml](Get-Content -LiteralPath $dumpPath -Raw)
        $named = $dump.SelectNodes('//*[@uri]')
        foreach ($file in $resources) {
            $language = $file.Directory.Name
            $source = [xml](Get-Content -LiteralPath $file.FullName -Raw)
            foreach ($key in $keys) {
                $expected = @($source.root.data | Where-Object name -EQ $key)
                if ($expected.Count -ne 1) { throw "Expected one source value for $language/$key." }
                $suffix = '/Resources/' + $key.Replace('.', '/')
                $resource = @($named | Where-Object { $_.uri.EndsWith($suffix, [StringComparison]::Ordinal) })
                $candidate = @($resource.Candidate | Where-Object {
                    @($_.QualifierSet.Qualifier | Where-Object { $_.name -eq 'Language' -and $_.value -eq $language }).Count -eq 1
                })
                if ($candidate.Count -ne 1 -or $candidate[0].Value -cne $expected[0].value) {
                    throw "Missing or incorrect packaged translation: $architecture/$language/$key."
                }
            }
        }
        Write-Host "Verified $architecture main package translations for all $($resources.Count) supported languages."
    }
}
finally {
    if ($null -ne $bundle) { $bundle.Dispose() }
    $resolved = [IO.Path]::GetFullPath($root)
    $temporary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$resolved.StartsWith($temporary, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'CaptureToolPackageLanguages-*') { throw 'Unsafe temporary cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
