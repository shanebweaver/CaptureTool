namespace CaptureTool.Application.Abstractions.Activation;

public interface ILaunchNavigationTargetProvider
{
    LaunchNavigationTarget? GetLaunchNavigationTarget();
}
