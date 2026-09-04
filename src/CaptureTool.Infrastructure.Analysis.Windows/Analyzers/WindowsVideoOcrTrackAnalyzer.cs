using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Media;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows.Media;
using System.Text;
using System.Text.Json;

namespace CaptureTool.Infrastructure.Analysis.Windows.Analyzers;

public sealed class WindowsVideoOcrTrackAnalyzer : IPreparableCaptureAnalyzer
{
    public const string AdapterVersion = "1.3.0";
    public const string LegacyAnalyzerId = "windows-video-frame-ocr";
    public const string WindowsAiAnalyzerId = "windows-ai-video-frame-ocr";
    private const int CheckpointSchemaVersion = 2;
    private const int CheckpointSampleInterval = 10;
    private static readonly TimeSpan PreferredSampleInterval = TimeSpan.FromSeconds(1);

    private readonly IVideoAnalysisFrameSource _frames;
    private readonly ITextExtractionAnalysisService _textExtraction;

    public WindowsVideoOcrTrackAnalyzer(
        IVideoAnalysisFrameSource frames,
        ITextExtractionAnalysisService textExtraction)
        : this(
            frames,
            textExtraction,
            LegacyAnalyzerId,
            CaptureAnalyzerRequirement.OperatingSystemCapability,
            qualityTier: 60)
    {
    }

    internal WindowsVideoOcrTrackAnalyzer(
        IVideoAnalysisFrameSource frames,
        ITextExtractionAnalysisService textExtraction,
        string analyzerId,
        CaptureAnalyzerRequirement requirements,
        int qualityTier)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(textExtraction);
        _frames = frames;
        _textExtraction = textExtraction;

        TextExtractionModelDescriptor model = textExtraction.ModelDescriptor;
        Descriptor = new CaptureAnalyzerDescriptor(
            AnalysisCapabilities.VideoOcrTrackV1,
            new AnalyzerIdentity(
                analyzerId,
                providerId: model.ProducerId,
                modelId: model.ModelId,
                modelVersion: model.ModelVersion,
                adapterVersion: AdapterVersion,
                runtimeId: model.RuntimeId,
                runtimeVersion: model.RuntimeVersion,
                packageVersion: null,
                configurationFingerprint:
                    "sha256:6616ca9b2bd6ca025cdaba2f0e1073faf95cf40bb78bbadb37cdf4297490c2d8"),
            [CaptureMediaKind.Video],
            ProcessingBoundary.OnDevice,
            CaptureAnalyzerDataKind.None,
            requirements,
            CaptureAnalyzerWorkloadClass.AiIntensive,
            maximumSourceBytes: 2L * 1024 * 1024 * 1024,
            qualityTier);
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

