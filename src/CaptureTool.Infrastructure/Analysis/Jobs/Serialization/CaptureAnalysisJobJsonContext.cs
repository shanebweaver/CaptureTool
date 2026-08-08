using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.Analysis.Jobs.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[JsonSerializable(typeof(CaptureAnalysisJobDocument))]
internal sealed partial class CaptureAnalysisJobJsonContext : JsonSerializerContext;
