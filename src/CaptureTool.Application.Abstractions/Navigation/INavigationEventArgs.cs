namespace CaptureTool.Application.Abstractions.Navigation;

public interface INavigationEventArgs
{
    INavigationRequest Request { get; }
}