        try
        {
            CaptureAnalyzerAvailability availability = _textExtraction.GetReadyState() switch
            {
                TextExtractionReadyState.Ready => CaptureAnalyzerAvailability.Available,
                TextExtractionReadyState.PreparationNeeded =>
                    CaptureAnalyzerAvailability.PreparationRequired,
                TextExtractionReadyState.Disabled => CaptureAnalyzerAvailability.Disabled,
                TextExtractionReadyState.NotSupported => CaptureAnalyzerAvailability.Unsupported(
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return ValueTask.FromResult(CaptureAnalyzerAvailability.TemporarilyUnavailable(
                new AnalysisFailure(
                    AnalysisFailureCode.ProviderUnavailable,
                    AnalysisFailureDisposition.Transient)));
        }
    }

    public async Task<CaptureAnalyzerPreparationResult> PrepareAsync(
        IProgress<AnalysisCapabilityPreparationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        TextExtractionPreparationResult result = await _textExtraction
            .EnsureReadyAsync(cancellationToken)
            .ConfigureAwait(false);
        if (result.Status == TextExtractionPreparationStatus.Success)
        {
            progress?.Report(new AnalysisCapabilityPreparationProgress(1));
            return CaptureAnalyzerPreparationResult.Succeeded;
        }

        return result.Status switch
        {
            TextExtractionPreparationStatus.NotSupported =>
                CaptureAnalyzerPreparationResult.Unsupported(new AnalysisFailure(
                    AnalysisFailureCode.CapabilityUnavailable,
                    AnalysisFailureDisposition.Terminal)),
            TextExtractionPreparationStatus.Cancelled =>
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

        var observations = new List<VideoOcrObservationV1>();
        string? activeText = null;
        TimeSpan activeStart = default;
        TimeSpan activeEnd = default;
        TimeSpan nextSampleTime = TimeSpan.Zero;
        int processedSinceCheckpoint = 0;
        try
        {
            WindowsVideoOcrCheckpointDocument? checkpoint = await RestoreCheckpointAsync(
                request.Checkpoint,
                cancellationToken).ConfigureAwait(false);
            if (checkpoint != null)
            {
                nextSampleTime = TimeSpan.FromTicks(checkpoint.NextSampleTicks);
                observations.AddRange(checkpoint.Observations.Select(observation =>
                    new VideoOcrObservationV1(
                        observation.Text,
                        TimeSpan.FromTicks(observation.StartTicks),
                        TimeSpan.FromTicks(observation.EndTicks))));
                activeText = checkpoint.ActiveText;
                activeStart = TimeSpan.FromTicks(checkpoint.ActiveStartTicks);
                activeEnd = TimeSpan.FromTicks(checkpoint.ActiveEndTicks);
            }

            await using Stream source = await request.Source.OpenReadAsync(cancellationToken)
                .ConfigureAwait(false);

            await foreach (VideoAnalysisFrame frame in _frames
                .ReadSampledFramesAsync(
                    source,
                    PreferredSampleInterval,
                    WindowsVideoAnalysisFrameSource.MaximumSampledFrameCount,
                    nextSampleTime,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                await using (frame.ConfigureAwait(false))
                {
                    TextExtractionAnalysisResult result = await _textExtraction
                        .ExtractAnalysisAsync(frame.Image, cancellationToken)
                        .ConfigureAwait(false);
                    if (result.Status != TextExtractionAnalysisStatus.Succeeded)
                    {
                        return MapFailure(result.Status);
                    }

                    if (result.Document == null)
                    {
                        return InvalidResponse();
                    }

                    string text = NormalizeText(result.Document.FullText);
                    if (text.Length == 0)
                    {
                        FlushObservation(observations, ref activeText, activeStart, activeEnd);
                    }
                    else if (string.Equals(activeText, text, StringComparison.Ordinal))
                    {
                        activeEnd = frame.EndTime;
                    }
                    else
                    {
                        FlushObservation(observations, ref activeText, activeStart, activeEnd);
                        activeText = text;
                        activeStart = frame.StartTime;
                        activeEnd = frame.EndTime;
                    }

                    nextSampleTime = frame.ResumeTime;
                    processedSinceCheckpoint++;
                    if (processedSinceCheckpoint >= CheckpointSampleInterval)
                    {
                        await TrySaveCheckpointAsync(
                            request.Checkpoint,
                            nextSampleTime,
                            observations,
                            activeText,
                            activeStart,
                            activeEnd,
                            cancellationToken).ConfigureAwait(false);
                        processedSinceCheckpoint = 0;
                    }
                }
            }

            FlushObservation(observations, ref activeText, activeStart, activeEnd);
            string fullText = string.Join('\n', observations.Select(observation => observation.Text));
            return CaptureAnalyzerOutput.Succeeded(new VideoOcrTrackV1(fullText, observations));
        }
        catch (OperationCanceledException)
        {
            await TrySaveCheckpointAsync(
                request.Checkpoint,
                nextSampleTime,
                observations,
                activeText,
                activeStart,
                activeEnd,
                CancellationToken.None).ConfigureAwait(false);
            return CaptureAnalyzerOutput.Cancelled;
        }
        catch (NotSupportedException)
        {
            return CaptureAnalyzerOutput.Unsupported(new AnalysisFailure(
                AnalysisFailureCode.CapabilityUnavailable,
                AnalysisFailureDisposition.Terminal));
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or ArgumentException or OverflowException)
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

    private static async Task<WindowsVideoOcrCheckpointDocument?> RestoreCheckpointAsync(
        ICaptureAnalyzerCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            ReadOnlyMemory<byte>? payload = await checkpoint.ReadAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!payload.HasValue)
            {
                return null;
            }

            WindowsVideoOcrCheckpointDocument? document = JsonSerializer.Deserialize(
                payload.Value.Span,
                WindowsVideoOcrCheckpointJsonContext.Default.WindowsVideoOcrCheckpointDocument);
            if (document == null ||
                document.SchemaVersion != CheckpointSchemaVersion ||
                !string.Equals(document.AdapterVersion, AdapterVersion, StringComparison.Ordinal) ||
                document.NextSampleTicks < 0 ||
                document.Observations == null ||
                (document.ActiveText == null &&
                    (document.ActiveStartTicks != 0 || document.ActiveEndTicks != 0)) ||
                (document.ActiveText != null &&
                    (document.ActiveStartTicks < 0 ||
                     document.ActiveEndTicks <= document.ActiveStartTicks ||
                     document.NextSampleTicks < document.ActiveEndTicks)))
            {
                throw new InvalidDataException("The video OCR checkpoint is incompatible.");
            }

            VideoOcrObservationV1[] restored = document.Observations.Select(observation =>
                new VideoOcrObservationV1(
                    observation.Text,
                    TimeSpan.FromTicks(observation.StartTicks),
                    TimeSpan.FromTicks(observation.EndTicks))).ToArray();
            _ = new VideoOcrTrackV1(
                string.Join('\n', restored.Select(observation => observation.Text)),
                restored);
            if (document.ActiveText != null)
            {
                _ = new VideoOcrObservationV1(
                    document.ActiveText,
                    TimeSpan.FromTicks(document.ActiveStartTicks),
                    TimeSpan.FromTicks(document.ActiveEndTicks));
                if (restored.Length > 0 &&
                    TimeSpan.FromTicks(document.ActiveStartTicks) < restored[^1].EndTime)
                {
                    throw new InvalidDataException(
                        "The active video OCR checkpoint span overlaps a completed observation.");
                }
            }

            return document;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            try
            {
                await checkpoint.ClearAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
            }

            return null;
        }
    }

    private static async Task TrySaveCheckpointAsync(
        ICaptureAnalyzerCheckpoint checkpoint,
        TimeSpan nextSampleTime,
        IEnumerable<VideoOcrObservationV1> observations,
        string? activeText,
        TimeSpan activeStart,
        TimeSpan activeEnd,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = new WindowsVideoOcrCheckpointDocument
            {
                SchemaVersion = CheckpointSchemaVersion,
                AdapterVersion = AdapterVersion,
                NextSampleTicks = nextSampleTime.Ticks,
                Observations = observations.Select(observation =>
                    new WindowsVideoOcrCheckpointObservationDocument
                    {
                        Text = observation.Text,
                        StartTicks = observation.StartTime.Ticks,
                        EndTicks = observation.EndTime.Ticks,
                    }).ToList(),
                ActiveText = activeText,
                ActiveStartTicks = activeText == null ? 0 : activeStart.Ticks,
                ActiveEndTicks = activeText == null ? 0 : activeEnd.Ticks,
            };
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
                document,
                WindowsVideoOcrCheckpointJsonContext.Default.WindowsVideoOcrCheckpointDocument);
            await checkpoint.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            // Checkpointing is a recovery aid and cannot make an otherwise valid analysis fail.
        }
    }

