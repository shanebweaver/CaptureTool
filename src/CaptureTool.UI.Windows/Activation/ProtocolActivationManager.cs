using CaptureTool.Capture;
using CaptureTool.Capture.Windows;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
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

                string source = queryParams.Get("source") ?? string.Empty;
                if (source == "PrintScreen")
                {
                    // PrtSc key
                    // Capture all monitors and silently put the image in the users clipboard.
                    List<MonitorCaptureResult> monitors = MonitorCaptureHelper.CaptureAllMonitors();
                    ClipboardImageHelper.CombineMonitorsAndCopyToClipboard(monitors);
                }
                else if (source == "ScreenRecorderHotKey" || isRecordingType)
                {
                    // Video capture
                    CaptureMode captureMode = CaptureMode.Video;
                    CaptureType captureType = CaptureType.Rectangle;
                    CaptureOptions captureOptions = new(captureMode, captureType);
                    ServiceLocator.AppController.ShowCaptureOverlay(captureOptions);
                }
                else if (source == "HotKey")
                {
                    // Image capture
                    CaptureMode captureMode = CaptureMode.Image;
                    CaptureType captureType = CaptureType.Rectangle;
                    CaptureOptions captureOptions = new(captureMode, captureType);
                    ServiceLocator.AppController.ShowCaptureOverlay(captureOptions);
                }
            }
        }
    }
}
