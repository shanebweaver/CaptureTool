$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

$testProjects = Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -Filter '*.Tests.csproj' |
    Sort-Object FullName |
    ForEach-Object { $_.FullName }

foreach ($testProject in $testProjects) {
    dotnet test $testProject -p:Platform=x64 --configuration Release

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
