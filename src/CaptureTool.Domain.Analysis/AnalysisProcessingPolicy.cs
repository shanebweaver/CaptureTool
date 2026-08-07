namespace CaptureTool.Domain.Analysis;

public sealed class AnalysisProcessingPolicy
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

        ProcessingBoundary[] boundaries = [.. allowedBoundaries.Distinct()];
        if (boundaries.Length == 0 || boundaries.Any(boundary => !Enum.IsDefined(boundary) || boundary == ProcessingBoundary.Unknown))
        {
            throw new ArgumentException("At least one valid processing boundary must be allowed.", nameof(allowedBoundaries));
        }

        AuthorizedPurpose = authorizedPurpose;
        _allowedBoundaries = Array.AsReadOnly(boundaries);
        _allowedRemoteProviderIds = Array.AsReadOnly(
            [.. (allowedRemoteProviderIds ?? []).Select(NormalizeProviderId).Distinct(StringComparer.Ordinal)]);
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

    private static string NormalizeProviderId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return BoundedIdentifier.Normalize(value, nameof(value));
    }
}
