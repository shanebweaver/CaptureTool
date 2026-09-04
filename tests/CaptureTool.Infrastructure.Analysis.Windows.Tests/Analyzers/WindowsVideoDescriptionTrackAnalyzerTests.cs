using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Media;
using CaptureTool.Application.Abstractions.Edit.Image.Description;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows.Analyzers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace CaptureTool.Infrastructure.Analysis.Windows.Tests.Analyzers;

[TestClass]
public sealed class WindowsVideoDescriptionTrackAnalyzerTests
{
    private static readonly AnalysisPurpose Purpose = new("capture-memory-search", 1);

    [TestMethod]
    public void Descriptor_ShouldDeclareOptionalTimestampedVideoInference()
    {
        var analyzer = CreateAnalyzer([]);

        Assert.AreEqual(AnalysisCapabilities.VideoDescriptionTrackV1,
            analyzer.Descriptor.Capability);
        CollectionAssert.AreEqual(new[] { CaptureMediaKind.Video },
            analyzer.Descriptor.SupportedMediaKinds.ToArray());
        Assert.AreEqual("windows-video-frame-description",
            analyzer.Descriptor.Identity.AnalyzerId);
        Assert.AreEqual("windows-app-sdk-image-description",
            analyzer.Descriptor.Identity.ModelId);
        Assert.AreEqual(CaptureAnalyzerWorkloadClass.AiIntensive,
            analyzer.Descriptor.WorkloadClass);
    }

