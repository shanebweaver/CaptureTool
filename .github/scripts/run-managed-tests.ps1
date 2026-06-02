$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

$testProjects = @(
    'src\CaptureTool.Application.Tests\CaptureTool.Application.Tests.csproj',
    'src\CaptureTool.Domain.Edit.Tests\CaptureTool.Domain.Edit.Tests.csproj',
    'src\CaptureTool.Infrastructure.Tests\CaptureTool.Infrastructure.Tests.csproj',
    'src\CaptureTool.Presentation.Tests\CaptureTool.Presentation.Tests.csproj'
)

foreach ($testProject in $testProjects) {
    dotnet test $testProject -p:Platform=x64 --configuration Release

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
