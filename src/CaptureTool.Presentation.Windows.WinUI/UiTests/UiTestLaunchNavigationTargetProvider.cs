using CaptureTool.Application.Abstractions.Activation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal sealed class UiTestLaunchNavigationTargetProvider : ILaunchNavigationTargetProvider
{
    private readonly UiTestLaunchOptions _options;

    public UiTestLaunchNavigationTargetProvider(UiTestLaunchOptions options)
    {
        _options = options;
    }

    public LaunchNavigationTarget? GetLaunchNavigationTarget()
    {
        return string.IsNullOrWhiteSpace(_options.ImageFilePath)
            ? null
            : new LaunchNavigationTarget(
                NavigationRoute.ImageEdit,
                new ImageFile(_options.ImageFilePath),
                true);
    }
}
