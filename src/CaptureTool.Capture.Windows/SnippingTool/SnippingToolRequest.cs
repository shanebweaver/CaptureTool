namespace CaptureTool.Capture.Windows.SnippingTool;

public sealed partial class SnippingToolRequest
{
    public SnippingToolQuery Query { get; }

    private SnippingToolRequest(SnippingToolQuery query)
    {
        Query = query;
    }

    public static SnippingToolRequest DiscoverSupport(string redirectUri)
    {
        SnippingToolDiscoverQuery query = new(redirectUri);
        return new(query);
    }

    public static SnippingToolRequest CaptureImage(SnippingToolCaptureMode defaultMode, SnippingToolEnabledMode[] enabledModes, string redirectUri)
    {
        SnippingToolCaptureQuery query = new(SnippingToolPath.Image, defaultMode, enabledModes, redirectUri);
        return new SnippingToolRequest(query);
    }

    public static SnippingToolRequest CaptureVideo(SnippingToolCaptureMode defaultMode, SnippingToolEnabledMode[] enabledModes, string redirectUri)
    {
        SnippingToolCaptureQuery query = new(SnippingToolPath.Video, defaultMode, enabledModes, redirectUri);
        return new SnippingToolRequest(query);
    }

    public override string ToString()
    {
        return $"ms-screenclip://{Query}";
    }
}
