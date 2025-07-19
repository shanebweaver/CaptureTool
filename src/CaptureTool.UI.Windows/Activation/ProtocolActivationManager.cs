using CaptureTool.Capture;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Specialized;
using System.Web;
using Windows.ApplicationModel.Activation;

namespace CaptureTool.UI.Windows.Activation;

internal sealed partial class ProtocolActivationManager
{
    public static void HandleActivation(AppActivationArguments args)
    {
        if (args.Data is IProtocolActivatedEventArgs protocolArgs)
        {
            Uri protocolUri = protocolArgs.Uri;
            if (protocolUri.Scheme == "ms-screenclip")
            {
                NameValueCollection queryParams = HttpUtility.ParseQueryString(protocolUri.Query) ?? [];
                bool isRecordingType = queryParams.Get("type") is string type && type == "recording";

                CaptureMode captureMode = isRecordingType ? CaptureMode.Video : CaptureMode.Image;
                CaptureType captureType = CaptureType.FullScreen;
                CaptureOptions captureOptions = new(captureMode, captureType);

                ServiceLocator.AppController.ShowCaptureOverlay(captureOptions);
            }
        }
    }
}
