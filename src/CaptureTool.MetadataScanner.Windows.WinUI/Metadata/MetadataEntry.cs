namespace CaptureTool.MetadataScanner.Windows.WinUI.Metadata;

public sealed class MetadataEntry(
    long timestamp,
    string scannerId,
    string key,
    object? value,
    IReadOnlyDictionary<string, object?>? additionalData = null)
{
    public long Timestamp { get; } = timestamp;
    public string ScannerId { get; } = scannerId ?? throw new ArgumentNullException(nameof(scannerId));
    public string Key { get; } = key ?? throw new ArgumentNullException(nameof(key));
    public object? Value { get; } = value;
    public IReadOnlyDictionary<string, object?>? AdditionalData { get; } = additionalData;
}
