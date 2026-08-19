using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Media;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal;

public sealed class FoundryLocalSpeechTranscriptAnalyzer : IPreparableCaptureAnalyzer
{
    public const string AdapterVersion = "1.2.0";

    private readonly IFoundryLocalSpeechTranscriptionService _transcription;
    private readonly IVideoAudioExtractionService? _videoAudioExtraction;

    public FoundryLocalSpeechTranscriptAnalyzer(
        IFoundryLocalSpeechTranscriptionService transcription,
        IVideoAudioExtractionService? videoAudioExtraction = null)
    {
        ArgumentNullException.ThrowIfNull(transcription);
        _transcription = transcription;
        _videoAudioExtraction = videoAudioExtraction;
        Descriptor = new CaptureAnalyzerDescriptor(
            AnalysisCapabilities.SpeechTranscriptV1,
            new AnalyzerIdentity(
                analyzerId: "foundry-local-speech-transcript",
                providerId: "microsoft-foundry-local",
                modelId: FoundryLocalSpeechTranscriptionService.ModelAlias,
                modelVersion: null,
                adapterVersion: AdapterVersion,
                runtimeId: "microsoft-foundry-local-core",
                runtimeVersion: FoundryLocalSpeechTranscriptionService.RuntimeVersion,
                packageVersion: FoundryLocalSpeechTranscriptionService.RuntimeVersion,
                configurationFingerprint:
                    "sha256:77aa0597f58f79700e57f137f42183c33121597dd6876415c08787bc34ff4e67"),
            videoAudioExtraction == null
                ? [CaptureMediaKind.Audio]
                : [CaptureMediaKind.Audio, CaptureMediaKind.Video],
            ProcessingBoundary.OnDevice,
            CaptureAnalyzerDataKind.None,
            CaptureAnalyzerRequirement.ModelPackage |
                CaptureAnalyzerRequirement.UserInitiatedPreparation,
            CaptureAnalyzerWorkloadClass.AiIntensive,
            maximumSourceBytes: 2L * 1024 * 1024 * 1024,
            qualityTier: 40);
    }

    public CaptureAnalyzerDescriptor Descriptor { get; }

    public ValueTask<CaptureAnalyzerAvailability> GetAvailabilityAsync(
        CaptureAnalyzerAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!request.IsEligibleFor(Descriptor))
        {
            throw new ArgumentException("The availability request targets another analyzer.", nameof(request));
        }

