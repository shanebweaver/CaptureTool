using CaptureTool.Capture.Image;
using CaptureTool.Capture.Video;
using CaptureTool.Capture.Windows.SnippingTool;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace CaptureTool.UI.Windows.Activation;

internal static class ProtocolActivationHandler
{
    private const string SnippingToolPfn = "Microsoft.ScreenSketch_8wekyb3d8bbwe";
    private const string SnippingToolResponseHost = "response";

    public static void HandleActivation(AppActivationArguments args)
    {
        if (args.Data is ProtocolActivatedEventArgs protocolArgs)
        {
            string protocolString = protocolArgs.Uri.ToString();

            if (protocolString.StartsWith("ms-screenclip:"))
            {
                if (protocolString.Contains("type=recording"))
                {
                    // Video
                    VideoCaptureOptions options = new(VideoCaptureMode.Rectangle, VideoFileType.Mp4, true);
                    _ = ServiceLocator.AppController.NewVideoCaptureAsync(options);
                }
                else
                {
                    // Images
                    ImageCaptureOptions options = new(ImageCaptureMode.Rectangle, ImageFileType.Png, true);
                    _ = ServiceLocator.AppController.NewImageCaptureAsync(options);
                }
            }
            // Handle responses from SnippingTool
            else if (
                protocolArgs.CallerPackageFamilyName == SnippingToolPfn &&
                protocolArgs.Uri.Host == SnippingToolResponseHost)
            {
                SnippingToolResponse response = SnippingToolResponse.CreateFromUri(protocolArgs.Uri);
                ServiceLocator.SnippingToolService.HandleSnippingToolResponse(response);
            }
        }
    }
}
