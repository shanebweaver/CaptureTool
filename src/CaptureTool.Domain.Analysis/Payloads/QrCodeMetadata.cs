namespace CaptureTool.Domain.Analysis.Payloads;

public sealed record DecodedQrCode
{
    public string Value { get; }
    public NormalizedBounds Bounds { get; }
    public TimeSpan? Timestamp { get; }

    public DecodedQrCode(string value, NormalizedBounds bounds, TimeSpan? timestamp = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentNullException.ThrowIfNull(bounds);
        if (bounds.Width <= 0 || bounds.Height <= 0) throw new ArgumentOutOfRangeException(nameof(bounds));
        if (timestamp < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timestamp));
        Value = value;
        Bounds = bounds;
        Timestamp = timestamp;
    }
}

/// <summary>Decoded values are untrusted data; storing a code never opens or executes it.</summary>
public sealed class QrCodeMetadata(IEnumerable<DecodedQrCode> codes) : AnalysisPayload
{
    public override AnalysisCapability Capability => AnalysisCapability.QrCodeDetection;
    public IReadOnlyList<DecodedQrCode> Codes { get; } = AnalysisGuard.Freeze(codes);
    public override bool Supports(AnalysisMediaKind mediaKind) => mediaKind is AnalysisMediaKind.Image or AnalysisMediaKind.Video;
}