        CaptureAnalyzerAvailability availability = _transcription.GetReadyState() switch
        {
            FoundryLocalSpeechReadyState.Ready => CaptureAnalyzerAvailability.Available,
            FoundryLocalSpeechReadyState.PreparationNeeded =>
                CaptureAnalyzerAvailability.PreparationRequired,
            FoundryLocalSpeechReadyState.NotSupported => CaptureAnalyzerAvailability.Unsupported(
                new AnalysisFailure(
                    AnalysisFailureCode.CapabilityUnavailable,
                    AnalysisFailureDisposition.Terminal)),
            _ => CaptureAnalyzerAvailability.TemporarilyUnavailable(
                new AnalysisFailure(
                    AnalysisFailureCode.ProviderUnavailable,
                    AnalysisFailureDisposition.Transient)),
        };
        return ValueTask.FromResult(availability);
    }

    public async Task<CaptureAnalyzerPreparationResult> PrepareAsync(
        IProgress<AnalysisCapabilityPreparationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        IProgress<double>? providerProgress = progress == null
            ? null
            : new DelegateProgress<double>(fraction =>
                progress.Report(new AnalysisCapabilityPreparationProgress(fraction)));
        FoundryLocalSpeechPreparationResult result = await _transcription
            .PrepareAsync(providerProgress, cancellationToken)
            .ConfigureAwait(false);
        return result.Status switch
        {
            FoundryLocalSpeechPreparationStatus.Succeeded =>
                CaptureAnalyzerPreparationResult.Succeeded,
            FoundryLocalSpeechPreparationStatus.Unsupported =>
                CaptureAnalyzerPreparationResult.Unsupported(new AnalysisFailure(
                    AnalysisFailureCode.CapabilityUnavailable,
                    AnalysisFailureDisposition.Terminal)),
            FoundryLocalSpeechPreparationStatus.Cancelled =>
                CaptureAnalyzerPreparationResult.Cancelled,
            FoundryLocalSpeechPreparationStatus.Failed =>
                CaptureAnalyzerPreparationResult.Failed(new AnalysisFailure(
                    AnalysisFailureCode.ProviderUnavailable,
                    AnalysisFailureDisposition.Transient)),
            _ => CaptureAnalyzerPreparationResult.Failed(new AnalysisFailure(
                AnalysisFailureCode.InvalidResponse,
                AnalysisFailureDisposition.Terminal)),
        };
    }

    public async Task<CaptureAnalyzerOutput> AnalyzeAsync(
        CaptureAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.IsEligibleFor(Descriptor))
        {
            throw new ArgumentException("The analysis request targets another analyzer.", nameof(request));
        }

        try
        {
            await using Stream source = await request.Source.OpenReadAsync(cancellationToken)
                .ConfigureAwait(false);
            if (request.Source.MediaKind == CaptureMediaKind.Video)
            {
                return await AnalyzeVideoAsync(source, cancellationToken).ConfigureAwait(false);
            }

            FoundryLocalTranscriptionResult result = await _transcription
                .TranscribeAsync(source, cancellationToken)
                .ConfigureAwait(false);
            return MapTranscriptionResult(result);
        }
        catch (OperationCanceledException)
        {
            return CaptureAnalyzerOutput.Cancelled;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or ArgumentException)
        {
            return CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                AnalysisFailureCode.InvalidSource,
                AnalysisFailureDisposition.Terminal));
        }
        catch
        {
            return CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                AnalysisFailureCode.ProviderUnavailable,
                AnalysisFailureDisposition.Transient));
        }
    }

    private async Task<CaptureAnalyzerOutput> AnalyzeVideoAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        if (_videoAudioExtraction == null)
        {
            return CaptureAnalyzerOutput.Unsupported(new AnalysisFailure(
                AnalysisFailureCode.CapabilityUnavailable,
                AnalysisFailureDisposition.Terminal));
        }

        await using VideoAudioExtractionResult extraction = await _videoAudioExtraction
            .ExtractWaveAudioAsync(source, cancellationToken)
            .ConfigureAwait(false);
        if (extraction.Status != VideoAudioExtractionStatus.Succeeded)
        {
            return extraction.Status switch
            {
                VideoAudioExtractionStatus.NoAudio or VideoAudioExtractionStatus.Unsupported =>
                    CaptureAnalyzerOutput.Unsupported(new AnalysisFailure(
                        AnalysisFailureCode.CapabilityUnavailable,
                        AnalysisFailureDisposition.Terminal)),
                VideoAudioExtractionStatus.Cancelled => CaptureAnalyzerOutput.Cancelled,
                VideoAudioExtractionStatus.Failed => CaptureAnalyzerOutput.Failed(
                    new AnalysisFailure(
                        AnalysisFailureCode.ProviderUnavailable,
                        AnalysisFailureDisposition.Transient)),
                _ => CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                    AnalysisFailureCode.InvalidResponse,
                    AnalysisFailureDisposition.Terminal)),
            };
        }

        if (extraction.Audio == null)
        {
            return CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                AnalysisFailureCode.InvalidResponse,
                AnalysisFailureDisposition.Terminal));
        }

        FoundryLocalTranscriptionResult transcription = await _transcription
            .TranscribeAsync(extraction.Audio, cancellationToken)
            .ConfigureAwait(false);
        return MapTranscriptionResult(transcription);
    }

    private static CaptureAnalyzerOutput MapTranscriptionResult(
        FoundryLocalTranscriptionResult result)
    {
        return result.Status switch
        {
            FoundryLocalTranscriptionStatus.Succeeded =>
                CreateSuccessfulOutput(result),
            FoundryLocalTranscriptionStatus.PreparationRequired => CaptureAnalyzerOutput.Failed(
                new AnalysisFailure(
                    AnalysisFailureCode.ModelNotReady,
                    AnalysisFailureDisposition.Transient)),
            FoundryLocalTranscriptionStatus.Unsupported => CaptureAnalyzerOutput.Unsupported(
                new AnalysisFailure(
                    AnalysisFailureCode.CapabilityUnavailable,
                    AnalysisFailureDisposition.Terminal)),
            FoundryLocalTranscriptionStatus.Cancelled => CaptureAnalyzerOutput.Cancelled,
            FoundryLocalTranscriptionStatus.Failed => CaptureAnalyzerOutput.Failed(
                new AnalysisFailure(
                    AnalysisFailureCode.ProviderUnavailable,
                    AnalysisFailureDisposition.Transient)),
            _ => CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                AnalysisFailureCode.InvalidResponse,
                AnalysisFailureDisposition.Terminal)),
        };
    }

    private static CaptureAnalyzerOutput CreateSuccessfulOutput(FoundryLocalTranscriptionResult result)
    {
        try
        {
            IEnumerable<SpeechTranscriptSegmentV1> segments = result.Segments?.Select(segment =>
                new SpeechTranscriptSegmentV1(
                    segment.Text,
                    segment.StartTime,
                    segment.EndTime)) ?? [];
            return CaptureAnalyzerOutput.Succeeded(new SpeechTranscriptV1(
                result.Transcript ?? string.Empty,
                segments,
                result.LanguageTag));
        }
        catch (ArgumentException)
        {
            return CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                AnalysisFailureCode.InvalidResponse,
                AnalysisFailureDisposition.Terminal));
        }
    }

    private sealed class DelegateProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
