namespace CaptureTool.Domain.Analysis.Payloads;

public sealed record NormalizedBounds
{
    public double X { get; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }

    public NormalizedBounds(double x, double y, double width, double height)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(width) || !double.IsFinite(height) ||
            x < 0 || y < 0 || width < 0 || height < 0 || x + width > 1 || y + height > 1)
            throw new ArgumentOutOfRangeException(nameof(x), "Bounds must lie within the normalized image.");
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}

public sealed record RecognizedText
{
    public string Text { get; }
    public NormalizedBounds? Bounds { get; }
    public TimeSpan? Timestamp { get; }

    public RecognizedText(string text, NormalizedBounds? bounds = null, TimeSpan? timestamp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (timestamp < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timestamp));
        Text = text;
        Bounds = bounds;
        Timestamp = timestamp;
    }
}

public sealed class TextRecognitionMetadata : AnalysisPayload
{
    public override AnalysisCapability Capability => AnalysisCapability.TextRecognition;
    public IReadOnlyList<RecognizedText> Regions { get; }

    public TextRecognitionMetadata(IEnumerable<RecognizedText> regions) => Regions = AnalysisGuard.Freeze(regions);
    public override bool Supports(AnalysisMediaKind mediaKind) => mediaKind is AnalysisMediaKind.Image or AnalysisMediaKind.Video;
}
