using System.Text.Json.Serialization;
using CaptureTool.Domains.Capture.Interfaces.Metadata;

namespace CaptureTool.Domains.Capture.Implementations.Windows.Metadata;

/// <summary>
/// JSON serialization context for metadata files (AOT-compatible).
/// </summary>
[JsonSerializable(typeof(MetadataFileDto))]
[JsonSerializable(typeof(MetadataEntryDto))]
[JsonSerializable(typeof(ScanJobRequest))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(List<MetadataEntryDto>))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class MetadataJsonContext : JsonSerializerContext
{
}
