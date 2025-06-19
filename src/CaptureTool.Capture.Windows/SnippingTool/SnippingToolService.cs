using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CaptureTool.FeatureManagement;
using Windows.Management.Deployment;
using Windows.System;

namespace CaptureTool.Capture.Windows.SnippingTool;

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
        SnippingToolEnabledMode[] enabledModes = GetEnabledModes();
        SnippingToolRequest request = SnippingToolRequest.CaptureImage(options.CaptureMode, enabledModes, CaptureToolRedirectUri);
        Uri requestUri = new(request.ToString());
        await Launcher.LaunchUriAsync(requestUri);
    }

    public async Task CaptureVideoAsync(SnippingToolCaptureOptions options)
    {
        SnippingToolEnabledMode[] enabledModes = GetEnabledModes();
        SnippingToolRequest request = SnippingToolRequest.CaptureVideo(options.CaptureMode, enabledModes, CaptureToolRedirectUri);
        Uri requestUri = new(request.ToString());
        await Launcher.LaunchUriAsync(requestUri);
    }

    private SnippingToolEnabledMode[] GetEnabledModes()
    {
        List<SnippingToolEnabledMode> enabledModes = [];
        bool isImageCaptureEnabled = _featureManager.IsEnabled(CaptureToolFeatures.Feature_Capture_Image);
        if (isImageCaptureEnabled)
        {
            enabledModes.Add(SnippingToolEnabledMode.SnippingAllModes);
        }
        bool isVideoCaptureEnabled = _featureManager.IsEnabled(CaptureToolFeatures.Feature_Capture_Video);
        if (isVideoCaptureEnabled)
        {
            enabledModes.Add(SnippingToolEnabledMode.RecordAllModes);
        }

        return [.. enabledModes];
    }

    public bool IsSnippingToolInstalled()
    {
        return IsAppInstalled("Microsoft.ScreenSketch_8wekyb3d8bbwe");
    }

    private static bool IsAppInstalled(string packageFamilyName)
    {
        var packageManager = new PackageManager();
        var packages = packageManager.FindPackagesForUser(string.Empty);

        foreach (var package in packages)
        {
            if (package.Id.FamilyName.Equals(packageFamilyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
