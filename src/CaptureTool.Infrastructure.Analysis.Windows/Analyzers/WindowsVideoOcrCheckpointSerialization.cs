using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.Analysis.Windows.Analyzers;

internal sealed class WindowsVideoOcrCheckpointDocument
{
    public int SchemaVersion { get; set; }

    public string AdapterVersion { get; set; } = string.Empty;

    public long NextSampleTicks { get; set; }

    public List<WindowsVideoOcrCheckpointObservationDocument> Observations { get; set; } = [];

    public string? ActiveText { get; set; }

    public long ActiveStartTicks { get; set; }

    public long ActiveEndTicks { get; set; }
}

internal sealed class WindowsVideoOcrCheckpointObservationDocument
{
    public string Text { get; set; } = string.Empty;

    public long StartTicks { get; set; }

    public long EndTicks { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[JsonSerializable(typeof(WindowsVideoOcrCheckpointDocument))]
internal sealed partial class WindowsVideoOcrCheckpointJsonContext : JsonSerializerContext;
