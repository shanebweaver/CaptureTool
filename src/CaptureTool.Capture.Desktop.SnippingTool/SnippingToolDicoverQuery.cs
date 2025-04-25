using System;

namespace CaptureTool.Capture.Desktop.SnippingTool;

public class SnippingToolDicoverQuery : SnippingToolQuery
{
    public override SnippingToolHost Host => SnippingToolHost.Discover;

    public SnippingToolDicoverQuery(string redirectUri)
        : base(redirectUri)
    {
    }

    public override string ToString()
    {
        string? hostName = Enum.GetName(Host);
        ArgumentNullException.ThrowIfNull(hostName);

        return $"{hostName}?{base.ToString()}";
    }
}
