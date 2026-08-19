using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Media;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Edit.Image.Description;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Text.Json;

namespace CaptureTool.Infrastructure.Analysis.Windows.Analyzers;

public sealed class WindowsVideoDescriptionTrackAnalyzer : IPreparableCaptureAnalyzer
{
    public const string AdapterVersion = "2.0.0";
    public const int MaximumSampleCount = 500;
    public static readonly TimeSpan PreferredSampleInterval = TimeSpan.FromSeconds(5);
    private const int CheckpointSchemaVersion = 1;
    private const int CheckpointSampleInterval = 10;

    private readonly IVideoAnalysisFrameSource _frames;
    private readonly IImageDescriptionAnalysisService _imageDescription;

    public WindowsVideoDescriptionTrackAnalyzer(
        IVideoAnalysisFrameSource frames,
        IImageDescriptionAnalysisService? imageDescription = null)
    {
        ArgumentNullException.ThrowIfNull(frames);
        _frames = frames;
        _imageDescription = imageDescription ?? UnavailableImageDescriptionAnalysisService.Instance;

        ImageDescriptionModelDescriptor model = _imageDescription.ModelDescriptor;
        Descriptor = new CaptureAnalyzerDescriptor(
            AnalysisCapabilities.VideoDescriptionTrackV1,
            new AnalyzerIdentity(
                analyzerId: "windows-video-frame-description",
                providerId: model.ProducerId,
                modelId: model.ModelId,
                modelVersion: model.ModelVersion,
                adapterVersion: AdapterVersion,
                runtimeId: model.RuntimeId,
                runtimeVersion: model.RuntimeVersion,
                packageVersion: model.PackageVersion,
                configurationFingerprint:
                    "sha256:afb8ea3e611b398b2af45bff11b13ca578dbbf2474476e7293ec6f133a34fc90"),
            [CaptureMediaKind.Video],
            ProcessingBoundary.OnDevice,
            CaptureAnalyzerDataKind.None,
            CaptureAnalyzerRequirement.OperatingSystemCapability |
                CaptureAnalyzerRequirement.ModelPackage |
                CaptureAnalyzerRequirement.UserInitiatedPreparation,
            CaptureAnalyzerWorkloadClass.AiIntensive,
            maximumSourceBytes: 2L * 1024 * 1024 * 1024,
            qualityTier: 100);
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

        CaptureAnalyzerAvailability availability = _imageDescription.GetReadyState() switch
        {
            ImageDescriptionReadyState.Ready => CaptureAnalyzerAvailability.Available,
            ImageDescriptionReadyState.PreparationNeeded =>
                CaptureAnalyzerAvailability.PreparationRequired,
            ImageDescriptionReadyState.NotSupported => CaptureAnalyzerAvailability.Unsupported(
                new AnalysisFailure(
                    AnalysisFailureCode.CapabilityUnavailable,
                    AnalysisFailureDisposition.Terminal)),
            ImageDescriptionReadyState.Disabled => CaptureAnalyzerAvailability.Disabled,
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
        ImageDescriptionAnalysisPreparationResult result = await _imageDescription
            .PrepareAnalysisAsync(providerProgress, cancellationToken)
            .ConfigureAwait(false);
        return result.Status switch
        {
            ImageDescriptionAnalysisPreparationStatus.Succeeded =>
                CaptureAnalyzerPreparationResult.Succeeded,
            ImageDescriptionAnalysisPreparationStatus.Unsupported =>
                CaptureAnalyzerPreparationResult.Unsupported(new AnalysisFailure(
                    AnalysisFailureCode.CapabilityUnavailable,
                    AnalysisFailureDisposition.Terminal)),
            ImageDescriptionAnalysisPreparationStatus.Disabled =>
                CaptureAnalyzerPreparationResult.Disabled,
            ImageDescriptionAnalysisPreparationStatus.Cancelled =>
                CaptureAnalyzerPreparationResult.Cancelled,
            ImageDescriptionAnalysisPreparationStatus.TransientFailure =>
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

        var observations = new List<VideoDescriptionObservationV1>();
        TimeSpan nextSampleTime = TimeSpan.Zero;
        int processedSinceCheckpoint = 0;
        try
        {
            WindowsVideoDescriptionCheckpointDocument? checkpoint = await RestoreCheckpointAsync(
                request.Checkpoint,
                cancellationToken).ConfigureAwait(false);
            if (checkpoint != null)
            {
                nextSampleTime = TimeSpan.FromTicks(checkpoint.NextSampleTicks);
                observations.AddRange(checkpoint.Observations.Select(observation =>
                    new VideoDescriptionObservationV1(
                        observation.Description,
                        TimeSpan.FromTicks(observation.StartTicks),
                        TimeSpan.FromTicks(observation.EndTicks))));
            }

            await using Stream source = await request.Source.OpenReadAsync(cancellationToken)
                .ConfigureAwait(false);
            await foreach (VideoAnalysisFrame frame in _frames.ReadSampledFramesAsync(
                source,
                PreferredSampleInterval,
                MaximumSampleCount,
                nextSampleTime,
                cancellationToken).ConfigureAwait(false))
            {
                await using (frame.ConfigureAwait(false))
                {
                    ImageDescriptionAnalysisResult result = await _imageDescription
                        .DescribeAnalysisAsync(frame.Image, cancellationToken)
                        .ConfigureAwait(false);
                    if (result.Status != ImageDescriptionAnalysisStatus.Succeeded)
                    {
                        return MapFailure(result.Status);
                    }

                    AddObservation(observations, new VideoDescriptionObservationV1(
                        result.Description,
                        frame.StartTime,
                        frame.EndTime));
                    nextSampleTime = frame.ResumeTime;
                    processedSinceCheckpoint++;
                    if (processedSinceCheckpoint >= CheckpointSampleInterval)
                    {
                        await TrySaveCheckpointAsync(
                            request.Checkpoint,
                            nextSampleTime,
                            observations,
                            cancellationToken).ConfigureAwait(false);
                        processedSinceCheckpoint = 0;
                    }
                }
            }

            string fullText = string.Join('\n',
                observations.Select(observation => observation.Description));
            return CaptureAnalyzerOutput.Succeeded(
                new VideoDescriptionTrackV1(fullText, observations));
        }
        catch (OperationCanceledException)
        {
            await TrySaveCheckpointAsync(
                request.Checkpoint,
                nextSampleTime,
                observations,
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

    private static CaptureAnalyzerOutput MapFailure(ImageDescriptionAnalysisStatus status)
    {
        return status switch
        {
            ImageDescriptionAnalysisStatus.PreparationRequired => CaptureAnalyzerOutput.Failed(
                new AnalysisFailure(
                    AnalysisFailureCode.ModelNotReady,
                    AnalysisFailureDisposition.Transient)),
            ImageDescriptionAnalysisStatus.Unsupported or ImageDescriptionAnalysisStatus.Disabled =>
                CaptureAnalyzerOutput.Unsupported(new AnalysisFailure(
                    AnalysisFailureCode.CapabilityUnavailable,
                    AnalysisFailureDisposition.Terminal)),
            ImageDescriptionAnalysisStatus.Cancelled => CaptureAnalyzerOutput.Cancelled,
            ImageDescriptionAnalysisStatus.TransientFailure => CaptureAnalyzerOutput.Failed(
                new AnalysisFailure(
                    AnalysisFailureCode.ProviderUnavailable,
                    AnalysisFailureDisposition.Transient)),
            _ => CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                AnalysisFailureCode.InvalidResponse,
                AnalysisFailureDisposition.Terminal)),
        };
    }

    private static void AddObservation(
        IList<VideoDescriptionObservationV1> observations,
        VideoDescriptionObservationV1 observation)
    {
        if (observations.LastOrDefault() is { } previous &&
            previous.EndTime == observation.StartTime &&
            string.Equals(
                previous.Description,
                observation.Description,
                StringComparison.Ordinal))
        {
            observations[^1] = new VideoDescriptionObservationV1(
                previous.Description,
                previous.StartTime,
                observation.EndTime);
            return;
        }

        observations.Add(observation);
    }

    private static async Task<WindowsVideoDescriptionCheckpointDocument?> RestoreCheckpointAsync(
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

            WindowsVideoDescriptionCheckpointDocument? document = JsonSerializer.Deserialize(
                payload.Value.Span,
                WindowsVideoDescriptionCheckpointJsonContext.Default
                    .WindowsVideoDescriptionCheckpointDocument);
            if (document == null ||
                document.SchemaVersion != CheckpointSchemaVersion ||
                !string.Equals(document.AdapterVersion, AdapterVersion, StringComparison.Ordinal) ||
                document.NextSampleTicks < 0 ||
                document.Observations == null)
            {
                throw new InvalidDataException("The video-description checkpoint is incompatible.");
            }

            VideoDescriptionObservationV1[] restored = document.Observations.Select(observation =>
                new VideoDescriptionObservationV1(
                    observation.Description,
                    TimeSpan.FromTicks(observation.StartTicks),
                    TimeSpan.FromTicks(observation.EndTicks))).ToArray();
            _ = new VideoDescriptionTrackV1(
                string.Join('\n', restored.Select(observation => observation.Description)),
                restored);
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
        IEnumerable<VideoDescriptionObservationV1> observations,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = new WindowsVideoDescriptionCheckpointDocument
            {
                SchemaVersion = CheckpointSchemaVersion,
                AdapterVersion = AdapterVersion,
                NextSampleTicks = nextSampleTime.Ticks,
                Observations = observations.Select(observation =>
                    new WindowsVideoDescriptionCheckpointObservationDocument
                    {
                        Description = observation.Description,
                        StartTicks = observation.StartTime.Ticks,
                        EndTicks = observation.EndTime.Ticks,
                    }).ToList(),
            };
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
                document,
                WindowsVideoDescriptionCheckpointJsonContext.Default
                    .WindowsVideoDescriptionCheckpointDocument);
            await checkpoint.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            // Checkpoints are a recovery aid and cannot fail an otherwise valid analysis.
        }
    }

    private sealed class DelegateProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class UnavailableImageDescriptionAnalysisService :
        IImageDescriptionAnalysisService
    {
        public static UnavailableImageDescriptionAnalysisService Instance { get; } = new();

        public ImageDescriptionModelDescriptor ModelDescriptor { get; } = new(
            "microsoft-windows",
            "windows-app-sdk-image-description",
            null,
            "windows-app-sdk-ai",
            null,
            null);

        public ImageDescriptionReadyState GetReadyState() => ImageDescriptionReadyState.NotSupported;

        public Task<ImageDescriptionAnalysisPreparationResult> PrepareAnalysisAsync(
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ImageDescriptionAnalysisPreparationResult.Unsupported);

        public Task<ImageDescriptionAnalysisResult> DescribeAnalysisAsync(
            Stream sourceImage,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ImageDescriptionAnalysisResult.Unsupported);
    }
}
