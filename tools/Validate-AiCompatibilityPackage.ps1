[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $PackagePath,
    [Parameter(Mandatory)] [ValidateSet('x64', 'arm64')] [string] $Architecture
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$package = (Resolve-Path -LiteralPath $PackagePath).Path
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $manifestEntry = $archive.GetEntry('AppxManifest.xml')
    if (-not $manifestEntry) { throw 'Package manifest is missing.' }
    $reader = [IO.StreamReader]::new($manifestEntry.Open())
    try { [xml] $manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $family = $manifest.Package.Dependencies.TargetDeviceFamily
    if ($family.Name -ne 'Windows.Desktop' -or $family.MinVersion -ne '10.0.17763.0') {
        throw 'The package no longer preserves the Windows 10 installation minimum.'
    }
    if ([version] $family.MaxVersionTested -lt [version] '10.0.26226.0') {
        throw 'The package is missing the Windows AI MaxVersionTested declaration.'
    }
    if ($manifest.Package.Identity.ProcessorArchitecture -ne $Architecture) { throw 'Manifest architecture mismatch.' }
    $desktopCrt = @($manifest.Package.Dependencies.PackageDependency | Where-Object Name -eq 'Microsoft.VCLibs.140.00.UWPDesktop')
    if ($desktopCrt.Count -ne 1 -or [version] $desktopCrt[0].MinVersion -lt [version] '14.0.33728.0' -or
        $desktopCrt[0].Publisher -ne 'CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US') {
        throw 'The package must declare the retail desktop Visual C++ runtime required by CaptureKit recording.'
    }
    $ai = $manifest.Package.Capabilities.ChildNodes | Where-Object {
        $_.Name -eq 'systemAIModels' -and $_.NamespaceURI -eq 'http://schemas.microsoft.com/appx/manifest/systemai/windows10'
    }
    if (-not $ai -or ($manifest.Package.IgnorableNamespaces -split '\s+') -notcontains $ai.Prefix) {
        throw 'The AI capability or its ignorable namespace is missing.'
    }
    $languages = @($manifest.Package.Resources.Resource.Language)
    foreach ($language in @('en-US', 'de-DE', 'es-ES', 'fr-FR', 'ru-RU', 'zh-CN')) {
        if ($languages -notcontains $language) { throw "Missing language: $language" }
    }

    $native = foreach ($name in @(
        'CaptureTool.Presentation.Windows.WinUI.exe',
        'CaptureKit.Windows.Native.Recording.dll',
        'CaptureKit.Windows.Native.Screenshot.dll',
        'Microsoft.AI.Foundry.Local.Core.dll',
        'Microsoft.Windows.AI.MachineLearning.dll',
        'onnxruntime.dll',
        'onnxruntime-genai.dll'
    )) {
        $entries = @($archive.Entries | Where-Object FullName -eq $name)
        if ($entries.Count -ne 1) { throw "Expected exactly one native payload: $name" }
        $binary = [IO.BinaryReader]::new($entries[0].Open())
        try {
            if ($binary.ReadUInt16() -ne 0x5a4d) { throw "Invalid DOS header: $name" }
            $null = $binary.ReadBytes(58)
            $offset = $binary.ReadInt32()
            if ($offset -lt 64 -or $offset -gt 1048576) { throw "Invalid PE offset: $name" }
            $null = $binary.ReadBytes($offset - 64)
            if ($binary.ReadUInt32() -ne 0x4550) { throw "Invalid PE signature: $name" }
            $machine = $binary.ReadUInt16()
            $expected = if ($Architecture -eq 'x64') { 0x8664 } else { 0xaa64 }
            if ($machine -ne $expected) { throw "Native architecture mismatch: $name (0x$($machine.ToString('X4')))" }
            [pscustomobject] @{ Name = $name; Machine = '0x' + $machine.ToString('X4'); Bytes = $entries[0].Length }
        } finally { $binary.Dispose() }
    }
    [pscustomobject] @{
        Package = $package
        Version = $manifest.Package.Identity.Version
        Architecture = $Architecture
        MinVersion = $family.MinVersion
        MaxVersionTested = $family.MaxVersionTested
        Languages = $languages
        Dependencies = @($manifest.Package.Dependencies.PackageDependency | ForEach-Object {
            [pscustomobject] @{ Name = $_.Name; MinVersion = $_.MinVersion }
        })
        NativePayloads = @($native)
        Scope = 'Static package validation only; installation, startup, native loading, and inference still require device tests.'
    } | ConvertTo-Json -Depth 5
} finally { $archive.Dispose() }
