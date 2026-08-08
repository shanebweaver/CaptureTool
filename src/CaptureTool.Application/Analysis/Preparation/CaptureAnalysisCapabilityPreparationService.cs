using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Domain.Analysis;
using System.Collections.Concurrent;

namespace CaptureTool.Application.Analysis.Preparation;

public sealed class CaptureAnalysisCapabilityPreparationService :
    IAnalysisCapabilityPreparationQueryService,
    IUserInitiatedAnalysisCapabilityPreparationService
{
    private readonly ICaptureAnalyzerResolver _resolver;
    private readonly ICaptureAnalyzerCatalog _catalog;
    private readonly ICaptureAnalysisFeatureAvailability _featureAvailability;
    private readonly ICaptureAnalysisJobStore _jobStore;
    private readonly ICaptureAnalysisWakeSignal _wakeSignal;
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<AnalyzerRevision, byte> _preparing = new();

    public CaptureAnalysisCapabilityPreparationService(
        ICaptureAnalyzerResolver resolver,
        ICaptureAnalyzerCatalog catalog,
        ICaptureAnalysisFeatureAvailability featureAvailability,
        ICaptureAnalysisJobStore jobStore,
        ICaptureAnalysisWakeSignal wakeSignal,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(featureAvailability);
        ArgumentNullException.ThrowIfNull(jobStore);
        ArgumentNullException.ThrowIfNull(wakeSignal);
        ArgumentNullException.ThrowIfNull(clock);
        _resolver = resolver;
        _catalog = catalog;
        _featureAvailability = featureAvailability;
        _jobStore = jobStore;
        _wakeSignal = wakeSignal;
        _clock = clock;
    }

    public async ValueTask<AnalysisCapabilityPreparationState> GetStateAsync(
        AnalysisCapabilityPreparationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CaptureAnalyzerResolution resolution = await ResolveAsync(request, cancellationToken)
            .ConfigureAwait(false);
        return ToPreparationState(resolution);
    }

    public async Task<AnalysisCapabilityPreparationState> PrepareAsync(
        AnalysisCapabilityPreparationRequest request,
        IProgress<AnalysisCapabilityPreparationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        CaptureAnalyzerCandidateEvaluation? candidate = null;
        bool ownsPreparation = false;
        try
        {
            CaptureAnalyzerResolution resolution = await ResolveAsync(request, cancellationToken)
                .ConfigureAwait(false);
            AnalysisCapabilityPreparationState current = ToPreparationState(resolution);
            if (resolution.Status != CaptureAnalyzerResolutionStatus.WaitingForPreparation)
            {
                if (current.Status == AnalysisCapabilityPreparationStatus.Ready &&
                    current.ProcessingBoundary is { } readyBoundary)
                {
                    await ResumeWaitingAndWakeAsync(
                        request.Capability,
                        readyBoundary,
                        cancellationToken).ConfigureAwait(false);
                }

                return current;
            }

            candidate = FindPreparationCandidate(resolution);
            if (candidate == null)
            {
                return InvalidResponseFailure();
            }

            ICaptureAnalyzer? analyzer = _catalog.Find(
                candidate.Descriptor.Revision,
                candidate.Descriptor.Capability);
            if (analyzer is not IPreparableCaptureAnalyzer preparable)
            {
                return InvalidResponseFailure();
            }

            if (!_preparing.TryAdd(candidate.Descriptor.Revision, 0))
            {
                return AnalysisCapabilityPreparationState.Preparing(
                    candidate.Descriptor.Identity,
                    candidate.Descriptor.ProcessingBoundary);
            }

            ownsPreparation = true;
            CaptureAnalyzerPreparationResult result = await preparable
                .PrepareAsync(progress, cancellationToken)
                .ConfigureAwait(false);
            if (result.Status != CaptureAnalyzerPreparationStatus.Succeeded)
            {
                return MapPreparationResult(result);
            }

            CaptureAnalyzerResolution refreshed = await ResolveAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (refreshed.Status == CaptureAnalyzerResolutionStatus.WaitingForPreparation)
            {
                return AnalysisCapabilityPreparationState.Failed(new AnalysisFailure(
                    AnalysisFailureCode.ModelNotReady,
                    AnalysisFailureDisposition.Transient));
            }

            AnalysisCapabilityPreparationState refreshedState = ToPreparationState(refreshed);
            if (refreshedState.Status == AnalysisCapabilityPreparationStatus.Ready)
            {
                await ResumeWaitingAndWakeAsync(
                    request.Capability,
                    refreshedState.ProcessingBoundary!.Value,
                    cancellationToken).ConfigureAwait(false);
                return refreshedState;
            }

            return refreshedState;
        }
        catch (OperationCanceledException)
        {
            return AnalysisCapabilityPreparationState.Cancelled;
        }
        catch
        {
            return AnalysisCapabilityPreparationState.Failed(new AnalysisFailure(
                AnalysisFailureCode.ProviderUnavailable,
                AnalysisFailureDisposition.Transient));
        }
        finally
        {
            if (ownsPreparation && candidate != null)
            {
                _preparing.TryRemove(candidate.Descriptor.Revision, out _);
            }
        }
    }

    private ValueTask<CaptureAnalyzerResolution> ResolveAsync(
        AnalysisCapabilityPreparationRequest request,
        CancellationToken cancellationToken)
    {
        return _resolver.ResolveAsync(
            new CaptureAnalyzerResolutionRequest(
                request.Capability,
                request.MediaKind,
                sourceLength: 0,
                request.Purpose,
                request.ProcessingPolicy,
                _featureAvailability.ResolutionPolicyRevision),
            cancellationToken);
    }

    private AnalysisCapabilityPreparationState ToPreparationState(
        CaptureAnalyzerResolution resolution)
    {
        if (resolution.Status == CaptureAnalyzerResolutionStatus.Resolved &&
            resolution.Analyzer != null)
        {
            return AnalysisCapabilityPreparationState.Ready(
                resolution.Analyzer.Descriptor.Identity,
                resolution.Analyzer.Descriptor.ProcessingBoundary);
        }

        if (resolution.Status == CaptureAnalyzerResolutionStatus.FeatureDisabled)
        {
            return AnalysisCapabilityPreparationState.Disabled;
        }

        if (resolution.Status == CaptureAnalyzerResolutionStatus.WaitingForPreparation)
        {
            CaptureAnalyzerCandidateEvaluation? candidate = FindPreparationCandidate(resolution);
            if (candidate == null)
            {
                return InvalidResponseFailure();
            }

            return _preparing.ContainsKey(candidate.Descriptor.Revision)
                ? AnalysisCapabilityPreparationState.Preparing(
                    candidate.Descriptor.Identity,
                    candidate.Descriptor.ProcessingBoundary)
                : AnalysisCapabilityPreparationState.PreparationRequired(
                    candidate.Descriptor.Identity,
                    candidate.Descriptor.ProcessingBoundary);
        }

        CaptureAnalyzerCandidateEvaluation? disabled = resolution.Candidates.FirstOrDefault(
            evaluation =>
                evaluation.Eligibility is CaptureAnalyzerEligibilityStatus.AnalysisFeatureDisabled or
                    CaptureAnalyzerEligibilityStatus.AnalyzerFeatureDisabled or
                    CaptureAnalyzerEligibilityStatus.ProviderNotAuthorized ||
                evaluation.Availability?.Status == CaptureAnalyzerAvailabilityStatus.Disabled);
        if (disabled != null)
        {
            return AnalysisCapabilityPreparationState.Disabled;
        }

        CaptureAnalyzerAvailability? unsupported = resolution.Candidates
            .Select(candidate => candidate.Availability)
            .FirstOrDefault(availability =>
                availability?.Status == CaptureAnalyzerAvailabilityStatus.Unsupported);
        if (unsupported?.Failure is { } unsupportedFailure)
        {
            return AnalysisCapabilityPreparationState.Unsupported(unsupportedFailure);
        }

        CaptureAnalyzerAvailability? unavailable = resolution.Candidates
            .Select(candidate => candidate.Availability)
            .FirstOrDefault(availability =>
                availability?.Status == CaptureAnalyzerAvailabilityStatus.TemporarilyUnavailable);
        if (unavailable?.Failure is { } unavailableFailure)
        {
            return AnalysisCapabilityPreparationState.Failed(unavailableFailure);
        }

        return AnalysisCapabilityPreparationState.Unsupported(new AnalysisFailure(
            AnalysisFailureCode.CapabilityUnavailable,
            AnalysisFailureDisposition.Terminal));
    }

    private static CaptureAnalyzerCandidateEvaluation? FindPreparationCandidate(
        CaptureAnalyzerResolution resolution)
    {
        return resolution.Candidates.FirstOrDefault(candidate =>
            candidate.Eligibility == CaptureAnalyzerEligibilityStatus.PreparationRequired &&
            candidate.Availability?.Status == CaptureAnalyzerAvailabilityStatus.PreparationRequired);
    }

    private static AnalysisCapabilityPreparationState MapPreparationResult(
        CaptureAnalyzerPreparationResult result)
    {
        return result.Status switch
        {
            CaptureAnalyzerPreparationStatus.Unsupported when result.Failure is { } failure =>
                AnalysisCapabilityPreparationState.Unsupported(failure),
            CaptureAnalyzerPreparationStatus.Disabled => AnalysisCapabilityPreparationState.Disabled,
            CaptureAnalyzerPreparationStatus.Cancelled => AnalysisCapabilityPreparationState.Cancelled,
            CaptureAnalyzerPreparationStatus.Failed when result.Failure is { } failure =>
                AnalysisCapabilityPreparationState.Failed(failure),
            _ => InvalidResponseFailure(),
        };
    }

    private static AnalysisCapabilityPreparationState InvalidResponseFailure()
    {
        return AnalysisCapabilityPreparationState.Failed(new AnalysisFailure(
            AnalysisFailureCode.InvalidResponse,
            AnalysisFailureDisposition.Terminal));
    }

    private async ValueTask ResumeWaitingAndWakeAsync(
        CapabilityDefinition capability,
        ProcessingBoundary processingBoundary,
        CancellationToken cancellationToken)
    {
        DateTime utcNow = DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc);
        _ = await _jobStore.ResumeWaitingForCapabilityAsync(
            capability,
            processingBoundary,
            new DateTimeOffset(utcNow),
            cancellationToken).ConfigureAwait(false);
        _ = _wakeSignal.TrySignal();
    }
}
