using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Analysis.Analyzers;

public sealed class CaptureAnalyzerResolver : ICaptureAnalyzerResolver
{
    private readonly CaptureAnalyzerCatalog _catalog;
    private readonly ICaptureAnalysisFeatureAvailability _featureAvailability;
    private readonly ICaptureAnalyzerResolutionPreference _preference;

    public CaptureAnalyzerResolver(
        CaptureAnalyzerCatalog catalog,
        ICaptureAnalysisFeatureAvailability featureAvailability,
        ICaptureAnalyzerResolutionPreference preference)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(featureAvailability);
        ArgumentNullException.ThrowIfNull(preference);
        _catalog = catalog;
        _featureAvailability = featureAvailability;
        _preference = preference;
    }

    public async ValueTask<CaptureAnalyzerResolution> ResolveAsync(
        CaptureAnalyzerResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ICaptureAnalyzer[] candidates =
        [
            .. _catalog.Analyzers
                .OrderByDescending(analyzer => _preference.GetPreference(analyzer.Descriptor))
                .ThenByDescending(analyzer => analyzer.Descriptor.QualityTier)
                .ThenBy(analyzer => analyzer.Descriptor.ProcessingBoundary == ProcessingBoundary.OnDevice ? 0 : 1)
                .ThenBy(analyzer => analyzer.Descriptor.WorkloadClass)
                .ThenBy(analyzer => analyzer.Descriptor.Identity.AnalyzerId, StringComparer.Ordinal)
                .ThenBy(analyzer => analyzer.Descriptor.Revision.Value, StringComparer.Ordinal),
        ];

        var evaluations = new List<CaptureAnalyzerCandidateEvaluation>(candidates.Length);
        if (!_featureAvailability.IsCaptureAnalysisEnabled)
        {
            evaluations.AddRange(candidates.Select(analyzer => new CaptureAnalyzerCandidateEvaluation(
                analyzer.Descriptor,
                CaptureAnalyzerEligibilityStatus.AnalysisFeatureDisabled)));
            return CaptureAnalyzerResolution.FeatureDisabled(evaluations);
        }

        foreach (ICaptureAnalyzer analyzer in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CaptureAnalyzerDescriptor descriptor = analyzer.Descriptor;
            CaptureAnalyzerEligibilityStatus? filtered = GetFilteredEligibility(descriptor, request);
            if (filtered.HasValue)
            {
                evaluations.Add(new(descriptor, filtered.Value));
                continue;
            }

            var availabilityRequest = new CaptureAnalyzerAvailabilityRequest(
                descriptor,
                request.MediaKind,
                request.SourceLength,
                request.Purpose,
                request.ProcessingPolicy);
            CaptureAnalyzerAvailability availability;
            try
            {
                availability = await analyzer
                    .GetAvailabilityAsync(availabilityRequest, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                availability = CaptureAnalyzerAvailability.TemporarilyUnavailable(
                    new AnalysisFailure(
                        AnalysisFailureCode.ProviderUnavailable,
                        AnalysisFailureDisposition.Transient));
            }
            CaptureAnalyzerEligibilityStatus eligibility = availability.Status switch
            {
                CaptureAnalyzerAvailabilityStatus.Available => CaptureAnalyzerEligibilityStatus.Eligible,
                CaptureAnalyzerAvailabilityStatus.PreparationRequired =>
                    CaptureAnalyzerEligibilityStatus.PreparationRequired,
                _ => CaptureAnalyzerEligibilityStatus.Unavailable,
            };
            evaluations.Add(new(descriptor, eligibility, availability));
            if (eligibility == CaptureAnalyzerEligibilityStatus.Eligible)
            {
                return CaptureAnalyzerResolution.Resolved(analyzer, evaluations);
            }
        }

        return evaluations.Any(evaluation =>
            evaluation.Eligibility == CaptureAnalyzerEligibilityStatus.PreparationRequired)
                ? CaptureAnalyzerResolution.WaitingForPreparation(evaluations)
                : CaptureAnalyzerResolution.NoEligibleAnalyzer(evaluations);
    }

    private CaptureAnalyzerEligibilityStatus? GetFilteredEligibility(
        CaptureAnalyzerDescriptor descriptor,
        CaptureAnalyzerResolutionRequest request)
    {
        if (!_featureAvailability.IsProviderEnabled(descriptor.Identity.ProviderId))
        {
            return CaptureAnalyzerEligibilityStatus.ProviderNotAuthorized;
        }

        if (!_featureAvailability.IsAnalyzerEnabled(descriptor.Identity))
        {
            return CaptureAnalyzerEligibilityStatus.AnalyzerFeatureDisabled;
        }

        if (descriptor.Capability != request.Capability)
        {
            return CaptureAnalyzerEligibilityStatus.UnsupportedCapability;
        }

        if (!descriptor.SupportedMediaKinds.Contains(request.MediaKind))
        {
            return CaptureAnalyzerEligibilityStatus.UnsupportedMediaKind;
        }

        if (descriptor.MaximumSourceBytes is { } maximumSourceBytes &&
            request.SourceLength > maximumSourceBytes)
        {
            return CaptureAnalyzerEligibilityStatus.Unavailable;
        }

        if (request.ProcessingPolicy.AuthorizedPurpose != request.Purpose)
        {
            return CaptureAnalyzerEligibilityStatus.PurposeNotAuthorized;
        }

        if (!request.ProcessingPolicy.AllowedBoundaries.Contains(descriptor.ProcessingBoundary))
        {
            return CaptureAnalyzerEligibilityStatus.BoundaryNotAuthorized;
        }

        if (descriptor.ProcessingBoundary == ProcessingBoundary.Remote &&
            !request.ProcessingPolicy.AllowedRemoteProviderIds.Contains(
                descriptor.Identity.ProviderId,
                StringComparer.Ordinal))
        {
            return CaptureAnalyzerEligibilityStatus.ProviderNotAuthorized;
        }

        if (request.AttemptedAnalyzers.Contains(descriptor.Revision))
        {
            return CaptureAnalyzerEligibilityStatus.Unavailable;
        }

        return null;
    }
}
