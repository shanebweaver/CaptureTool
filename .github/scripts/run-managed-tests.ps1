$ErrorActionPreference = 'Stop'

$testProjects = @(
    'src\CaptureTool.Application.Tests\CaptureTool.Application.Tests.csproj',
    'src\CaptureTool.Domain.Edit.Tests.Windows\CaptureTool.Domain.Edit.Tests.Windows.csproj',
    'src\CaptureTool.Infrastructure.Tests\CaptureTool.Infrastructure.Tests.csproj',
    'src\CaptureTool.Presentation.Tests\CaptureTool.Presentation.Tests.csproj'
)

foreach ($testProject in $testProjects) {
    dotnet test $testProject -p:Platform=x64 --configuration Release

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$prebuiltTestAssemblies = @(
    'src\CaptureTool.Domain.Capture.Tests.Windows\bin\x64\Release\net10.0-windows10.0.26100.0\CaptureTool.Domain.Capture.Tests.Windows.dll'
)

foreach ($testAssembly in $prebuiltTestAssemblies) {
    if (-not (Test-Path $testAssembly)) {
        Write-Host "Skipping $testAssembly because it has not been built."
        continue
    }

    dotnet test $testAssembly

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