    [TestMethod]
    public async Task Analyze_ShouldDescribeBoundedAdaptiveSamplesAndPreserveTimecodes()
    {
        var frames = new StubFrameSource(
            (0, 0, 15),
            (1, 15, 30),
            (2, 30, 45));
        var descriptions = new QueueDescriptionService(
            [
                ImageDescriptionAnalysisResult.Succeeded("A welcome screen."),
                ImageDescriptionAnalysisResult.Succeeded("A settings panel."),
                ImageDescriptionAnalysisResult.Succeeded("A deployment graph."),
            ]);
        var analyzer = new WindowsVideoDescriptionTrackAnalyzer(frames, descriptions);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Succeeded, output.Status);
        Assert.AreEqual(WindowsVideoDescriptionTrackAnalyzer.PreferredSampleInterval,
            frames.RequestedInterval);
        Assert.AreEqual(WindowsVideoDescriptionTrackAnalyzer.MaximumSampleCount,
            frames.RequestedMaximumSampleCount);
        Assert.AreEqual(TimeSpan.Zero, frames.RequestedStartTime);
        var payload = (VideoDescriptionTrackV1)output.Payload!;
        Assert.AreEqual(
            "A welcome screen.\nA settings panel.\nA deployment graph.",
            payload.FullText);
        Assert.HasCount(3, payload.Observations);
        Assert.AreEqual(TimeSpan.FromSeconds(30), payload.Observations[2].StartTime);
        Assert.AreEqual(TimeSpan.FromSeconds(45), payload.Observations[2].EndTime);
    }

    [TestMethod]
    public async Task Analyze_ShouldCoalesceIdenticalAdjacentVisualDescriptions()
    {
        var frames = new StubFrameSource(
            (0, 0, 5),
            (1, 5, 10),
            (2, 10, 15));
        var analyzer = new WindowsVideoDescriptionTrackAnalyzer(
            frames,
            new QueueDescriptionService(
            [
                ImageDescriptionAnalysisResult.Succeeded("A settings panel."),
                ImageDescriptionAnalysisResult.Succeeded("A settings panel."),
                ImageDescriptionAnalysisResult.Succeeded("A deployment graph."),
            ]));

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Succeeded, output.Status);
        var payload = (VideoDescriptionTrackV1)output.Payload!;
        Assert.AreEqual("A settings panel.\nA deployment graph.", payload.FullText);
        Assert.HasCount(2, payload.Observations);
        Assert.AreEqual(TimeSpan.Zero, payload.Observations[0].StartTime);
        Assert.AreEqual(TimeSpan.FromSeconds(10), payload.Observations[0].EndTime);
        Assert.AreEqual(TimeSpan.FromSeconds(10), payload.Observations[1].StartTime);
    }

    [TestMethod]
    [DataRow(ImageDescriptionReadyState.Ready, CaptureAnalyzerAvailabilityStatus.Available)]
    [DataRow(ImageDescriptionReadyState.PreparationNeeded,
        CaptureAnalyzerAvailabilityStatus.PreparationRequired)]
    [DataRow(ImageDescriptionReadyState.NotSupported,
        CaptureAnalyzerAvailabilityStatus.Unsupported)]
    [DataRow(ImageDescriptionReadyState.Disabled, CaptureAnalyzerAvailabilityStatus.Disabled)]
    [DataRow(ImageDescriptionReadyState.Unknown,
        CaptureAnalyzerAvailabilityStatus.TemporarilyUnavailable)]
    public async Task Availability_ShouldMapImageDescriptionReadyState(
        ImageDescriptionReadyState source,
        CaptureAnalyzerAvailabilityStatus expected)
    {
        var analyzer = new WindowsVideoDescriptionTrackAnalyzer(
            new StubFrameSource(),
            new QueueDescriptionService([], source));
        var request = new CaptureAnalyzerAvailabilityRequest(
            analyzer.Descriptor,
            CaptureMediaKind.Video,
            sourceLength: 4,
            Purpose,
            AnalysisProcessingPolicy.LocalOnly(Purpose));

        CaptureAnalyzerAvailability availability = await analyzer.GetAvailabilityAsync(request);

        Assert.AreEqual(expected, availability.Status);
    }

    [TestMethod]
    [DataRow(ImageDescriptionAnalysisStatus.PreparationRequired,
        CaptureAnalyzerOutputStatus.Failed, AnalysisFailureCode.ModelNotReady)]
    [DataRow(ImageDescriptionAnalysisStatus.Unsupported,
        CaptureAnalyzerOutputStatus.Unsupported, AnalysisFailureCode.CapabilityUnavailable)]
    [DataRow(ImageDescriptionAnalysisStatus.Disabled,
        CaptureAnalyzerOutputStatus.Unsupported, AnalysisFailureCode.CapabilityUnavailable)]
    [DataRow(ImageDescriptionAnalysisStatus.TransientFailure,
        CaptureAnalyzerOutputStatus.Failed, AnalysisFailureCode.ProviderUnavailable)]
    [DataRow(ImageDescriptionAnalysisStatus.TerminalFailure,
        CaptureAnalyzerOutputStatus.Failed, AnalysisFailureCode.InvalidResponse)]
    public async Task Analyze_ShouldMapPerFrameImageDescriptionOutcome(
        ImageDescriptionAnalysisStatus source,
        CaptureAnalyzerOutputStatus expectedStatus,
        AnalysisFailureCode expectedCode)
    {
        ImageDescriptionAnalysisResult providerResult = source switch
        {
            ImageDescriptionAnalysisStatus.PreparationRequired =>
                ImageDescriptionAnalysisResult.PreparationRequired,
            ImageDescriptionAnalysisStatus.Unsupported =>
                ImageDescriptionAnalysisResult.Unsupported,
            ImageDescriptionAnalysisStatus.Disabled => ImageDescriptionAnalysisResult.Disabled,
            ImageDescriptionAnalysisStatus.TransientFailure =>
                ImageDescriptionAnalysisResult.TransientFailure,
            ImageDescriptionAnalysisStatus.TerminalFailure =>
                ImageDescriptionAnalysisResult.TerminalFailure,
            _ => throw new ArgumentOutOfRangeException(nameof(source)),
        };
        var analyzer = CreateAnalyzer([providerResult]);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        Assert.AreEqual(expectedStatus, output.Status);
        Assert.AreEqual(expectedCode, output.Failure?.Code);
    }

    [TestMethod]
    public async Task Analyze_ShouldDiscardInvalidCheckpointAndRestartAtBeginning()
    {
        var checkpoint = new MemoryCheckpoint();
        await checkpoint.WriteAsync("invalid"u8.ToArray());
        var frames = new StubFrameSource((0, 0, 15));
        var analyzer = new WindowsVideoDescriptionTrackAnalyzer(
            frames,
            new QueueDescriptionService(
                [ImageDescriptionAnalysisResult.Succeeded("Fresh description.")]));

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(
            CreateRequest(analyzer, checkpoint));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Succeeded, output.Status);
        Assert.AreEqual(TimeSpan.Zero, frames.RequestedStartTime);
        Assert.AreEqual(1, checkpoint.ClearCount);
        Assert.AreEqual("Fresh description.",
            ((VideoDescriptionTrackV1)output.Payload!).FullText);
    }

    private static WindowsVideoDescriptionTrackAnalyzer CreateAnalyzer(
        IEnumerable<ImageDescriptionAnalysisResult> results)
    {
        return new WindowsVideoDescriptionTrackAnalyzer(
            new StubFrameSource((0, 0, 15)),
            new QueueDescriptionService(results));
    }

    private static CaptureAnalysisRequest CreateRequest(
        WindowsVideoDescriptionTrackAnalyzer analyzer,
        ICaptureAnalyzerCheckpoint? checkpoint = null)
    {
        return new CaptureAnalysisRequest(
            analyzer.Descriptor,
            Purpose,
            AnalysisProcessingPolicy.LocalOnly(Purpose),
            new MemoryVideoSource([1, 2, 3, 4]),
            checkpoint: checkpoint);
    }

    private sealed class StubFrameSource(
        params (long Index, double Start, double End)[] frames)
        : IVideoAnalysisFrameSource
    {
        public TimeSpan? RequestedInterval { get; private set; }

        public int? RequestedMaximumSampleCount { get; private set; }

        public TimeSpan? RequestedStartTime { get; private set; }

        public IAsyncEnumerable<VideoAnalysisFrame> ReadFramesAsync(
            Stream video,
            CancellationToken cancellationToken = default) =>
            ReadFramesCore(TimeSpan.Zero, cancellationToken);

        public IAsyncEnumerable<VideoAnalysisFrame> ReadSampledFramesAsync(
            Stream video,
            TimeSpan preferredInterval,
            int maximumSampleCount,
            TimeSpan startTime,
            CancellationToken cancellationToken = default)
        {
            RequestedInterval = preferredInterval;
            RequestedMaximumSampleCount = maximumSampleCount;
            RequestedStartTime = startTime;
            return ReadFramesCore(startTime, cancellationToken);
        }

        private async IAsyncEnumerable<VideoAnalysisFrame> ReadFramesCore(
            TimeSpan startTime,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            foreach ((long index, double start, double end) in frames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TimeSpan.FromSeconds(start) < startTime)
                {
                    continue;
                }

                yield return new VideoAnalysisFrame(
                    index,
                    TimeSpan.FromSeconds(start),
                    TimeSpan.FromSeconds(end),
                    new MemoryStream([(byte)index], writable: false));
            }
        }
    }

    private sealed class QueueDescriptionService : IImageDescriptionAnalysisService
    {
        private readonly Queue<ImageDescriptionAnalysisResult> _results;
        private readonly ImageDescriptionReadyState _readyState;

        public QueueDescriptionService(
            IEnumerable<ImageDescriptionAnalysisResult> results,
            ImageDescriptionReadyState readyState = ImageDescriptionReadyState.Ready)
        {
            _results = new Queue<ImageDescriptionAnalysisResult>(results);
            _readyState = readyState;
        }

        public ImageDescriptionModelDescriptor ModelDescriptor { get; } = new(
            "microsoft-windows",
            "windows-app-sdk-image-description",
            "model-1",
            "windows-app-sdk-ai",
            "2.3",
            "2.3");

        public ImageDescriptionReadyState GetReadyState() => _readyState;

        public Task<ImageDescriptionAnalysisPreparationResult> PrepareAnalysisAsync(
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ImageDescriptionAnalysisPreparationResult.Succeeded);

        public Task<ImageDescriptionAnalysisResult> DescribeAnalysisAsync(
            Stream sourceImage,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_results.Count == 0
                ? ImageDescriptionAnalysisResult.TerminalFailure
                : _results.Dequeue());
        }
    }

    private sealed class MemoryCheckpoint : ICaptureAnalyzerCheckpoint
    {
        public ReadOnlyMemory<byte>? Payload { get; private set; }

        public int ClearCount { get; private set; }

        public ValueTask<ReadOnlyMemory<byte>?> ReadAsync(
            CancellationToken cancellationToken = default) => ValueTask.FromResult(Payload);

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> payload,
            CancellationToken cancellationToken = default)
        {
            Payload = payload.ToArray();
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        {
            Payload = null;
            ClearCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MemoryVideoSource(byte[] bytes) : ICaptureAnalysisSource
    {
        public CaptureId CaptureId { get; } = CaptureId.New();

        public CaptureMediaKind MediaKind => CaptureMediaKind.Video;

        public SourceRevision SourceRevision { get; } = new(
            bytes.LongLength,
            new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero),
            ContentFingerprint.Sha256(Convert.ToHexStringLower(SHA256.HashData(bytes))));

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stream stream = new MemoryStream(bytes, writable: false);
            return ValueTask.FromResult(stream);
        }
    }
}
