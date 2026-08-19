using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Media;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows.Analyzers;
using Microsoft.Windows.AI;
using System.Security.Cryptography;

namespace CaptureTool.Infrastructure.Analysis.Windows.Tests.Analyzers;

[TestClass]
public sealed class WindowsAiSpeechTranscriptAnalyzerTests
{
    private static readonly AnalysisPurpose Purpose = new("capture-memory-search", 1);

    [TestMethod]
    [DataRow(AIFeatureReadyState.Ready, WindowsAiSpeechReadyState.Ready)]
    [DataRow(AIFeatureReadyState.NotReady, WindowsAiSpeechReadyState.PreparationNeeded)]
    [DataRow(AIFeatureReadyState.DisabledByUser, WindowsAiSpeechReadyState.Disabled)]
    [DataRow(AIFeatureReadyState.NotSupportedOnCurrentSystem,
        WindowsAiSpeechReadyState.NotSupported)]
    [DataRow(AIFeatureReadyState.NotCompatibleWithSystemHardware,
        WindowsAiSpeechReadyState.NotSupported)]
    [DataRow(AIFeatureReadyState.CapabilityMissing, WindowsAiSpeechReadyState.NotSupported)]
    [DataRow(AIFeatureReadyState.OSUpdateNeeded, WindowsAiSpeechReadyState.NotSupported)]
    public void ReadyState_ShouldMapWindowsCompatibilityReasons(
        AIFeatureReadyState source,
        WindowsAiSpeechReadyState expected)
    {
        Assert.AreEqual(expected, WindowsAiSpeechRecognitionService.MapReadyState(source));
    }

    [TestMethod]
    public void Descriptor_ShouldPreferWindowsAiAndSupportVideoWhenExtractionIsAvailable()
    {
        var analyzer = new WindowsAiSpeechTranscriptAnalyzer(
            new StubSpeechService(),
            new StubVideoAudioExtractionService(VideoAudioExtractionResult.NoAudio));

        Assert.AreEqual(AnalysisCapabilities.SpeechTranscriptV1, analyzer.Descriptor.Capability);
        CollectionAssert.AreEqual(
            new[] { CaptureMediaKind.Audio, CaptureMediaKind.Video },
            analyzer.Descriptor.SupportedMediaKinds.ToArray());
        Assert.AreEqual("windows-ai-speech-transcript", analyzer.Descriptor.Identity.AnalyzerId);
        Assert.AreEqual("microsoft-windows", analyzer.Descriptor.Identity.ProviderId);
        Assert.AreEqual("windows-ai-speech-recognition", analyzer.Descriptor.Identity.ModelId);
        Assert.AreEqual(120, analyzer.Descriptor.QualityTier);
        Assert.IsTrue(analyzer.Descriptor.Requirements.HasFlag(
            CaptureAnalyzerRequirement.UserInitiatedPreparation));
    }

    [TestMethod]
    [DataRow(WindowsAiSpeechReadyState.Ready, CaptureAnalyzerAvailabilityStatus.Available)]
    [DataRow(WindowsAiSpeechReadyState.PreparationNeeded,
        CaptureAnalyzerAvailabilityStatus.PreparationRequired)]
    [DataRow(WindowsAiSpeechReadyState.NotSupported,
        CaptureAnalyzerAvailabilityStatus.Unsupported)]
    [DataRow(WindowsAiSpeechReadyState.Disabled, CaptureAnalyzerAvailabilityStatus.Disabled)]
    [DataRow(WindowsAiSpeechReadyState.Unknown,
        CaptureAnalyzerAvailabilityStatus.TemporarilyUnavailable)]
    public async Task Availability_ShouldMapWindowsAiReadyState(
        WindowsAiSpeechReadyState readyState,
        CaptureAnalyzerAvailabilityStatus expected)
    {
        var analyzer = new WindowsAiSpeechTranscriptAnalyzer(
            new StubSpeechService { ReadyState = readyState });
        var request = new CaptureAnalyzerAvailabilityRequest(
            analyzer.Descriptor,
            CaptureMediaKind.Audio,
            sourceLength: 4,
            Purpose,
            AnalysisProcessingPolicy.LocalOnly(Purpose));

        CaptureAnalyzerAvailability availability = await analyzer.GetAvailabilityAsync(request);

        Assert.AreEqual(expected, availability.Status);
    }

    [TestMethod]
    public async Task Prepare_ShouldForwardProgressAndMapSuccess()
    {
        var service = new StubSpeechService
        {
            PreparationStatus = WindowsAiSpeechPreparationStatus.Succeeded,
            PreparationProgress = [0.25, 1],
        };
        var analyzer = new WindowsAiSpeechTranscriptAnalyzer(service);
        var progress = new RecordingProgress();

        CaptureAnalyzerPreparationResult result = await analyzer.PrepareAsync(progress);

        Assert.AreEqual(CaptureAnalyzerPreparationStatus.Succeeded, result.Status);
        CollectionAssert.AreEqual(new[] { 0.25, 1d }, progress.Values.ToArray());
    }

