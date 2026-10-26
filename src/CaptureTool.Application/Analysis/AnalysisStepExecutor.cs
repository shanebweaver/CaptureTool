using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Application.Analysis;

/// <summary>Runs a step's ordered providers. It does not authorize or publish results.</summary>
internal sealed class AnalysisStepExecutor
{
    private static readonly TimeSpan AvailabilityTimeout = TimeSpan.FromSeconds(10);
    private readonly IReadOnlyDictionary<string, IMediaAnalyzer> _analyzers;
    private readonly IReadOnlyDictionary<string, IMetadataProcessor> _processors;
    private readonly AnalysisProviderSession _session;
    private readonly ILogService? _log;

    public AnalysisStepExecutor(IEnumerable<IMediaAnalyzer> analyzers, IEnumerable<IMetadataProcessor> processors,
        AnalysisProviderSession session, ILogService? log)
    {
        _analyzers = analyzers.ToDictionary(analyzer => analyzer.Descriptor.Id, StringComparer.Ordinal);
        _processors = processors.ToDictionary(processor => processor.Descriptor.Id, StringComparer.Ordinal);
        _session = session;
        _log = log;
    }

    public bool RequiresMetadata(AnalysisStep step) => _processors.ContainsKey(step.Candidates[0]);

    public async Task<AnalysisStepResult> ExecuteAsync(AnalysisStep step, AnalysisInput input,
        CaptureAnalysisRecord? metadata, Func<AnalysisProgressScope> beginProgress, CancellationToken cancellationToken)
    {
        AnalyzerOutcome strongestFailure = AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "no-compatible-model");
        foreach (string providerId in step.Candidates)
        {
            for (int retry = 0; retry <= step.RetryCount; retry++)
            {
                using AnalysisProgressScope progress = beginProgress();
                ProviderAttempt attempt = await ExecuteAttemptAsync(providerId, step, input, metadata, progress, cancellationToken).ConfigureAwait(false);
                LogFailure(providerId, step, attempt.Outcome);

                if (attempt.NextAction == NextAction.FinishStep)
                {
                    return new(attempt.Outcome, attempt.Derivation);
                }

                // An unavailable/unsupported fallback cannot hide an execution failure.
                if (GetFailurePriority(attempt.Outcome) >= GetFailurePriority(strongestFailure))
                {
                    strongestFailure = attempt.Outcome;
                }

                if (attempt.NextAction == NextAction.TryNextProvider)
                {
                    break;
                }
            }
        }

