using System.Linq;
using System.Text.Json.Serialization;
using Windows.Foundation.Collections;

namespace CaptureTool.UI.Windows.Activation;

public sealed class DiscoverResponse
{
    [JsonPropertyName("version")]
    public float Version { get; set; }

    [JsonPropertyName("capabilities")]
    public DiscoverResponseCapability[] Capabilities { get; set; }

    [JsonConstructor]
    public DiscoverResponse(float version, DiscoverResponseCapability[] capabilities)
    {
        Version = version;
        Capabilities = capabilities;
    }

    public ValueSet ToValueSet()
    {
        return new() {
            { "version", Version },
            { "capabilities", Capabilities.Select(c => c.ToValueSet())},
        };
    }
}
