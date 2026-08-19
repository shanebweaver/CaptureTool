using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Media;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows.Analyzers;
using CaptureTool.Infrastructure.Analysis.Windows.Media;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace CaptureTool.Infrastructure.Analysis.Windows.Tests.Analyzers;

[TestClass]
public sealed class WindowsVideoOcrTrackAnalyzerTests
{
    private static readonly AnalysisPurpose Purpose = new("capture-memory-search", 1);

    [TestMethod]
    public void Descriptor_ShouldDeclareOnDeviceVideoOcrAndBoundedFrameProcessing()
    {
        var analyzer = CreateAnalyzer([]);
        CaptureAnalyzerDescriptor descriptor = analyzer.Descriptor;

        Assert.AreEqual(AnalysisCapabilities.VideoOcrTrackV1, descriptor.Capability);
        CollectionAssert.AreEqual(
            new[] { CaptureMediaKind.Video },
            descriptor.SupportedMediaKinds.ToArray());
        Assert.AreEqual(ProcessingBoundary.OnDevice, descriptor.ProcessingBoundary);
        Assert.AreEqual(CaptureAnalyzerWorkloadClass.AiIntensive, descriptor.WorkloadClass);
        Assert.AreEqual("windows-video-frame-ocr", descriptor.Identity.AnalyzerId);
        Assert.AreEqual("microsoft-windows", descriptor.Identity.ProviderId);
        Assert.AreEqual("windows-media-ocr", descriptor.Identity.ModelId);
        Assert.IsNotNull(descriptor.Identity.ConfigurationFingerprint);
    }

