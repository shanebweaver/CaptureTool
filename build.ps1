[CmdletBinding()]
param(
    [string]$Solution = (Join-Path $PSScriptRoot 'CaptureTool.slnx'),
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [string]$Platform = 'x64',
    [switch]$NoRestore,
    [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
    [string]$Verbosity = 'minimal',
    [string]$MSBuildPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-MSBuildPath {
    param(
        [string]$ExplicitPath
    )

    if ($ExplicitPath) {
        if (Test-Path -LiteralPath $ExplicitPath -PathType Leaf) {
            return (Resolve-Path -LiteralPath $ExplicitPath).Path
        }

        throw "MSBuild was not found at '$ExplicitPath'."
    }

    $programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
    $vswherePath = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'

    if (Test-Path -LiteralPath $vswherePath -PathType Leaf) {
        $queries = @(
            @('-latest', '-products', '*', '-requires', 'Microsoft.Component.MSBuild', 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64', '-find', 'MSBuild\Current\Bin\MSBuild.exe'),
            @('-latest', '-products', '*', '-requires', 'Microsoft.Component.MSBuild', '-find', 'MSBuild\Current\Bin\MSBuild.exe')
        )

        foreach ($query in $queries) {
            $matches = & $vswherePath @query
            foreach ($match in $matches) {
                if ($match -and (Test-Path -LiteralPath $match -PathType Leaf)) {
                    return $match
                }
            }
        }
    }

    $programFiles = [Environment]::GetFolderPath('ProgramFiles')
    $commonInstallRoots = @(
        (Join-Path $programFiles 'Microsoft Visual Studio\18'),
        (Join-Path $programFiles 'Microsoft Visual Studio\2022')
    )
    $editions = @('Community', 'Professional', 'Enterprise', 'BuildTools', 'Preview')

    foreach ($root in $commonInstallRoots) {
        foreach ($edition in $editions) {
            $candidate = Join-Path $root "$edition\MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                return $candidate
            }
        }
    }

    throw @"
Could not find Visual Studio MSBuild.

Install Visual Studio with the "Desktop development with C++" workload, or pass -MSBuildPath with the full path to MSBuild.exe.
This solution contains C++ projects, so dotnet build may not resolve Microsoft.Cpp.Default.props from a normal shell.
"@
}

$resolvedSolution = Resolve-Path -LiteralPath $Solution
$resolvedMSBuildPath = Resolve-MSBuildPath -ExplicitPath $MSBuildPath

Write-Host "Using MSBuild: $resolvedMSBuildPath"
Write-Host "Building: $($resolvedSolution.Path)"

$msbuildArgs = @(
    $resolvedSolution.Path,
    '/m',
    '/nologo',
    "/v:$Verbosity",
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform"
)

if (-not $NoRestore) {
    $msbuildArgs += '/restore'
}

& $resolvedMSBuildPath @msbuildArgs
exit $LASTEXITCODE
