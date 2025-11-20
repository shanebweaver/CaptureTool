namespace CaptureTool.Services.Interfaces.Navigation;

public interface INavigationRequest
{
    bool ClearHistory { get; }
    bool IsBackNavigation { get; }
    object? Parameter { get; }
    object Route { get; }
}