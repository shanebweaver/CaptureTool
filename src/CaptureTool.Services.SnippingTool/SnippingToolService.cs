using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CaptureTool.FeatureManagement;
using Windows.System;

namespace CaptureTool.Services.SnippingTool;

public class SnippingToolService : ISnippingToolService
{
    private const string CaptureToolRedirectUri = "capture-tool://response";

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

    public async Task CaptureImageAsync(SnippingToolCaptureOptions options)
    {
        SnippingToolEnabledMode[] enabledModes = await GetEnabledModesAsync();
        SnippingToolRequest request = SnippingToolRequest.CaptureImage(options.CaptureMode, enabledModes, CaptureToolRedirectUri);
        Uri requestUri = new(request.ToString());
        await Launcher.LaunchUriAsync(requestUri);
    }

    public async Task CaptureVideoAsync(SnippingToolCaptureOptions options)
    {
        SnippingToolEnabledMode[] enabledModes = await GetEnabledModesAsync();
        SnippingToolRequest request = SnippingToolRequest.CaptureVideo(options.CaptureMode, enabledModes, CaptureToolRedirectUri);
        Uri requestUri = new(request.ToString());
        await Launcher.LaunchUriAsync(requestUri);
    }

    private async Task<SnippingToolEnabledMode[]> GetEnabledModesAsync()
    {
        List<SnippingToolEnabledMode> enabledModes = [];
        bool isImageDesktopCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Image);
        if (isImageDesktopCaptureEnabled)
        {
            enabledModes.Add(SnippingToolEnabledMode.SnippingAllModes);
        }
        bool isVideoDesktopCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Video);
        if (isVideoDesktopCaptureEnabled)
        {
            enabledModes.Add(SnippingToolEnabledMode.RecordAllModes);
        }

        return enabledModes.ToArray();
    }
}
