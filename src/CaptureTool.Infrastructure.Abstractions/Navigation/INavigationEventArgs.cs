namespace CaptureTool.Infrastructure.Abstractions.Navigation;

public interface INavigationEventArgs
{
    INavigationRequest Request { get; }
}