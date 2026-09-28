namespace CaptureTool.Domain.Analysis.Payloads;

public sealed record RecognizedText
{
    public string Text { get; }
    public NormalizedBounds? Bounds { get; }
    public TimeSpan? Timestamp { get; }
    public int? LineIndex { get; }
    public int? WordIndex { get; }

    public RecognizedText(string text, NormalizedBounds? bounds = null, TimeSpan? timestamp = null,
        int? lineIndex = null, int? wordIndex = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (timestamp < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timestamp));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(lineIndex ?? 0);
        ArgumentOutOfRangeException.ThrowIfNegative(wordIndex ?? 0);

        Text = text;
        Bounds = bounds;
        Timestamp = timestamp;
        LineIndex = lineIndex;
        WordIndex = wordIndex;
    }
}
