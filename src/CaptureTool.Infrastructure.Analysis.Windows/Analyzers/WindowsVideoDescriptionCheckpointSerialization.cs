using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.Analysis.Windows.Analyzers;

internal sealed class WindowsVideoDescriptionCheckpointDocument
{
    public int SchemaVersion { get; set; }

    public string AdapterVersion { get; set; } = string.Empty;

    public long NextSampleTicks { get; set; }

    public List<WindowsVideoDescriptionCheckpointObservationDocument> Observations { get; set; } = [];
}

internal sealed class WindowsVideoDescriptionCheckpointObservationDocument
{
    public string Description { get; set; } = string.Empty;

    public long StartTicks { get; set; }

    public long EndTicks { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WindowsVideoDescriptionCheckpointDocument))]
internal sealed partial class WindowsVideoDescriptionCheckpointJsonContext : JsonSerializerContext;
