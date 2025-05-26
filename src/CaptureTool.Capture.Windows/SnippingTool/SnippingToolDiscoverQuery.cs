using System;

namespace CaptureTool.Capture.Windows.SnippingTool;

public class SnippingToolDiscoverQuery : SnippingToolQuery
{
    public override SnippingToolHost Host => SnippingToolHost.Discover;

    public SnippingToolDiscoverQuery(string redirectUri)
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
