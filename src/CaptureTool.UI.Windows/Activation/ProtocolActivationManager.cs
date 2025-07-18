using CaptureTool.Capture;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using Windows.ApplicationModel.Activation;
using Windows.Foundation.Collections;
using Windows.System;

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
                string host = protocolUri.Host;
                if (host == "capture")
                {
                    HandleCaptureRequest(protocolUri);
                }
                else if (host == "discover")
                {
                    HandleDiscoverRequest(protocolUri);
                }
                else
                {
                    // Default, use Capture
                    HandleCaptureRequest(protocolUri);
                }
            }
        }
    }

    private static void HandleCaptureRequest(Uri protocolUri)
    {
        CaptureMode captureMode = ParseCaptureMode(protocolUri);

        NameValueCollection queryParams = HttpUtility.ParseQueryString(protocolUri.Query) ?? [];
        bool autoSave = ParseAutoSave(queryParams);
        CaptureType captureType = ParseCaptureType(queryParams);

        CaptureOptions captureOptions = new(captureMode, captureType, autoSave);
        ServiceLocator.AppController.ShowCaptureOverlay(captureOptions);
    }

    private static CaptureMode ParseCaptureMode(Uri protocolUri)
    {
        var path = protocolUri.AbsolutePath.TrimStart('/');
        if (path == "image")
        {
            return CaptureMode.Image;
        }
        else if (path == "video")
        {
            return CaptureMode.Video;
        }
        else
        {
            // Default, use Image
            return CaptureMode.Image;
        }
    }

    private static bool ParseAutoSave(NameValueCollection queryParameters)
    {
        return queryParameters.Get("auto-save") is string autoSaveParam && bool.TryParse(autoSaveParam, out bool autoSave) && autoSave;
    }

    private static CaptureType ParseCaptureType(NameValueCollection queryParameters)
    {
        if (queryParameters.AllKeys.Contains("rectangle"))
        {
            return CaptureType.Rectangle;
        }
        else if (queryParameters.AllKeys.Contains("fullscreen"))
        {
            return CaptureType.FullScreen;
        }
        else
        {
            // Default, use Rectangle
            return CaptureType.Rectangle;
        }
    }

    private static void HandleDiscoverRequest(Uri protocolUri)
    {
        try
        {
            var queryParams = HttpUtility.ParseQueryString(protocolUri.Query) ?? [];
            if (queryParams.Get("redirect-uri") is string redirectString)
            {
                float version = 1.0f;
                DiscoverResponseCapability[] capabilities = [
                    new("capture/image", ["GET"], ["rectangle", "fullscreen"], "Captures an image in a defined area."),
                    //new("capture/video", ["GET"], ["rectangle", "fullscreen"], "Captures a video in a defined area.")
                ];
                DiscoverResponse response = new(version, capabilities);

                Uri redirectUri = new(redirectString);
                LauncherOptions options = new();
                ValueSet data = response.ToValueSet();
                _ = Launcher.LaunchUriAsync(redirectUri, options, data);
            }
        }
        catch (Exception)
        {
            // TODO: Log exception
        }
    }
}
