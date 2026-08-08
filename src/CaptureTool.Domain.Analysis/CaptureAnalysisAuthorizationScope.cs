namespace CaptureTool.Domain.Analysis;

public sealed class CaptureAnalysisAuthorizationScope : IEquatable<CaptureAnalysisAuthorizationScope>
{
    private readonly IReadOnlyList<CapabilityDefinition> _capabilities;

    public CaptureAnalysisAuthorizationScope(
        AnalysisPurpose purpose,
        AnalysisProcessingPolicy processingPolicy,
        IEnumerable<CapabilityDefinition> capabilities)
    {
        if (purpose.IsEmpty)
        {
            throw new ArgumentException("An authorization scope requires a purpose.", nameof(purpose));
        }

        ArgumentNullException.ThrowIfNull(processingPolicy);
        if (processingPolicy.AuthorizedPurpose != purpose)
        {
            throw new ArgumentException(
                "The processing policy must authorize the scoped purpose.",
                nameof(processingPolicy));
        }

        ArgumentNullException.ThrowIfNull(capabilities);
        CapabilityDefinition[] copiedCapabilities =
        [
            .. capabilities.OrderBy(
                capability => capability.Id.Value,
                StringComparer.Ordinal),
        ];
        if (copiedCapabilities.Length == 0 ||
            copiedCapabilities.Any(capability => capability.Id.IsEmpty) ||
            copiedCapabilities.Select(capability => capability.Id).Distinct().Count() !=
                copiedCapabilities.Length)
        {
            throw new ArgumentException(
                "An authorization scope requires distinct, known capability IDs.",
                nameof(capabilities));
        }

        Purpose = purpose;
        ProcessingPolicy = processingPolicy;
        _capabilities = Array.AsReadOnly(copiedCapabilities);
    }

    public AnalysisPurpose Purpose { get; }

    public AnalysisProcessingPolicy ProcessingPolicy { get; }

    public IReadOnlyList<CapabilityDefinition> Capabilities => _capabilities;

    public bool Allows(CapabilityDefinition capability)
    {
        return !capability.Id.IsEmpty && _capabilities.Contains(capability);
    }

    public bool IsEquivalentTo(CaptureAnalysisAuthorizationScope? other)
    {
        return Equals(other);
    }

    public bool Equals(CaptureAnalysisAuthorizationScope? other)
    {
        return ReferenceEquals(this, other) ||
            other != null &&
            Purpose == other.Purpose &&
            ProcessingPolicy.Equals(other.ProcessingPolicy) &&
            _capabilities.SequenceEqual(other._capabilities);
    }

    public override bool Equals(object? obj)
    {
        return obj is CaptureAnalysisAuthorizationScope other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Purpose);
        hash.Add(ProcessingPolicy);
        foreach (CapabilityDefinition capability in _capabilities)
        {
            hash.Add(capability);
        }

        return hash.ToHashCode();
    }
}
