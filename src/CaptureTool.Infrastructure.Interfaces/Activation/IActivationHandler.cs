namespace CaptureTool.Infrastructure.Interfaces.Activation;

public partial interface IActivationHandler
{
    Task HandleLaunchActivationAsync();
    Task HandleProtocolActivationAsync(Uri protocolUri);
}
