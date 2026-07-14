using CaptureTool.Application.Abstractions.Activation;

namespace CaptureTool.Application.Activation;

internal sealed class DefaultLaunchNavigationTargetProvider : ILaunchNavigationTargetProvider
{
    public LaunchNavigationTarget? GetLaunchNavigationTarget()
    {
        return null;
    }
}
