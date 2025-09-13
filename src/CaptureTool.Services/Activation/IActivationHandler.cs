using System;
using System.Threading.Tasks;

namespace CaptureTool.Services.Activation;

public partial interface IActivationHandler
{
    Task HandleLaunchActivationAsync();
    Task HandleProtocolActivationAsync(Uri protocolUri);
}
