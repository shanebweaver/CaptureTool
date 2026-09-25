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
# Read the executable's PE machine rather than assuming the shell's architecture.
$binary = [IO.File]::OpenRead($executable)
$reader = [IO.BinaryReader]::new($binary)
try {
    $binary.Position = 0x3c
    $binary.Position = $reader.ReadInt32() + 4
    $architecture = switch ($reader.ReadUInt16()) { 0x8664 { 'x64' } 0xaa64 { 'arm64' } default { throw 'Unsupported smoke executable architecture.' } }
}
finally { $reader.Dispose(); $binary.Dispose() }
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
# Optional installed desktop voices make the multilingual checks repeatable without
# recording a person or downloading speech fixtures. Missing voices remain reported.
foreach ($fixture in @(
    @{ Language = 'de'; Voice = 'Hedda'; Text = 'Guten Morgen. Dies ist ein Test. Der Himmel ist blau und die Sonne scheint.' },
    @{ Language = 'fr'; Voice = 'Hortense'; Text = 'Bonjour. Ceci est un test. Le ciel est bleu et le soleil brille.' }
)) {
    $path = Join-Path $outputRoot ("synthetic-" + $fixture.Language + '.wav')
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    $voice = New-Object -ComObject SAPI.SpVoice
    $stream = $null
    try {
        $selected = $voice.GetVoices() | Where-Object { $_.GetDescription().Contains($fixture.Voice) } | Select-Object -First 1
        if ($null -eq $selected) { continue }
        $voice.Voice = $selected
        $stream = New-Object -ComObject SAPI.SpFileStream
        $stream.Open($path, 3, $false)
        $voice.AudioOutputStream = $stream
        [void]$voice.Speak($fixture.Text)
    }
    finally {
        if ($null -ne $stream) { $stream.Close(); [void][Runtime.InteropServices.Marshal]::ReleaseComObject($stream) }
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($voice)
    }
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
$process = $null
try {
    if ($Packaged) {
        if (Get-AppxPackage -Name $packageName) { throw 'A smoke package is already registered; finish that run first.' }
        $manifest = @'
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
 xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
 xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
 xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
 IgnorableNamespaces="uap uap10 rescap">
 <Identity Name="CaptureTool.AnalysisSmoke.Slice2" Publisher="CN=CaptureToolSmoke" Version="1.0.0.0" ProcessorArchitecture="__ARCH__" />
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
'@.Replace('__ARCH__', $architecture)
        $manifestPath = Join-Path $binaryRoot 'AppxManifest.xml'
        if (Test-Path -LiteralPath $manifestPath) { throw 'The binary directory already contains a package manifest.' }
        $manifest | Set-Content -LiteralPath $manifestPath -Encoding utf8
        $createdManifest = $true
        $logo = Join-Path $PSScriptRoot '../../src/CaptureTool.Presentation.Windows.WinUI/Assets/StoreLogo.scale-100.png'
        Copy-Item -LiteralPath $logo -Destination (Join-Path $binaryRoot 'SmokeLogo.png')
        Add-AppxPackage -Register $manifestPath
        $registered = $true
        $package = Get-AppxPackage -Name $packageName
        if (Get-Process -Name CaptureTool.Analysis.Smoke -ErrorAction SilentlyContinue | Where-Object Path -eq $executable) {
            throw 'This smoke executable is already running.'
        }
        Invoke-CommandInDesktopPackage -PackageFamilyName $package.PackageFamilyName -AppId Smoke -Command $executable -Args $arguments -PreventBreakaway
        $deadline = [DateTime]::UtcNow.AddMinutes(25)
        $startupDeadline = [DateTime]::UtcNow.AddSeconds(30)
        $exitPath = Join-Path $outputRoot 'exit-code.txt'
        while (!(Test-Path -LiteralPath $exitPath) -and [DateTime]::UtcNow -lt $deadline) {
            if ($null -eq $process) {
                $process = Get-Process -Name CaptureTool.Analysis.Smoke -ErrorAction SilentlyContinue | Where-Object Path -eq $executable | Select-Object -First 1
                if ($null -eq $process -and [DateTime]::UtcNow -gt $startupDeadline) { throw 'Packaged smoke exited or failed to start without a result.' }
            }
            elseif ($process.HasExited -and !(Test-Path -LiteralPath $exitPath)) { throw 'Packaged smoke exited without a result.' }
            Start-Sleep -Seconds 1
        }
        if (!(Test-Path -LiteralPath $exitPath)) { throw 'Packaged smoke did not complete within its process budget.' }
        $exitCode = [int](Get-Content -LiteralPath $exitPath)
        $report = Get-Content -LiteralPath (Join-Path $outputRoot 'results.json') | ConvertFrom-Json
        if (!$report.Packaged) { throw 'The smoke process did not acquire package identity.' }
        if ($report.ProcessArchitecture.ToLowerInvariant() -ne $architecture) { throw 'Smoke report architecture does not match the executable.' }
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
    if ($null -ne $process) {
        if (!$process.HasExited -and !$process.WaitForExit(10000)) { $process.Kill(); $process.WaitForExit() }
        $process.Dispose()
    }
    if ($registered) { Get-AppxPackage -Name $packageName | Remove-AppxPackage }
    if ($createdManifest) {
        Remove-Item -LiteralPath $manifestPath
        $smokeLogo = Join-Path $binaryRoot 'SmokeLogo.png'
        if (Test-Path -LiteralPath $smokeLogo) { Remove-Item -LiteralPath $smokeLogo }
    }
}