    [TestMethod]
    public async Task Analyze_ShouldOcrSampledFramesCoalesceAdjacentTextAndPreserveTimecodes()
    {
        TextExtractionAnalysisResult[] results =
        [
            Success("  Build cafe\u0301  "),
            Success("Build cafe\u0301"),
            Success(string.Empty),
            Success("Build cafe\u0301"),
        ];
        var frames = new StubFrameSource(
            (0, 0, 1),
            (1, 1, 2),
            (2, 2, 3),
            (3, 3, 4));
        var text = new QueueTextExtractionService(results);
        var analyzer = new WindowsVideoOcrTrackAnalyzer(frames, text);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Succeeded, output.Status);
        var payload = (VideoOcrTrackV1)output.Payload!;
        Assert.AreEqual("Build caf\u00e9\nBuild caf\u00e9", payload.FullText);
        Assert.HasCount(2, payload.Observations);
        Assert.AreEqual(TimeSpan.Zero, payload.Observations[0].StartTime);
        Assert.AreEqual(TimeSpan.FromSeconds(2), payload.Observations[0].EndTime);
        Assert.AreEqual(TimeSpan.FromSeconds(3), payload.Observations[1].StartTime);
        Assert.AreEqual(TimeSpan.FromSeconds(4), payload.Observations[1].EndTime);
        Assert.AreEqual(4, text.CallCount);
        Assert.AreEqual(TimeSpan.FromSeconds(1), frames.RequestedPreferredInterval);
        Assert.AreEqual(
            WindowsVideoAnalysisFrameSource.MaximumSampledFrameCount,
            frames.RequestedMaximumSampleCount);
        Assert.AreEqual(TimeSpan.Zero, frames.RequestedStartTime);
    }

    [TestMethod]
    [DataRow(TextExtractionAnalysisStatus.Unavailable, CaptureAnalyzerOutputStatus.Unsupported,
        AnalysisFailureCode.CapabilityUnavailable, AnalysisFailureDisposition.Terminal)]
    [DataRow(TextExtractionAnalysisStatus.TransientFailure, CaptureAnalyzerOutputStatus.Failed,
        AnalysisFailureCode.ProviderUnavailable, AnalysisFailureDisposition.Transient)]
    [DataRow(TextExtractionAnalysisStatus.TerminalFailure, CaptureAnalyzerOutputStatus.Failed,
        AnalysisFailureCode.InvalidResponse, AnalysisFailureDisposition.Terminal)]
    [DataRow(TextExtractionAnalysisStatus.Cancelled, CaptureAnalyzerOutputStatus.Cancelled,
        AnalysisFailureCode.Unknown, AnalysisFailureDisposition.Unknown)]
    public async Task Analyze_ShouldMapPerFrameOcrOutcome(
        TextExtractionAnalysisStatus sourceStatus,
        CaptureAnalyzerOutputStatus expectedStatus,
        AnalysisFailureCode expectedCode,
        AnalysisFailureDisposition expectedDisposition)
    {
        TextExtractionAnalysisResult result = sourceStatus switch
        {
            TextExtractionAnalysisStatus.Unavailable => TextExtractionAnalysisResult.Unavailable,
            TextExtractionAnalysisStatus.TransientFailure => TextExtractionAnalysisResult.TransientFailure,
            TextExtractionAnalysisStatus.TerminalFailure => TextExtractionAnalysisResult.TerminalFailure,
            TextExtractionAnalysisStatus.Cancelled => TextExtractionAnalysisResult.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(sourceStatus)),
        };
        var analyzer = CreateAnalyzer([result]);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        Assert.AreEqual(expectedStatus, output.Status);
        Assert.AreEqual(expectedCode, output.Failure?.Code ?? AnalysisFailureCode.Unknown);
        Assert.AreEqual(
            expectedDisposition,
            output.Failure?.Disposition ?? AnalysisFailureDisposition.Unknown);
    }

    [TestMethod]
    public async Task Analyze_WhenFrameLimitIsUnsupported_ShouldReturnTerminalUnsupported()
    {
        var analyzer = new WindowsVideoOcrTrackAnalyzer(
            new StubFrameSource(throwUnsupported: true),
            new QueueTextExtractionService([]));

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Unsupported, output.Status);
        Assert.AreEqual(AnalysisFailureCode.CapabilityUnavailable, output.Failure?.Code);
    }

    [TestMethod]
    public async Task Analyze_ShouldResumeFromProtectedAnalyzerCheckpoint()
    {
        (long, double, double)[] frameDefinitions = Enumerable.Range(0, 31)
            .Select(index => ((long)index, (double)index, index + 1d))
            .ToArray();
        var checkpoint = new MemoryCheckpoint();
        var firstFrames = new StubFrameSource(frameDefinitions);
        var firstText = new QueueTextExtractionService(
            Enumerable.Repeat(Success("persistent text"), 30)
                .Append(TextExtractionAnalysisResult.Cancelled));
        var first = new WindowsVideoOcrTrackAnalyzer(firstFrames, firstText);

        CaptureAnalyzerOutput interrupted = await first.AnalyzeAsync(
            CreateRequest(first, checkpoint));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Cancelled, interrupted.Status);
        Assert.IsTrue(checkpoint.Payload.HasValue);
        var resumedFrames = new StubFrameSource(frameDefinitions);
        var resumedText = new QueueTextExtractionService([Success("persistent text")]);
        var resumed = new WindowsVideoOcrTrackAnalyzer(resumedFrames, resumedText);

        CaptureAnalyzerOutput completed = await resumed.AnalyzeAsync(
            CreateRequest(resumed, checkpoint));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Succeeded, completed.Status);
        Assert.AreEqual(TimeSpan.FromSeconds(30), resumedFrames.RequestedStartTime);
        Assert.AreEqual(1, resumedText.CallCount);
        var payload = (VideoOcrTrackV1)completed.Payload!;
        Assert.HasCount(1, payload.Observations);
        Assert.AreEqual(TimeSpan.Zero, payload.Observations[0].StartTime);
        Assert.AreEqual(TimeSpan.FromSeconds(31), payload.Observations[0].EndTime);

        var uninterrupted = new WindowsVideoOcrTrackAnalyzer(
            new StubFrameSource(frameDefinitions),
            new QueueTextExtractionService(
                Enumerable.Repeat(Success("persistent text"), 31)));
        CaptureAnalyzerOutput uninterruptedOutput = await uninterrupted.AnalyzeAsync(
            CreateRequest(uninterrupted));
        Assert.IsTrue(payload.IsEquivalentTo(uninterruptedOutput.Payload!));
    }

    [TestMethod]
    public async Task Analyze_ShouldDiscardInvalidCheckpointAndRestartAtFrameZero()
    {
        var checkpoint = new MemoryCheckpoint();
        byte[] invalid = Encoding.UTF8.GetBytes($$"""
            {
              "schemaVersion": 2,
              "adapterVersion": "{{WindowsVideoOcrTrackAnalyzer.AdapterVersion}}",
              "nextSampleTicks": 20000000,
              "observations": [
                { "text": "first", "startTicks": 0, "endTicks": 20000000 },
                { "text": "overlap", "startTicks": 10000000, "endTicks": 30000000 }
              ],
              "activeText": null,
              "activeStartTicks": 0,
              "activeEndTicks": 0
            }
            """);
        await checkpoint.WriteAsync(invalid);
        var frames = new StubFrameSource((0, 0, 1));
        var analyzer = new WindowsVideoOcrTrackAnalyzer(
            frames,
            new QueueTextExtractionService([Success("fresh")]));

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(
            CreateRequest(analyzer, checkpoint));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Succeeded, output.Status);
        Assert.AreEqual(TimeSpan.Zero, frames.RequestedStartTime);
        Assert.AreEqual(1, checkpoint.ClearCount);
        Assert.IsFalse(checkpoint.Payload.HasValue);
        Assert.AreEqual("fresh", ((VideoOcrTrackV1)output.Payload!).FullText);
    }

    [TestMethod]
    [DataRow(TextExtractionReadyState.Ready, CaptureAnalyzerAvailabilityStatus.Available)]
    [DataRow(TextExtractionReadyState.PreparationNeeded,
        CaptureAnalyzerAvailabilityStatus.PreparationRequired)]
    [DataRow(TextExtractionReadyState.Disabled, CaptureAnalyzerAvailabilityStatus.Disabled)]
    [DataRow(TextExtractionReadyState.NotSupported, CaptureAnalyzerAvailabilityStatus.Unsupported)]
    [DataRow(TextExtractionReadyState.Unknown,
        CaptureAnalyzerAvailabilityStatus.TemporarilyUnavailable)]
    public async Task Availability_ShouldMapOcrReadyState(
        TextExtractionReadyState readyState,
        CaptureAnalyzerAvailabilityStatus expected)
    {
        var analyzer = new WindowsVideoOcrTrackAnalyzer(
            new StubFrameSource(),
            new QueueTextExtractionService([], readyState));
        var request = new CaptureAnalyzerAvailabilityRequest(
            analyzer.Descriptor,
            CaptureMediaKind.Video,
            sourceLength: 4,
            Purpose,
            AnalysisProcessingPolicy.LocalOnly(Purpose));

        CaptureAnalyzerAvailability availability = await analyzer.GetAvailabilityAsync(request);

        Assert.AreEqual(expected, availability.Status);
    }

    private static WindowsVideoOcrTrackAnalyzer CreateAnalyzer(
        IReadOnlyList<TextExtractionAnalysisResult> results)
    {
        return new WindowsVideoOcrTrackAnalyzer(
            new StubFrameSource((0, 0, 1)),
            new QueueTextExtractionService(results));
    }

    private static TextExtractionAnalysisResult Success(string text)
    {
        return TextExtractionAnalysisResult.Succeeded(new TextExtractionAnalysisDocument(
            new TextExtractionRasterSize(64, 32),
            text,
            [],
            []));
    }

    private static CaptureAnalysisRequest CreateRequest(
        WindowsVideoOcrTrackAnalyzer analyzer,
        ICaptureAnalyzerCheckpoint? checkpoint = null)
    {
        return new CaptureAnalysisRequest(
            analyzer.Descriptor,
            Purpose,
            AnalysisProcessingPolicy.LocalOnly(Purpose),
            new MemoryVideoSource([1, 2, 3, 4]),
            checkpoint: checkpoint);
    }

    private sealed class StubFrameSource : IVideoAnalysisFrameSource
    {
        private readonly (long Index, double Start, double End)[] _frames;
        private readonly bool _throwUnsupported;

        public StubFrameSource(
            params (long Index, double Start, double End)[] frames)
            : this(false, frames)
        {
        }

        public StubFrameSource(
            bool throwUnsupported,
            params (long Index, double Start, double End)[] frames)
        {
            _throwUnsupported = throwUnsupported;
            _frames = frames;
        }

        public TimeSpan? RequestedPreferredInterval { get; private set; }

        public int? RequestedMaximumSampleCount { get; private set; }

        public TimeSpan? RequestedStartTime { get; private set; }

        public IAsyncEnumerable<VideoAnalysisFrame> ReadFramesAsync(
            Stream video,
            CancellationToken cancellationToken = default)
        {
            return ReadFramesCore(video, TimeSpan.Zero, cancellationToken);
        }

        public IAsyncEnumerable<VideoAnalysisFrame> ReadSampledFramesAsync(
            Stream video,
            TimeSpan preferredInterval,
            int maximumSampleCount,
            TimeSpan startTime,
            CancellationToken cancellationToken = default)
        {
            RequestedPreferredInterval = preferredInterval;
            RequestedMaximumSampleCount = maximumSampleCount;
            RequestedStartTime = startTime;
            return ReadFramesCore(video, startTime, cancellationToken);
        }

        private async IAsyncEnumerable<VideoAnalysisFrame> ReadFramesCore(
            Stream video,
            TimeSpan startTime,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            if (_throwUnsupported)
            {
                throw new NotSupportedException();
            }

            foreach ((long index, double start, double end) in _frames)
            {
                if (TimeSpan.FromSeconds(start) < startTime)
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                yield return new VideoAnalysisFrame(
                    index,
                    TimeSpan.FromSeconds(start),
                    TimeSpan.FromSeconds(end),
                    new MemoryStream([(byte)index], writable: false));
            }
        }
    }

    private sealed class MemoryCheckpoint : ICaptureAnalyzerCheckpoint
    {
        public ReadOnlyMemory<byte>? Payload { get; private set; }

        public int ClearCount { get; private set; }

        public ValueTask<ReadOnlyMemory<byte>?> ReadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Payload);
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> payload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Payload = payload.ToArray();
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Payload = null;
            ClearCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class QueueTextExtractionService : ITextExtractionAnalysisService
    {
        private readonly Queue<TextExtractionAnalysisResult> _results;
        private readonly TextExtractionReadyState _readyState;

        public QueueTextExtractionService(
            IEnumerable<TextExtractionAnalysisResult> results,
            TextExtractionReadyState readyState = TextExtractionReadyState.Ready)
        {
            _results = new Queue<TextExtractionAnalysisResult>(results);
            _readyState = readyState;
        }

        public int CallCount { get; private set; }

        public TextExtractionModelDescriptor ModelDescriptor { get; } = new(
            "microsoft-windows",
            "windows-media-ocr",
            ModelVersion: null,
            "windows-media-ocr",
            RuntimeVersion: null);

        public TextExtractionReadyState GetReadyState() => _readyState;

        public Task<TextExtractionAnalysisResult> ExtractAnalysisAsync(
            Stream sourceImage,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(_results.Count == 0
                ? TextExtractionAnalysisResult.TerminalFailure
                : _results.Dequeue());
        }
    }

    private sealed class MemoryVideoSource : ICaptureAnalysisSource
    {
        private readonly byte[] _bytes;

        public MemoryVideoSource(byte[] bytes)
        {
            _bytes = bytes;
            DateTimeOffset timestamp = new(2026, 8, 11, 0, 0, 0, TimeSpan.Zero);
            SourceRevision = new(
                bytes.LongLength,
                timestamp,
                ContentFingerprint.Sha256(Convert.ToHexStringLower(SHA256.HashData(bytes))));
        }

        public CaptureId CaptureId { get; } = CaptureId.New();

        public CaptureMediaKind MediaKind => CaptureMediaKind.Video;

        public SourceRevision SourceRevision { get; }

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stream stream = new MemoryStream(_bytes, writable: false);
            return ValueTask.FromResult(stream);
        }
    }
}
