namespace CaptureTool.Capture.Desktop.SnippingTool;

public abstract class SnippingToolQuery
{
    public string RedirectUri { get; set; }
    public abstract SnippingToolHost Host { get; }

    public SnippingToolQuery(string redirectUri)
    {
        RedirectUri = redirectUri;
    }

    public override string ToString()
    {
        return $"redirect-uri={RedirectUri}";
    }
}
