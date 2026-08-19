using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Media;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Infrastructure.Analysis.Windows.Analyzers;

public sealed class WindowsAiSpeechTranscriptAnalyzer : IPreparableCaptureAnalyzer
{
    public const string AdapterVersion = "1.0.0";

    private readonly IWindowsAiSpeechRecognitionService _speech;
    private readonly IVideoAudioExtractionService? _videoAudioExtraction;

    public WindowsAiSpeechTranscriptAnalyzer(
        IWindowsAiSpeechRecognitionService speech,
        IVideoAudioExtractionService? videoAudioExtraction = null)
    {
        ArgumentNullException.ThrowIfNull(speech);
        _speech = speech;
        _videoAudioExtraction = videoAudioExtraction;
        Descriptor = new CaptureAnalyzerDescriptor(
            AnalysisCapabilities.SpeechTranscriptV1,
            new AnalyzerIdentity(
                analyzerId: "windows-ai-speech-transcript",
                providerId: "microsoft-windows",
                modelId: WindowsAiSpeechRecognitionService.ModelId,
                modelVersion: null,
                adapterVersion: AdapterVersion,
                runtimeId: "windows-app-sdk-ai-experimental",
                runtimeVersion: WindowsAiSpeechRecognitionService.RuntimeVersion,
                packageVersion: WindowsAiSpeechRecognitionService.RuntimeVersion,
                configurationFingerprint:
                    "sha256:d33452b220a2da89cb3ae138559816793476449643685cf8f3604a5c8755177e"),
            videoAudioExtraction == null
                ? [CaptureMediaKind.Audio]
                : [CaptureMediaKind.Audio, CaptureMediaKind.Video],
            ProcessingBoundary.OnDevice,
            CaptureAnalyzerDataKind.None,
            CaptureAnalyzerRequirement.OperatingSystemCapability |
                CaptureAnalyzerRequirement.ModelPackage |
                CaptureAnalyzerRequirement.UserInitiatedPreparation,
            CaptureAnalyzerWorkloadClass.AiIntensive,
            maximumSourceBytes: 2L * 1024 * 1024 * 1024,
            qualityTier: 120);
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

        CaptureAnalyzerAvailability availability = _speech.GetReadyState() switch
        {
            WindowsAiSpeechReadyState.Ready => CaptureAnalyzerAvailability.Available,
            WindowsAiSpeechReadyState.PreparationNeeded =>
                CaptureAnalyzerAvailability.PreparationRequired,
            WindowsAiSpeechReadyState.NotSupported => CaptureAnalyzerAvailability.Unsupported(
                new AnalysisFailure(
                    AnalysisFailureCode.CapabilityUnavailable,
                    AnalysisFailureDisposition.Terminal)),
            WindowsAiSpeechReadyState.Disabled => CaptureAnalyzerAvailability.Disabled,
            _ => CaptureAnalyzerAvailability.TemporarilyUnavailable(new AnalysisFailure(
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
        WindowsAiSpeechPreparationResult result = await _speech
            .PrepareAsync(providerProgress, cancellationToken)
            .ConfigureAwait(false);
        return result.Status switch
        {
            WindowsAiSpeechPreparationStatus.Succeeded =>
                CaptureAnalyzerPreparationResult.Succeeded,
            WindowsAiSpeechPreparationStatus.Unsupported =>
                CaptureAnalyzerPreparationResult.Unsupported(new AnalysisFailure(
                    AnalysisFailureCode.CapabilityUnavailable,
                    AnalysisFailureDisposition.Terminal)),
            WindowsAiSpeechPreparationStatus.Disabled =>
                CaptureAnalyzerPreparationResult.Disabled,
            WindowsAiSpeechPreparationStatus.Cancelled =>
                CaptureAnalyzerPreparationResult.Cancelled,
            _ => CaptureAnalyzerPreparationResult.Failed(new AnalysisFailure(
                AnalysisFailureCode.ProviderUnavailable,
                AnalysisFailureDisposition.Transient)),
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
            if (request.Source.MediaKind != CaptureMediaKind.Video)
            {
                return Map(await _speech.TranscribeAsync(source, cancellationToken)
                    .ConfigureAwait(false));
            }

            if (_videoAudioExtraction == null)
            {
                return Unsupported();
            }

            await using VideoAudioExtractionResult extraction = await _videoAudioExtraction
                .ExtractWaveAudioAsync(source, cancellationToken)
                .ConfigureAwait(false);
            if (extraction.Status != VideoAudioExtractionStatus.Succeeded || extraction.Audio == null)
            {
                return extraction.Status switch
                {
                    VideoAudioExtractionStatus.NoAudio or VideoAudioExtractionStatus.Unsupported =>
                        Unsupported(),
                    VideoAudioExtractionStatus.Cancelled => CaptureAnalyzerOutput.Cancelled,
                    _ => CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                        AnalysisFailureCode.ProviderUnavailable,
                        AnalysisFailureDisposition.Transient)),
                };
            }

            return Map(await _speech.TranscribeAsync(extraction.Audio, cancellationToken)
                .ConfigureAwait(false));
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

    private static CaptureAnalyzerOutput Map(WindowsAiSpeechTranscriptionResult result)
    {
        if (result.Status == WindowsAiSpeechTranscriptionStatus.Succeeded)
        {
            try
            {
                return CaptureAnalyzerOutput.Succeeded(new SpeechTranscriptV1(
                    result.Transcript ?? string.Empty,
                    result.Segments?.Select(segment => new SpeechTranscriptSegmentV1(
                        segment.Text,
                        segment.StartTime,
                        segment.EndTime))));
            }
            catch (ArgumentException)
            {
                return InvalidResponse();
            }
        }

        return result.Status switch
        {
            WindowsAiSpeechTranscriptionStatus.PreparationRequired =>
                CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                    AnalysisFailureCode.ModelNotReady,
                    AnalysisFailureDisposition.Transient)),
            WindowsAiSpeechTranscriptionStatus.Unsupported or
                WindowsAiSpeechTranscriptionStatus.Disabled => Unsupported(),
            WindowsAiSpeechTranscriptionStatus.Cancelled => CaptureAnalyzerOutput.Cancelled,
            WindowsAiSpeechTranscriptionStatus.Failed => CaptureAnalyzerOutput.Failed(
                new AnalysisFailure(
                    AnalysisFailureCode.ProviderUnavailable,
                    AnalysisFailureDisposition.Transient)),
            _ => InvalidResponse(),
        };
    }

    private static CaptureAnalyzerOutput Unsupported() => CaptureAnalyzerOutput.Unsupported(
        new AnalysisFailure(
            AnalysisFailureCode.CapabilityUnavailable,
            AnalysisFailureDisposition.Terminal));

    private static CaptureAnalyzerOutput InvalidResponse() => CaptureAnalyzerOutput.Failed(
        new AnalysisFailure(
            AnalysisFailureCode.InvalidResponse,
            AnalysisFailureDisposition.Terminal));

    private sealed class DelegateProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
