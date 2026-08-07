namespace CaptureTool.Domain.Analysis;

public readonly record struct ProvisionalSourceStamp
{
    public ProvisionalSourceStamp(long length, DateTimeOffset lastWriteTimeUtc)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "A source length cannot be negative.");
        }

        EnsureUtc(lastWriteTimeUtc, nameof(lastWriteTimeUtc));

        Length = length;
        LastWriteTimeUtc = lastWriteTimeUtc;
        IsKnown = true;
    }

    public static ProvisionalSourceStamp Unknown => default;

    public bool IsKnown { get; }

    public long Length { get; }

    public DateTimeOffset LastWriteTimeUtc { get; }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A source timestamp must be expressed in UTC.", parameterName);
        }
    }
}

public readonly record struct ContentFingerprint
{
    public const string Sha256Algorithm = "sha256";

    public ContentFingerprint(string algorithm, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string normalizedAlgorithm = algorithm.Trim().ToLowerInvariant();
        string normalizedValue = value.Trim().ToLowerInvariant();
        if (normalizedAlgorithm != Sha256Algorithm)
        {
            throw new ArgumentException("Only SHA-256 source fingerprints are supported.", nameof(algorithm));
        }

        if (normalizedValue.Length != 64 || !normalizedValue.All(IsLowerHexCharacter))
        {
            throw new ArgumentException("A SHA-256 fingerprint must contain 64 hexadecimal characters.", nameof(value));
        }

        Algorithm = normalizedAlgorithm;
        Value = normalizedValue;
    }

    public string Algorithm { get; }

    public string Value { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public static ContentFingerprint Sha256(string value)
    {
        return new(Sha256Algorithm, value);
    }

    public override string ToString()
    {
        return $"{Algorithm}:{Value}";
    }

    private static bool IsLowerHexCharacter(char value)
    {
        return value is >= '0' and <= '9' or >= 'a' and <= 'f';
    }
}

public readonly record struct SourceRevision
{
    public SourceRevision(long length, DateTimeOffset lastWriteTimeUtc, ContentFingerprint fingerprint)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "A source length cannot be negative.");
        }

        if (lastWriteTimeUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A source timestamp must be expressed in UTC.", nameof(lastWriteTimeUtc));
        }

        if (fingerprint.IsEmpty)
        {
            throw new ArgumentException("A verified source revision requires a fingerprint.", nameof(fingerprint));
        }

        Length = length;
        LastWriteTimeUtc = lastWriteTimeUtc;
        Fingerprint = fingerprint;
    }

    public long Length { get; }

    public DateTimeOffset LastWriteTimeUtc { get; }

    public ContentFingerprint Fingerprint { get; }

    public ProvisionalSourceStamp ProvisionalStamp => new(Length, LastWriteTimeUtc);

    public bool IsEmpty => Fingerprint.IsEmpty;

    public bool HasSameBytesAs(SourceRevision other)
    {
        return Length == other.Length && Fingerprint == other.Fingerprint;
    }

    public bool Matches(ProvisionalSourceStamp stamp)
    {
        return stamp.IsKnown && Length == stamp.Length && LastWriteTimeUtc == stamp.LastWriteTimeUtc;
    }
}
