using System.Text.Json.Serialization;

namespace CaptureTool.Domain.Capture.Implementations.Windows.Metadata.Grooming;

/// <summary>
/// JSON serialization context for refined metadata files (AOT-compatible).
/// </summary>
[JsonSerializable(typeof(RefinedMetadataFileDto))]
[JsonSerializable(typeof(InsightEntryDto))]
[JsonSerializable(typeof(List<InsightEntryDto>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class RefinedMetadataJsonContext : JsonSerializerContext
{
}