    private static void FlushObservation(
        ICollection<VideoOcrObservationV1> observations,
        ref string? text,
        TimeSpan startTime,
        TimeSpan endTime)
    {
        if (text == null)
        {
            return;
        }

        observations.Add(new VideoOcrObservationV1(text, startTime, endTime));
        text = null;
    }

    private static string NormalizeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim()
            .Normalize(NormalizationForm.FormC);
    }

    private static CaptureAnalyzerOutput MapFailure(TextExtractionAnalysisStatus status)
    {
        return status switch
        {
            TextExtractionAnalysisStatus.Unavailable => CaptureAnalyzerOutput.Unsupported(
                new AnalysisFailure(
                    AnalysisFailureCode.CapabilityUnavailable,
                    AnalysisFailureDisposition.Terminal)),
            TextExtractionAnalysisStatus.Cancelled => CaptureAnalyzerOutput.Cancelled,
            TextExtractionAnalysisStatus.TransientFailure => CaptureAnalyzerOutput.Failed(
                new AnalysisFailure(
                    AnalysisFailureCode.ProviderUnavailable,
                    AnalysisFailureDisposition.Transient)),
            _ => InvalidResponse(),
        };
    }

    private static CaptureAnalyzerOutput InvalidResponse()
    {
        return CaptureAnalyzerOutput.Failed(new AnalysisFailure(
            AnalysisFailureCode.InvalidResponse,
            AnalysisFailureDisposition.Terminal));
    }
}
