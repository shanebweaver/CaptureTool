namespace CaptureTool.Domain.Analysis;

/// <summary>SHA-256 of the exact source bytes. Computing/verifying it belongs to infrastructure.</summary>
public sealed record SourceRevision
{
    public string Sha256 { get; }

    public SourceRevision(string sha256)
    {
        ArgumentNullException.ThrowIfNull(sha256);
        if (sha256.Length != 64 || sha256.Any(c => !char.IsAsciiHexDigit(c)))
            throw new ArgumentException("A source revision must be a SHA-256 digest.", nameof(sha256));
        Sha256 = sha256.ToLowerInvariant();
    }
}
