using CaptureTool.Capture.Windows.SnippingTool;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace CaptureTool.UI.Activation;

internal static class ProtocolActivationHandler
{
    private const string SnippingToolPfn = "Microsoft.ScreenSketch_8wekyb3d8bbwe";
    private const string SnippingToolResponseHost = "response";

    public static void HandleActivation(AppActivationArguments args)
    {
        if (args.Data is ProtocolActivatedEventArgs protocolArgs)
        {
            // Handle responses from SnippingTool
            if (protocolArgs.CallerPackageFamilyName == SnippingToolPfn &&
                protocolArgs.Uri.Host == SnippingToolResponseHost)
            {
                SnippingToolResponse response = SnippingToolResponse.CreateFromUri(protocolArgs.Uri);
                ServiceLocator.SnippingToolService.HandleSnippingToolResponse(response);
            }
        }
    }
}
