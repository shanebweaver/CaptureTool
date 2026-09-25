param(
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64',
    [string]$OutputDirectory = 'artifacts/native-aot'
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
$platformRoot = Join-Path $outputRoot $Platform
New-Item -ItemType Directory -Path $platformRoot -Force | Out-Null
$rid = 'win-' + $Platform.ToLowerInvariant()
$targets = @(
    @{ Name = 'app'; Project = 'src/CaptureTool.Presentation.Windows.WinUI/CaptureTool.Presentation.Windows.WinUI.csproj'; Extra = @('-p:WindowsPackageType=None', '-p:WindowsAppSDKSelfContained=true', '-p:EnableMsixTooling=false') },
    @{ Name = 'harness'; Project = 'tools/CaptureTool.Analysis.Smoke/CaptureTool.Analysis.Smoke.csproj'; Extra = @() }
)
Push-Location $repo
try {
    foreach ($target in $targets) {
        $destination = Join-Path $platformRoot $target.Name
        $log = Join-Path $platformRoot ($target.Name + '.log')
        Write-Host "Publishing $($target.Name) with Native AOT for $Platform..."
        # PublishAot is local to each executable. A global property would reach
        # the netstandard build-time generator and fail before compiling the app.
        $arguments = @('publish', $target.Project, '-c', 'Release', "-p:Platform=$Platform", '-r', $rid,
            '-o', $destination, '-m:1', '-p:TrimmerSingleWarn=false') + $target.Extra
        & dotnet @arguments *> $log
        $publishExit = $LASTEXITCODE
        & (Join-Path $PSScriptRoot 'assert-native-aot-log.ps1') -LogPath $log -PublishExitCode $publishExit
        if ($target.Name -eq 'app') {
            foreach ($resource in @('CaptureTool.Presentation.Windows.WinUI.pri', 'Xaml/Windows/MainWindow.xbf', 'Themes/Generic.xbf', 'Fonts/FluentSystemIcons-Regular.ttf', 'Assets/StoreLogo.scale-100.png')) {
                $path = Join-Path $destination $resource
                if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
                    throw "Published UI resource is missing or empty: $resource"
                }
            }
        }
    }
}
finally { Pop-Location }
