param(
    [Parameter(Mandatory)][string]$BinaryDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [switch]$Packaged,
    [switch]$PrepareWhisper,
    [switch]$PrepareVision,
    [switch]$PrepareAll
)
$ErrorActionPreference = 'Stop'
$binaryRoot = (Resolve-Path -LiteralPath $BinaryDirectory).Path
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$executable = Join-Path $binaryRoot 'CaptureTool.Analysis.Smoke.exe'
if (!(Test-Path -LiteralPath $executable)) { throw 'Build or publish the smoke executable first.' }
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

# Fixed synthetic speech crosses the 15-second chunk boundary. No microphone is used.
$voice = New-Object -ComObject SAPI.SpVoice
$stream = New-Object -ComObject SAPI.SpFileStream
try {
    $stream.Open((Join-Path $outputRoot 'synthetic.wav'), 3, $false)
    $voice.AudioOutputStream = $stream
    $speech = 'Capture analysis. This is a synthetic speech test. The quick brown fox jumps over the lazy dog. '
    [void]$voice.Speak($speech * 4)
}
finally {
    $stream.Close()
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($stream)
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($voice)
}
foreach ($name in @('synthetic.mp4', 'synthetic-shapes.mp4', 'exit-code.txt', 'results.json')) {
    $fixture = Join-Path $outputRoot $name
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture }
}
$arguments = '"' + $outputRoot + '"'
if ($PrepareAll) { $arguments += ' --prepare-all' }
elseif ($PrepareWhisper) { $arguments += ' --prepare-whisper' }
if ($PrepareVision) { $arguments += ' --prepare-vision' }
$packageName = 'CaptureTool.AnalysisSmoke.Slice2'
$registered = $false
$createdManifest = $false
try {
    if ($Packaged) {
        if (Get-AppxPackage -Name $packageName) { throw 'A smoke package is already registered; finish that run first.' }
        $manifest = @'
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
 xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
 xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
 xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
 IgnorableNamespaces="uap uap10 rescap">
 <Identity Name="CaptureTool.AnalysisSmoke.Slice2" Publisher="CN=CaptureToolSmoke" Version="1.0.0.0" ProcessorArchitecture="x64" />
 <Properties><DisplayName>Analysis smoke</DisplayName><PublisherDisplayName>CaptureTool</PublisherDisplayName><Logo>SmokeLogo.png</Logo></Properties>
 <Resources><Resource Language="en-us" /></Resources>
 <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.26100.0" MaxVersionTested="10.0.26100.0" /></Dependencies>
 <Applications><Application Id="Smoke" Executable="CaptureTool.Analysis.Smoke.exe" EntryPoint="Windows.FullTrustApplication"
   uap10:RuntimeBehavior="packagedClassicApp" uap10:TrustLevel="mediumIL">
  <uap:VisualElements AppListEntry="none" DisplayName="Analysis smoke" Description="Synthetic provider checks" BackgroundColor="transparent"
   Square150x150Logo="SmokeLogo.png" Square44x44Logo="SmokeLogo.png" />
 </Application></Applications>
 <Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>
</Package>
'@
        $manifestPath = Join-Path $binaryRoot 'AppxManifest.xml'
        if (Test-Path -LiteralPath $manifestPath) { throw 'The binary directory already contains a package manifest.' }
        $manifest | Set-Content -LiteralPath $manifestPath -Encoding utf8
        $createdManifest = $true
        $logo = Join-Path $PSScriptRoot '../../src/CaptureTool.Presentation.Windows.WinUI/Assets/StoreLogo.scale-100.png'
        Copy-Item -LiteralPath $logo -Destination (Join-Path $binaryRoot 'SmokeLogo.png')
        Add-AppxPackage -Register $manifestPath
        $registered = $true
        $package = Get-AppxPackage -Name $packageName
        Invoke-CommandInDesktopPackage -PackageFamilyName $package.PackageFamilyName -AppId Smoke -Command $executable -Args $arguments -PreventBreakaway
        $deadline = [DateTime]::UtcNow.AddMinutes(25)
        $exitPath = Join-Path $outputRoot 'exit-code.txt'
        while (!(Test-Path -LiteralPath $exitPath) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Seconds 1 }
        if (!(Test-Path -LiteralPath $exitPath)) { throw 'Packaged smoke did not complete within its process budget.' }
        $exitCode = [int](Get-Content -LiteralPath $exitPath)
        $report = Get-Content -LiteralPath (Join-Path $outputRoot 'results.json') | ConvertFrom-Json
        if (!$report.Packaged) { throw 'The smoke process did not acquire package identity.' }
    }
    else {
        $process = Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Hidden -PassThru
        if (!$process.WaitForExit(1500000)) { $process.Kill(); throw 'Smoke exceeded its process budget.' }
        $exitCode = $process.ExitCode
    }
    Get-Content -LiteralPath (Join-Path $outputRoot 'results.json')
    if ($exitCode -ne 0) { throw "Provider smoke failed: $exitCode" }
}
finally {
    if ($registered) { Get-AppxPackage -Name $packageName | Remove-AppxPackage }
    if ($createdManifest) {
        Remove-Item -LiteralPath $manifestPath
        $smokeLogo = Join-Path $binaryRoot 'SmokeLogo.png'
        if (Test-Path -LiteralPath $smokeLogo) { Remove-Item -LiteralPath $smokeLogo }
    }
}