    [TestMethod]
    public async Task AnalyzeAudio_ShouldPreserveTimestampedWindowsAiSegments()
    {
        var service = new StubSpeechService
        {
            TranscriptionResult = new(
                WindowsAiSpeechTranscriptionStatus.Succeeded,
                "First window.\nSecond window.",
                [
                    new("First window.", TimeSpan.Zero, TimeSpan.FromSeconds(15)),
                    new("Second window.", TimeSpan.FromSeconds(15),
                        TimeSpan.FromSeconds(27)),
                ]),
        };
        var analyzer = new WindowsAiSpeechTranscriptAnalyzer(service);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(
            CreateRequest(analyzer, CaptureMediaKind.Audio));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Succeeded, output.Status);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, service.ReceivedAudio);
        var payload = (SpeechTranscriptV1)output.Payload!;
        Assert.HasCount(2, payload.Segments);
        Assert.AreEqual(TimeSpan.FromSeconds(15), payload.Segments[1].StartTime);
        Assert.AreEqual(TimeSpan.FromSeconds(27), payload.Segments[1].EndTime);
    }

    [TestMethod]
    public async Task AnalyzeVideo_ShouldExtractAudioBeforeWindowsAiRecognition()
    {
        var service = new StubSpeechService
        {
            TranscriptionResult = new(
                WindowsAiSpeechTranscriptionStatus.Succeeded,
                "Video narration",
                [new("Video narration", TimeSpan.Zero, TimeSpan.FromSeconds(8))]),
        };
        var extraction = new StubVideoAudioExtractionService(
            VideoAudioExtractionResult.Succeeded(new MemoryStream([9, 8, 7], writable: false)));
        var analyzer = new WindowsAiSpeechTranscriptAnalyzer(service, extraction);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(
            CreateRequest(analyzer, CaptureMediaKind.Video));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Succeeded, output.Status);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, extraction.ReceivedVideo);
        CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, service.ReceivedAudio);
    }

    [TestMethod]
    [DataRow(WindowsAiSpeechTranscriptionStatus.PreparationRequired,
        CaptureAnalyzerOutputStatus.Failed, AnalysisFailureCode.ModelNotReady)]
    [DataRow(WindowsAiSpeechTranscriptionStatus.Unsupported,
        CaptureAnalyzerOutputStatus.Unsupported, AnalysisFailureCode.CapabilityUnavailable)]
    [DataRow(WindowsAiSpeechTranscriptionStatus.Disabled,
        CaptureAnalyzerOutputStatus.Unsupported, AnalysisFailureCode.CapabilityUnavailable)]
    [DataRow(WindowsAiSpeechTranscriptionStatus.Failed,
        CaptureAnalyzerOutputStatus.Failed, AnalysisFailureCode.ProviderUnavailable)]
    [DataRow(WindowsAiSpeechTranscriptionStatus.Unknown,
        CaptureAnalyzerOutputStatus.Failed, AnalysisFailureCode.InvalidResponse)]
    public async Task Analyze_ShouldMapBoundedWindowsAiOutcome(
        WindowsAiSpeechTranscriptionStatus source,
        CaptureAnalyzerOutputStatus expectedStatus,
        AnalysisFailureCode expectedCode)
    {
        var service = new StubSpeechService
        {
            TranscriptionResult = new(source),
        };
        var analyzer = new WindowsAiSpeechTranscriptAnalyzer(service);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(
            CreateRequest(analyzer, CaptureMediaKind.Audio));

        Assert.AreEqual(expectedStatus, output.Status);
        Assert.AreEqual(expectedCode, output.Failure?.Code);
    }

    private static CaptureAnalysisRequest CreateRequest(
        WindowsAiSpeechTranscriptAnalyzer analyzer,
        CaptureMediaKind mediaKind)
    {
        return new CaptureAnalysisRequest(
            analyzer.Descriptor,
            Purpose,
            AnalysisProcessingPolicy.LocalOnly(Purpose),
            new MemorySource(mediaKind, [1, 2, 3, 4]));
    }

    private sealed class StubSpeechService : IWindowsAiSpeechRecognitionService
    {
        public WindowsAiSpeechReadyState ReadyState { get; init; } =
            WindowsAiSpeechReadyState.Ready;

        public WindowsAiSpeechPreparationStatus PreparationStatus { get; init; } =
            WindowsAiSpeechPreparationStatus.Unsupported;

        public double[] PreparationProgress { get; init; } = [];

        public WindowsAiSpeechTranscriptionResult TranscriptionResult { get; init; } =
            new(WindowsAiSpeechTranscriptionStatus.Failed);

        public byte[] ReceivedAudio { get; private set; } = [];

        public WindowsAiSpeechReadyState GetReadyState() => ReadyState;

        public Task<WindowsAiSpeechPreparationResult> PrepareAsync(
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (double value in PreparationProgress)
            {
                progress?.Report(value);
            }

            return Task.FromResult(new WindowsAiSpeechPreparationResult(PreparationStatus));
        }

        public async Task<WindowsAiSpeechTranscriptionResult> TranscribeAsync(
            Stream audio,
            CancellationToken cancellationToken = default)
        {
            using var copy = new MemoryStream();
            await audio.CopyToAsync(copy, cancellationToken);
            ReceivedAudio = copy.ToArray();
            return TranscriptionResult;
        }
    }

    private sealed class RecordingProgress : IProgress<AnalysisCapabilityPreparationProgress>
    {
        public List<double> Values { get; } = [];

        public void Report(AnalysisCapabilityPreparationProgress value) =>
            Values.Add(value.FractionComplete);
    }

    private sealed class StubVideoAudioExtractionService(VideoAudioExtractionResult result)
        : IVideoAudioExtractionService
    {
        public byte[] ReceivedVideo { get; private set; } = [];

        public async Task<VideoAudioExtractionResult> ExtractWaveAudioAsync(
            Stream video,
            CancellationToken cancellationToken = default)
        {
            using var copy = new MemoryStream();
            await video.CopyToAsync(copy, cancellationToken);
            ReceivedVideo = copy.ToArray();
            return result;
        }
    }

    private sealed class MemorySource(CaptureMediaKind mediaKind, byte[] bytes)
        : ICaptureAnalysisSource
    {
        public CaptureId CaptureId { get; } = CaptureId.New();

        public CaptureMediaKind MediaKind { get; } = mediaKind;

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
