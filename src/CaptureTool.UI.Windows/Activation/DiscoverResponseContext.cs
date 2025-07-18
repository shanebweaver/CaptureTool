using System.Text.Json.Serialization;

namespace CaptureTool.UI.Windows.Activation;

[JsonSerializable(typeof(DiscoverResponse))]
internal sealed partial class DiscoverResponseContext : JsonSerializerContext { }
