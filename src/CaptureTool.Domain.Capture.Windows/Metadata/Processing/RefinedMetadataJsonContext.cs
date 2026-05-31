using System.Text.Json.Serialization;

namespace CaptureTool.Domain.Capture.Windows.Metadata.Processing;

[JsonSerializable(typeof(RefinedMetadataFileDto))]
[JsonSerializable(typeof(InsightEntryDto))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(List<InsightEntryDto>))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class RefinedMetadataJsonContext : JsonSerializerContext
{
}
