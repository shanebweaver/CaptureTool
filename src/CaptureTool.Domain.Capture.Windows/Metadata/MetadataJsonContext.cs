using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.Domain.Capture.Windows.Metadata.Scanners;
using System.Text.Json.Serialization;

namespace CaptureTool.Domain.Capture.Windows.Metadata;

/// <summary>
/// JSON serialization context for metadata files (AOT-compatible).
/// </summary>
[JsonSerializable(typeof(MetadataFileDto))]
[JsonSerializable(typeof(MetadataEntryDto))]
[JsonSerializable(typeof(ScanJobRequest))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(List<MetadataEntryDto>))]
[JsonSerializable(typeof(ObjectDetectionMetadataDto))]
[JsonSerializable(typeof(ObjectDetectionBoxMetadataDto))]
[JsonSerializable(typeof(List<ObjectDetectionMetadataDto>))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class MetadataJsonContext : JsonSerializerContext
{
}
