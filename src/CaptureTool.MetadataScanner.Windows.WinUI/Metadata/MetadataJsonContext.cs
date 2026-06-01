using System.Text.Json.Serialization;

namespace CaptureTool.MetadataScanner.Windows.WinUI.Metadata;

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
