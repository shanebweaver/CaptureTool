param(
    [Parameter(Mandatory)][string]$BinaryDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$binaryRoot = (Resolve-Path -LiteralPath $BinaryDirectory).Path
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$executable = Join-Path $binaryRoot 'CaptureTool.Analysis.Smoke.exe'
$reader = [IO.BinaryReader]::new([IO.File]::OpenRead($executable))
try {
    $reader.BaseStream.Position = 0x3c
    $reader.BaseStream.Position = $reader.ReadInt32() + 4
    $architecture = switch ($reader.ReadUInt16()) { 0x8664 { 'X64' } 0xaa64 { 'Arm64' } default { throw 'Unsupported executable architecture.' } }
}
finally { $reader.Dispose() }
$hostArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
if ($architecture -ne $hostArchitecture) { throw "Native checks require a matching host: binary=$architecture, host=$hostArchitecture." }

# The managed PowerShell host is an independent CPU-intrinsic oracle for the
# AOT executable, where intrinsic support can be limited by compilation flags.
if (-not ('CaptureToolSmokeHostMemory' -as [type])) {
    Add-Type @'
using System.Runtime.InteropServices;
public static class CaptureToolSmokeHostMemory {
    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetPhysicallyInstalledSystemMemory(out ulong kilobytes);
}
'@
}
$installedKilobytes = [ulong]0
$memoryKnown = [CaptureToolSmokeHostMemory]::GetPhysicallyInstalledSystemMemory([ref]$installedKilobytes)
if (!$memoryKnown) { throw 'Cannot determine installed memory for the hardware check.' }
if ([Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString() -ne $hostArchitecture) {
    throw 'Run native checks from a shell matching the operating system architecture.'
}
$expected = [Environment]::OSVersion.Version.Build -ge 26100 -and $installedKilobytes -ge (8 * 1024 * 1024) -and
    ($hostArchitecture -eq 'Arm64' -or ($hostArchitecture -eq 'X64' -and [System.Runtime.Intrinsics.X86.Avx2]::IsSupported))
$results = [ordered]@{ Architecture = $architecture; OS = [Environment]::OSVersion.Version.ToString(); InstalledKilobytes = $installedKilobytes; ExpectedFoundryCpu = $expected; Passed = $false }

function Invoke-NativeCheck([string]$Name, [string]$Options, [int]$TimeoutSeconds) {
    $process = Start-Process -FilePath $executable -ArgumentList ('"' + $outputRoot + '" ' + $Options) -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $outputRoot "$Name.log") -RedirectStandardError (Join-Path $outputRoot "$Name-error.log")
    try {
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
            $results[$Name + 'TimedOut'] = $true
            $process.Kill($true)
            $process.WaitForExit()
            throw "$Name exceeded its process deadline. See $outputRoot."
        }
        $results[$Name + 'ExitCode'] = $process.ExitCode
        if ($process.ExitCode -ne 0) { throw "$Name failed with exit code $($process.ExitCode). See $outputRoot." }
    }
    finally { $process.Dispose() }
}

try {
    Invoke-NativeCheck 'hardware' ('--hardware-check ' + $expected.ToString().ToLowerInvariant()) 60
    Invoke-NativeCheck 'recovery' '--recovery-checks' 300
    $results.Passed = $true
    Write-Host "Native hardware and recovery checks passed for $architecture. Reports: $outputRoot"
}
finally { $results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'runtime-checks.json') -Encoding utf8 }
