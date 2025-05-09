namespace CaptureTool.Services.Navigation;

public sealed partial class NavigationEventArgs(NavigationRequest request)
{
    public NavigationRequest Request { get; } = request;
}