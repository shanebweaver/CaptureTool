using System.Text.Json.Serialization;

namespace CaptureTool.MetadataScanner.Windows.WinUI.Metadata;

public sealed class MetadataFileDto
{
    [JsonPropertyName("sourceFilePath")]
    public string SourceFilePath { get; set; } = string.Empty;

    [JsonPropertyName("scanTimestamp")]
    public DateTime ScanTimestamp { get; set; }

    [JsonPropertyName("scannerInfo")]
    public Dictionary<string, string> ScannerInfo { get; set; } = new();

    [JsonPropertyName("entries")]
    public List<MetadataEntryDto> Entries { get; set; } = new();
}

public sealed class MetadataEntryDto
{
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("scannerId")]
    public string ScannerId { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("additionalData")]
    public Dictionary<string, string>? AdditionalData { get; set; }
}
