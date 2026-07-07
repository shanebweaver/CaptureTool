[CmdletBinding()]
param(
    [string]$Solution = (Join-Path $PSScriptRoot 'CaptureTool.slnx'),
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [string]$Platform = 'x64',
    [switch]$NoRestore,
    [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
    [string]$Verbosity = 'minimal',
    [Alias('AdditionalMSBuildArguments')]
    [string[]]$AdditionalBuildArguments = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedSolution = Resolve-Path -LiteralPath $Solution

Write-Host "Building: $($resolvedSolution.Path)"

$buildArgs = @(
    'build',
    $resolvedSolution.Path,
    '--configuration',
    $Configuration,
    '--verbosity',
    $Verbosity,
    "-p:Platform=$Platform"
)

if ($NoRestore) {
    $buildArgs += '--no-restore'
}

if ($AdditionalBuildArguments.Count -gt 0) {
    $buildArgs += $AdditionalBuildArguments
}

& dotnet @buildArgs
exit $LASTEXITCODE
