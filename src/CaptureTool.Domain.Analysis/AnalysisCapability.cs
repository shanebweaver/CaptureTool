namespace CaptureTool.Domain.Analysis;

public sealed record AnalysisCapability
{
    public string Name { get; }
    public int SchemaVersion { get; }

    public AnalysisCapability(string name, int schemaVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 80 || name.Any(c => !(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')))
            throw new ArgumentException("Use a bounded lowercase capability name.", nameof(name));
        if (schemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        Name = name;
        SchemaVersion = schemaVersion;
    }

    public static AnalysisCapability FileDetails { get; } = new("file-details", 1);
    public static AnalysisCapability TextRecognition { get; } = new("text-recognition", 1);
    public static AnalysisCapability QrCodeDetection { get; } = new("qr-code-detection", 1);
    public static AnalysisCapability Description { get; } = new("description", 1);
    public static AnalysisCapability Transcription { get; } = new("transcription", 1);
    public override string ToString() => $"{Name}/v{SchemaVersion}";
}
