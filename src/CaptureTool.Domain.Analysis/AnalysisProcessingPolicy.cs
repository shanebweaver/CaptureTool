namespace CaptureTool.Domain.Analysis;

public sealed class AnalysisProcessingPolicy : IEquatable<AnalysisProcessingPolicy>
{
    public AnalysisProcessingPolicy(
        AnalysisPurpose authorizedPurpose,
        IEnumerable<ProcessingBoundary> allowedBoundaries,
        IEnumerable<string>? allowedRemoteProviderIds = null)
    {
        if (authorizedPurpose.IsEmpty)
        {
            throw new ArgumentException("An analysis policy requires an authorized purpose.", nameof(authorizedPurpose));
        }

        ArgumentNullException.ThrowIfNull(allowedBoundaries);

        ProcessingBoundary[] boundaries =
        [
            .. allowedBoundaries
                .Distinct()
                .OrderBy(boundary => boundary),
        ];
        if (boundaries.Length == 0 || boundaries.Any(boundary => !Enum.IsDefined(boundary) || boundary == ProcessingBoundary.Unknown))
        {
            throw new ArgumentException("At least one valid processing boundary must be allowed.", nameof(allowedBoundaries));
        }

        string[] remoteProviderIds =
        [
            .. (allowedRemoteProviderIds ?? [])
                .Select(NormalizeProviderId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(providerId => providerId, StringComparer.Ordinal),
        ];
        bool permitsRemoteProcessing = boundaries.Contains(ProcessingBoundary.Remote);
        if (permitsRemoteProcessing != (remoteProviderIds.Length > 0))
        {
            throw new ArgumentException(
                permitsRemoteProcessing
                    ? "A remote processing boundary requires at least one authorized provider."
                    : "Remote provider IDs cannot be authorized without a remote processing boundary.",
                nameof(allowedRemoteProviderIds));
        }

        AuthorizedPurpose = authorizedPurpose;
        _allowedBoundaries = Array.AsReadOnly(boundaries);
        _allowedRemoteProviderIds = Array.AsReadOnly(remoteProviderIds);
    }

    private readonly IReadOnlyList<ProcessingBoundary> _allowedBoundaries;
    private readonly IReadOnlyList<string> _allowedRemoteProviderIds;

    public AnalysisPurpose AuthorizedPurpose { get; }

    public static AnalysisProcessingPolicy LocalOnly(AnalysisPurpose purpose)
    {
        return new(purpose, [ProcessingBoundary.OnDevice]);
    }

    public IReadOnlyList<ProcessingBoundary> AllowedBoundaries => _allowedBoundaries;

    public IReadOnlyList<string> AllowedRemoteProviderIds => _allowedRemoteProviderIds;

    public bool IsEligible(
        AnalyzerIdentity analyzer,
        ProcessingBoundary boundary,
        AnalysisPurpose requestedPurpose)
    {
        ArgumentNullException.ThrowIfNull(analyzer);

        if (requestedPurpose != AuthorizedPurpose || !_allowedBoundaries.Contains(boundary))
        {
            return false;
        }

        return boundary != ProcessingBoundary.Remote ||
            _allowedRemoteProviderIds.Contains(analyzer.ProviderId, StringComparer.Ordinal);
    }

    public bool IsEquivalentTo(AnalysisProcessingPolicy? other)
    {
        return Equals(other);
    }

    public bool Equals(AnalysisProcessingPolicy? other)
    {
        return ReferenceEquals(this, other) ||
            other != null &&
            AuthorizedPurpose == other.AuthorizedPurpose &&
            _allowedBoundaries.SequenceEqual(other._allowedBoundaries) &&
            _allowedRemoteProviderIds.SequenceEqual(
                other._allowedRemoteProviderIds,
                StringComparer.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is AnalysisProcessingPolicy other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(AuthorizedPurpose);
        foreach (ProcessingBoundary boundary in _allowedBoundaries)
        {
            hash.Add(boundary);
        }

        foreach (string providerId in _allowedRemoteProviderIds)
        {
            hash.Add(providerId, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    private static string NormalizeProviderId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return BoundedIdentifier.Normalize(value, nameof(value));
    }
}
