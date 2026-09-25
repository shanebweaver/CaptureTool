namespace CaptureTool.Domain.Analysis.Payloads;

public enum StructuredFactKind
{
    // Persisted values; additions must not renumber existing kinds.
    Url = 0,
    EmailAddress = 1,
    Date = 2,
    CurrencyAmount = 3,
    ErrorCode = 4,
    ReferenceCode = 5,
}

/// <summary>A literal observation, preserving source spelling rather than an inferred or normalized value.</summary>
public sealed class StructuredFact
{
    public StructuredFactKind Kind { get; }
    public string Value { get; }
    public IReadOnlyList<AnalysisEvidence> Evidence { get; }

    public StructuredFact(StructuredFactKind kind, string value, IEnumerable<AnalysisEvidence> evidence)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 4096) throw new ArgumentException("Fact value is too long.", nameof(value));
        Evidence = AnalysisGuard.Freeze(evidence);
        if (Evidence.Count is < 1 or > 16 || Evidence.Distinct().Count() != Evidence.Count)
            throw new ArgumentException("Specify between one and sixteen distinct evidence spans.", nameof(evidence));
        Kind = kind;
        Value = value;
    }
}

public sealed class StructuredFactsMetadata : DerivedAnalysisPayload
{
    public override AnalysisCapability Capability => AnalysisCapability.StructuredFacts;
    public IReadOnlyList<StructuredFact> Facts { get; }

    public StructuredFactsMetadata(IEnumerable<StructuredFact> facts)
    {
        Facts = AnalysisGuard.Freeze(facts);
        if (Facts.Count > 256 || Facts.Select(fact => (fact.Kind, fact.Value)).Distinct().Count() != Facts.Count)
            throw new ArgumentException("Specify at most 256 distinct facts.", nameof(facts));
    }

    public override bool Supports(AnalysisMediaKind mediaKind) => Enum.IsDefined(mediaKind);

    public override void ValidateEvidence(IReadOnlyList<AnalysisResult> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        foreach (StructuredFact fact in Facts)
        foreach (AnalysisEvidence evidence in fact.Evidence)
        {
            AnalysisResult? source = inputs.SingleOrDefault(input => input.ResultId == evidence.ResultId);
            if (source?.Payload is not (TextRecognitionMetadata or QrCodeMetadata or TranscriptMetadata) ||
                !string.Equals(fact.Value, evidence.ResolveText(source), StringComparison.Ordinal))
                throw new ArgumentException("Observed facts require matching evidence in OCR, QR values, or transcripts.", nameof(inputs));
        }
    }
}
