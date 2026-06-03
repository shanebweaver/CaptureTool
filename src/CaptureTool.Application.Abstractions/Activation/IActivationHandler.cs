namespace CaptureTool.Application.Abstractions.Activation;

public partial interface IActivationHandler
{
    Task HandleLaunchActivationAsync();
    Task HandleProtocolActivationAsync(Uri protocolUri);
}
