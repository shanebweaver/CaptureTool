namespace CaptureTool.Application.Abstractions.Navigation;

public interface INavigationHandler
{
    void HandleNavigationRequest(INavigationRequest request);
}