        return new(strongestFailure, null);
    }

    private async Task<ProviderAttempt> ExecuteAttemptAsync(string providerId, AnalysisStep step, AnalysisInput input,
        CaptureAnalysisRecord? metadata, IProgress<AnalysisProgress> progress, CancellationToken cancellationToken)
    {
        IMetadataProcessor? processor = _processors.GetValueOrDefault(providerId);
        try
        {
            if (processor != null)
            {
                return await ExecuteMetadataAsync(processor, step, input, metadata, progress, cancellationToken).ConfigureAwait(false);
            }

            IMediaAnalyzer analyzer = _analyzers[providerId];
            ProviderAttempt attempt = await ExecuteWhenReadyAsync(step, progress,
                getAvailability: token => analyzer.GetAvailabilityAsync(input.MediaKind, input.Language, token).AsTask(),
                prepare: token => analyzer.PrepareAsync(progress, token),
                execute: token => analyzer.AnalyzeAsync(input, progress, token),
                cancellationToken).ConfigureAwait(false);
            return ValidateOutput(attempt, step, input.MediaKind, providerId, isMetadata: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AnalysisProviderBusyException) when (processor != null && !cancellationToken.IsCancellationRequested)
        {
            // Commit this metadata failure so independent companion steps survive.
            // The session still blocks those steps until native ownership ends.
            return new(Failed("provider-not-stopped"), NextAction.FinishStep);
        }
        catch (AnalysisProviderBusyException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return new(Failed("model-timeout"), NextAction.RetryProvider);
        }
        catch (ModelDownloadRequiredException)
        {
            return new(AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.TemporarilyUnavailable,
                "model-download-required"), NextAction.TryNextProvider);
        }
        catch (OperationCanceledException)
        {
            var cancelled = AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Cancelled, "model-cancelled");
            return new(cancelled, NextAction.FinishStep);
        }
        catch (Exception)
        {
            return new(Failed("model-failed"), NextAction.RetryProvider);
        }
    }

    private async Task<ProviderAttempt> ExecuteMetadataAsync(IMetadataProcessor processor, AnalysisStep step,
        AnalysisInput input, CaptureAnalysisRecord? metadata, IProgress<AnalysisProgress> progress, CancellationToken cancellationToken)
    {
        if (metadata == null)
        {
            var missing = AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "metadata-input-missing");
            return new(missing, NextAction.FinishStep);
        }

        // Select whole entries immediately before each attempt. The store rechecks
        // their identities at commit; preparation never owns the publication gate.
        MetadataProcessorInput snapshot = MetadataProcessorInput.Create(metadata, processor.Descriptor, cancellationToken);
        if (!snapshot.Entries.Any(entry => !string.IsNullOrWhiteSpace(entry.Text)))
        {
            var empty = AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "metadata-input-empty");
            return new(empty, NextAction.FinishStep);
        }

        ProviderAttempt attempt = await ExecuteWhenReadyAsync(step, progress,
            getAvailability: token => processor.GetAvailabilityAsync(token).AsTask(),
            prepare: token => processor.PrepareAsync(progress, token),
            execute: token => processor.ProcessAsync(snapshot, token),
            cancellationToken).ConfigureAwait(false);
        attempt = ValidateOutput(attempt, step, input.MediaKind, processor.Descriptor.Id, isMetadata: true);

        if (attempt.Outcome.Kind == AnalyzerOutcomeKind.Succeeded)
        {
            var result = new AnalysisResult(attempt.Outcome.Payload!, attempt.Outcome.Producer!,
                DateTimeOffset.UtcNow, metadata.PlanVersion, derivation: snapshot.Derivation);
            _ = metadata.WithResult(result); // Validate evidence against the selected source results.
            return attempt with { Derivation = snapshot.Derivation };
        }

        if (attempt.Outcome.Kind == AnalyzerOutcomeKind.InvalidSource)
        {
            return new(Failed("invalid-metadata-input"), NextAction.RetryProvider);
        }

        return attempt;
    }

    private async Task<ProviderAttempt> ExecuteWhenReadyAsync(AnalysisStep step, IProgress<AnalysisProgress> progress,
        Func<CancellationToken, Task<AnalyzerAvailability>> getAvailability,
        Func<CancellationToken, Task<AnalyzerAvailability>> prepare,
        Func<CancellationToken, Task<AnalyzerOutcome>> execute, CancellationToken cancellationToken)
    {
        AnalyzerAvailability availability = await _session.InvokeAsync(getAvailability, AvailabilityTimeout, cancellationToken).ConfigureAwait(false);
        if (availability == AnalyzerAvailability.PreparationRequired)
        {
            progress.Report(new(AnalysisProgressStage.Preparing));
            availability = await _session.InvokeAsync(prepare, step.PreparationTimeout, cancellationToken).ConfigureAwait(false);
        }

        if (availability != AnalyzerAvailability.Ready)
        {
            // Readiness failures move to the next provider without spending inference retries.
            return new(Unavailable(availability), NextAction.TryNextProvider);
        }

        progress.Report(new(AnalysisProgressStage.Analyzing));
        AnalyzerOutcome outcome = await _session.InvokeAsync(execute, step.ExecutionTimeout, cancellationToken).ConfigureAwait(false);
        NextAction nextAction = outcome.Kind switch
        {
            AnalyzerOutcomeKind.Failed or AnalyzerOutcomeKind.TemporarilyUnavailable => NextAction.RetryProvider,
            AnalyzerOutcomeKind.Unsupported => NextAction.TryNextProvider,
            _ => NextAction.FinishStep,
        };
        return new(outcome, nextAction);
    }

    private static ProviderAttempt ValidateOutput(ProviderAttempt attempt, AnalysisStep step,
        AnalysisMediaKind mediaKind, string providerId, bool isMetadata)
    {
        AnalyzerOutcome outcome = attempt.Outcome;
        if (outcome.Kind != AnalyzerOutcomeKind.Succeeded)
        {
            return attempt;
        }

        bool matchesStep = outcome.Payload!.Capability == step.Capability && outcome.Payload.Supports(mediaKind);
        bool matchesProvider = outcome.Producer!.AnalyzerId == providerId;
        bool matchesInput = (outcome.Payload is DerivedAnalysisPayload) == isMetadata;
        return matchesStep && matchesProvider && matchesInput
            ? attempt
            : new(Failed("invalid-model-output"), NextAction.RetryProvider);
    }

    private static AnalyzerOutcome Unavailable(AnalyzerAvailability availability)
    {
        AnalyzerOutcomeKind kind = availability is AnalyzerAvailability.Unsupported or AnalyzerAvailability.LanguageResourcesRequired
            ? AnalyzerOutcomeKind.Unsupported
            : AnalyzerOutcomeKind.TemporarilyUnavailable;
        string code = availability == AnalyzerAvailability.LanguageResourcesRequired ? "ocr-language-required" : "model-unavailable";
        return AnalyzerOutcome.Unsuccessful(kind, code);
    }

    private static AnalyzerOutcome Failed(string code) => AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, code);

    private void LogFailure(string providerId, AnalysisStep step, AnalyzerOutcome outcome)
    {
        if (outcome.Kind is AnalyzerOutcomeKind.Failed or AnalyzerOutcomeKind.TemporarilyUnavailable)
        {
            _log?.LogWarning($"Analysis provider {providerId}, step {step.Capability.Name}: {outcome.Kind} ({outcome.FailureCode}).");
        }
    }

    private static int GetFailurePriority(AnalyzerOutcome outcome) => outcome.Kind switch
    {
        AnalyzerOutcomeKind.Failed => 3,
        AnalyzerOutcomeKind.TemporarilyUnavailable when outcome.FailureCode == "model-download-required" => 2,
        AnalyzerOutcomeKind.TemporarilyUnavailable => 1,
        _ => 0,
    };

    private enum NextAction { RetryProvider, TryNextProvider, FinishStep }

    private sealed record ProviderAttempt(AnalyzerOutcome Outcome, NextAction NextAction, AnalysisDerivation? Derivation = null);
}

internal sealed record AnalysisStepResult(AnalyzerOutcome Outcome, AnalysisDerivation? Derivation);
