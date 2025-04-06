using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CaptureTool.FeatureManagement;
using Windows.System;

namespace CaptureTool.Services.SnippingTool;

public class SnippingToolService : ISnippingToolService
{
    public const string CaptureToolRedirectUri = "capture-tool://response";

    private readonly IFeatureManager _featureManager;

    public event EventHandler<SnippingToolResponse>? ResponseReceived;

    public SnippingToolService(IFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    public void HandleSnippingToolResponse(SnippingToolResponse response)
    {
        ResponseReceived?.Invoke(this, response);
    }

    public async Task DiscoverSupportAsync()
    {
        SnippingToolRequest request = SnippingToolRequest.DiscoverSupport(CaptureToolRedirectUri);
        Uri requestUri = new(request.ToString());
        await Launcher.LaunchUriAsync(requestUri);
    }

    public async Task CaptureImageAsync()
    {
        bool isImageDesktopCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Image);
        bool isVideoDesktopCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Video);

        List<SnippingToolEnabledMode> enabledModes = [];
        if (isImageDesktopCaptureEnabled)
        {
            enabledModes.Add(SnippingToolEnabledMode.SnippingAllModes);
        }
        if (isVideoDesktopCaptureEnabled)
        {
            enabledModes.Add(SnippingToolEnabledMode.RecordAllModes);
        }

        SnippingToolRequest request = SnippingToolRequest.CaptureImage(SnippingToolCaptureMode.Rectangle, [.. enabledModes], CaptureToolRedirectUri);
        Uri requestUri = new(request.ToString());
        await Launcher.LaunchUriAsync(requestUri);
    }

    public async Task CaptureVideoAsync()
    {
        SnippingToolRequest request = SnippingToolRequest.CaptureVideo([SnippingToolEnabledMode.All], CaptureToolRedirectUri);
        Uri requestUri = new(request.ToString());
        await Launcher.LaunchUriAsync(requestUri);
    }
}
