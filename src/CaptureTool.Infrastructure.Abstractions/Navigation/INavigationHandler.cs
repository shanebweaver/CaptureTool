namespace CaptureTool.Infrastructure.Abstractions.Navigation;

public interface INavigationHandler
{
    void HandleNavigationRequest(INavigationRequest request);
}
