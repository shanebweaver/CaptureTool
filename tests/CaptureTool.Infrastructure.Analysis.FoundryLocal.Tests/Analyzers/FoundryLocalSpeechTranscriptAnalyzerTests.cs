using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Media;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Security.Cryptography;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal.Tests.Analyzers;

[TestClass]
public sealed class FoundryLocalSpeechTranscriptAnalyzerTests
{
    private static readonly AnalysisPurpose Purpose = new("capture-memory-search", 1);

    [TestMethod]
    public void Descriptor_ShouldDeclareOnDeviceAudioTranscriptAndModelPreparation()
    {
        var analyzer = CreateAnalyzer(FoundryLocalTranscriptionStatus.Failed);
        CaptureAnalyzerDescriptor descriptor = analyzer.Descriptor;

        Assert.AreEqual(AnalysisCapabilities.SpeechTranscriptV1, descriptor.Capability);
        CollectionAssert.AreEqual(
            new[] { CaptureMediaKind.Audio },
            descriptor.SupportedMediaKinds.ToArray());
        Assert.AreEqual(ProcessingBoundary.OnDevice, descriptor.ProcessingBoundary);
        Assert.AreEqual(CaptureAnalyzerDataKind.None, descriptor.DataSent);
        Assert.AreEqual("microsoft-foundry-local", descriptor.Identity.ProviderId);
        Assert.AreEqual("foundry-local-speech-transcript", descriptor.Identity.AnalyzerId);
        Assert.AreEqual("whisper-tiny", descriptor.Identity.ModelId);
        Assert.IsTrue(descriptor.Requirements.HasFlag(CaptureAnalyzerRequirement.ModelPackage));
        Assert.IsTrue(descriptor.Requirements.HasFlag(
            CaptureAnalyzerRequirement.UserInitiatedPreparation));
    }

    [TestMethod]
    public void Descriptor_WithVideoAudioExtractor_ShouldSupportAudioAndVideo()
    {
        var analyzer = new FoundryLocalSpeechTranscriptAnalyzer(
            new StubTranscriptionService(),
            new StubVideoAudioExtractionService(VideoAudioExtractionResult.NoAudio));

        CollectionAssert.AreEqual(
            new[] { CaptureMediaKind.Audio, CaptureMediaKind.Video },
            analyzer.Descriptor.SupportedMediaKinds.ToArray());
    }

    [TestMethod]
    [DataRow(FoundryLocalSpeechReadyState.Ready, CaptureAnalyzerAvailabilityStatus.Available)]
    [DataRow(FoundryLocalSpeechReadyState.PreparationNeeded,
        CaptureAnalyzerAvailabilityStatus.PreparationRequired)]
    [DataRow(FoundryLocalSpeechReadyState.NotSupported,
        CaptureAnalyzerAvailabilityStatus.Unsupported)]
    [DataRow(FoundryLocalSpeechReadyState.Unknown,
        CaptureAnalyzerAvailabilityStatus.TemporarilyUnavailable)]
    public async Task Availability_ShouldMapProviderState(
        FoundryLocalSpeechReadyState readyState,
        CaptureAnalyzerAvailabilityStatus expectedStatus)
    {
        var analyzer = new FoundryLocalSpeechTranscriptAnalyzer(
            new StubTranscriptionService { ReadyState = readyState });
        var request = new CaptureAnalyzerAvailabilityRequest(
            analyzer.Descriptor,
            CaptureMediaKind.Audio,
            sourceLength: 4,
            Purpose,
            AnalysisProcessingPolicy.LocalOnly(Purpose));

        CaptureAnalyzerAvailability availability = await analyzer.GetAvailabilityAsync(request);

        Assert.AreEqual(expectedStatus, availability.Status);
    }

    [TestMethod]
    public async Task Prepare_ShouldForwardProgressAndMapSuccess()
    {
        var service = new StubTranscriptionService
        {
            PreparationStatus = FoundryLocalSpeechPreparationStatus.Succeeded,
            PreparationProgress = [0.1, 0.7, 1],
        };
        var analyzer = new FoundryLocalSpeechTranscriptAnalyzer(service);
        var progress = new RecordingProgress();

        CaptureAnalyzerPreparationResult result = await analyzer.PrepareAsync(progress);

        Assert.AreEqual(CaptureAnalyzerPreparationStatus.Succeeded, result.Status);
        CollectionAssert.AreEqual(new[] { 0.1, 0.7, 1d }, progress.Values.ToArray());
    }

    [TestMethod]
    public void NemotronDescriptor_ShouldUseIndependentIdentityAndHigherEvaluationTier()
    {
        var analyzer = new FoundryLocalSpeechTranscriptAnalyzer(
            new StubTranscriptionService(),
            videoAudioExtraction: null,
            FoundryLocalSpeechModelConfiguration.NemotronMultilingual);

        Assert.AreEqual(
            "foundry-local-nemotron-multilingual-speech-transcript",
            analyzer.Descriptor.Identity.AnalyzerId);
        Assert.AreEqual(
            "nvidia-nemotron-3.5-asr-streaming-multilingual-0.6b",
            analyzer.Descriptor.Identity.ModelId);
        Assert.AreEqual("1.0.0", analyzer.Descriptor.Identity.AdapterVersion);
        Assert.AreEqual(50, analyzer.Descriptor.QualityTier);
    }

    [TestMethod]
    public void Descriptor_WhenAppSpeechLanguageChanges_ShouldAdvanceAnalyzerRevision()
    {
        var service = new StubTranscriptionService { LanguageHint = "en" };
        var analyzer = new FoundryLocalSpeechTranscriptAnalyzer(service);
        AnalyzerRevision englishRevision = analyzer.Descriptor.Revision;

        service.LanguageHint = "fr";

        Assert.AreNotEqual(englishRevision, analyzer.Descriptor.Revision);
    }

    [TestMethod]
    public async Task Prepare_ShouldPromoteDescriptorToExactResolvedModelProvenance()
    {
        var provenance = new FoundryLocalModelProvenance(
            "whisper-tiny",
            "whisper-tiny-winml-gpu-v4",
            "4",
            "GPU",
            "WinMLExecutionProvider",
            $"sha256:{new string('a', 64)}");
        var service = new StubTranscriptionService
        {
            PreparationStatus = FoundryLocalSpeechPreparationStatus.Succeeded,
            PreparationProvenance = provenance,
        };
        var analyzer = new FoundryLocalSpeechTranscriptAnalyzer(service);
        AnalyzerRevision unresolvedRevision = analyzer.Descriptor.Revision;

        CaptureAnalyzerPreparationResult result = await analyzer.PrepareAsync();
        AnalyzerIdentity resolved = analyzer.Descriptor.Identity;

        Assert.AreEqual(CaptureAnalyzerPreparationStatus.Succeeded, result.Status);
        Assert.AreEqual(provenance.ResolvedModelId, resolved.ModelId);
        Assert.AreEqual("4;alias=whisper-tiny", resolved.ModelVersion);
        Assert.AreEqual("2.1.0", resolved.AdapterVersion);
        Assert.AreEqual("1.2.4", resolved.RuntimeVersion);
        StringAssert.Contains(resolved.RuntimeId, "device=gpu");
        StringAssert.Contains(resolved.RuntimeId, "ep=winmlexecutionprovider");
        Assert.AreNotEqual(unresolvedRevision, resolved.Revision);
    }

    [TestMethod]
    [DataRow(FoundryLocalSpeechPreparationStatus.Unsupported,
        CaptureAnalyzerPreparationStatus.Unsupported, AnalysisFailureDisposition.Terminal)]
    [DataRow(FoundryLocalSpeechPreparationStatus.Cancelled,
        CaptureAnalyzerPreparationStatus.Cancelled, AnalysisFailureDisposition.Unknown)]
    [DataRow(FoundryLocalSpeechPreparationStatus.Failed,
        CaptureAnalyzerPreparationStatus.Failed, AnalysisFailureDisposition.Transient)]
    [DataRow(FoundryLocalSpeechPreparationStatus.Unknown,
        CaptureAnalyzerPreparationStatus.Failed, AnalysisFailureDisposition.Terminal)]
    public async Task Prepare_ShouldMapBoundedOutcome(
        FoundryLocalSpeechPreparationStatus sourceStatus,
        CaptureAnalyzerPreparationStatus expectedStatus,
        AnalysisFailureDisposition expectedDisposition)
    {
        var analyzer = new FoundryLocalSpeechTranscriptAnalyzer(
            new StubTranscriptionService { PreparationStatus = sourceStatus });

        CaptureAnalyzerPreparationResult result = await analyzer.PrepareAsync();

        Assert.AreEqual(expectedStatus, result.Status);
        Assert.AreEqual(
            expectedDisposition,
            result.Failure?.Disposition ?? AnalysisFailureDisposition.Unknown);
    }

    [TestMethod]
    public async Task AnalyzeSuccess_ShouldReturnNormalizedTranscriptAndReadAudioSource()
    {
        var service = new StubTranscriptionService
        {
            TranscriptionResult = new(
                FoundryLocalTranscriptionStatus.Succeeded,
                "  Release the cafe\u0301 build.\r\n"),
        };
        var analyzer = new FoundryLocalSpeechTranscriptAnalyzer(service);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Succeeded, output.Status);
        var payload = (SpeechTranscriptV1)output.Payload!;
        Assert.AreEqual("Release the café build.", payload.FullText);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, service.ReceivedAudio);
    }

    [TestMethod]
    public async Task AnalyzeSuccess_ShouldMapTimedSegmentsAndLanguageIntoCapabilityPayload()
    {
        var service = new StubTranscriptionService
        {
            TranscriptionResult = new(
                FoundryLocalTranscriptionStatus.Succeeded,
                "First window. Second window.",
                [
                    new FoundryLocalTranscriptionSegment(
                        "First window.",
                        TimeSpan.Zero,
                        TimeSpan.FromSeconds(15)),
                    new FoundryLocalTranscriptionSegment(
                        "Second window.",
                        TimeSpan.FromSeconds(15),
                        TimeSpan.FromSeconds(24.5)),
                ],
                "en"),
        };
        var analyzer = new FoundryLocalSpeechTranscriptAnalyzer(service);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Succeeded, output.Status);
        var payload = (SpeechTranscriptV1)output.Payload!;
        Assert.AreEqual("en", payload.LanguageTag);
        Assert.HasCount(2, payload.Segments);
        Assert.AreEqual(TimeSpan.FromSeconds(15), payload.Segments[1].StartTime);
        Assert.AreEqual(TimeSpan.FromSeconds(24.5), payload.Segments[1].EndTime);
    }

    [TestMethod]
    public async Task AnalyzeVideo_ShouldExtractWaveAndPreserveTranscriptTimecodes()
    {
        var transcription = new StubTranscriptionService
        {
            TranscriptionResult = new(
                FoundryLocalTranscriptionStatus.Succeeded,
                "Video narration",
                [new FoundryLocalTranscriptionSegment(
                    "Video narration",
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromSeconds(19))],
                "en-US"),
        };
        var extraction = new StubVideoAudioExtractionService(
            VideoAudioExtractionResult.Succeeded(
                new MemoryStream([9, 8, 7], writable: false)));
        var analyzer = new FoundryLocalSpeechTranscriptAnalyzer(transcription, extraction);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(
            CreateVideoRequest(analyzer));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Succeeded, output.Status);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, extraction.ReceivedVideo);
        CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, transcription.ReceivedAudio);
        var payload = (SpeechTranscriptV1)output.Payload!;
        Assert.AreEqual(TimeSpan.FromSeconds(15), payload.Segments[0].StartTime);
        Assert.AreEqual(TimeSpan.FromSeconds(19), payload.Segments[0].EndTime);
    }

    [TestMethod]
    [DataRow(VideoAudioExtractionStatus.NoAudio, CaptureAnalyzerOutputStatus.Unsupported,
        AnalysisFailureCode.CapabilityUnavailable, AnalysisFailureDisposition.Terminal)]
    [DataRow(VideoAudioExtractionStatus.Unsupported, CaptureAnalyzerOutputStatus.Unsupported,
        AnalysisFailureCode.CapabilityUnavailable, AnalysisFailureDisposition.Terminal)]
    [DataRow(VideoAudioExtractionStatus.Cancelled, CaptureAnalyzerOutputStatus.Cancelled,
        AnalysisFailureCode.Unknown, AnalysisFailureDisposition.Unknown)]
    [DataRow(VideoAudioExtractionStatus.Failed, CaptureAnalyzerOutputStatus.Failed,
        AnalysisFailureCode.ProviderUnavailable, AnalysisFailureDisposition.Transient)]
    public async Task AnalyzeVideo_ShouldMapAudioExtractionOutcome(
        VideoAudioExtractionStatus extractionStatus,
        CaptureAnalyzerOutputStatus expectedStatus,
        AnalysisFailureCode expectedCode,
        AnalysisFailureDisposition expectedDisposition)
    {
        VideoAudioExtractionResult extractionResult = extractionStatus switch
        {
            VideoAudioExtractionStatus.NoAudio => VideoAudioExtractionResult.NoAudio,
            VideoAudioExtractionStatus.Unsupported => VideoAudioExtractionResult.Unsupported,
            VideoAudioExtractionStatus.Cancelled => VideoAudioExtractionResult.Cancelled,
            VideoAudioExtractionStatus.Failed => VideoAudioExtractionResult.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(extractionStatus)),
        };
        var analyzer = new FoundryLocalSpeechTranscriptAnalyzer(
            new StubTranscriptionService(),
            new StubVideoAudioExtractionService(extractionResult));

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateVideoRequest(analyzer));

        Assert.AreEqual(expectedStatus, output.Status);
        Assert.AreEqual(expectedCode, output.Failure?.Code ?? AnalysisFailureCode.Unknown);
        Assert.AreEqual(
            expectedDisposition,
            output.Failure?.Disposition ?? AnalysisFailureDisposition.Unknown);
    }

    [TestMethod]
    [DataRow(FoundryLocalTranscriptionStatus.PreparationRequired,
        CaptureAnalyzerOutputStatus.Failed, AnalysisFailureCode.ModelNotReady,
        AnalysisFailureDisposition.Transient)]
    [DataRow(FoundryLocalTranscriptionStatus.Unsupported,
        CaptureAnalyzerOutputStatus.Unsupported, AnalysisFailureCode.CapabilityUnavailable,
        AnalysisFailureDisposition.Terminal)]
    [DataRow(FoundryLocalTranscriptionStatus.Failed,
        CaptureAnalyzerOutputStatus.Failed, AnalysisFailureCode.ProviderUnavailable,
        AnalysisFailureDisposition.Transient)]
    [DataRow(FoundryLocalTranscriptionStatus.Unknown,
        CaptureAnalyzerOutputStatus.Failed, AnalysisFailureCode.InvalidResponse,
        AnalysisFailureDisposition.Terminal)]
    public async Task Analyze_ShouldMapBoundedOutcome(
        FoundryLocalTranscriptionStatus sourceStatus,
        CaptureAnalyzerOutputStatus expectedStatus,
        AnalysisFailureCode expectedCode,
        AnalysisFailureDisposition expectedDisposition)
    {
        var analyzer = CreateAnalyzer(sourceStatus);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        Assert.AreEqual(expectedStatus, output.Status);
        Assert.AreEqual(expectedCode, output.Failure?.Code);
        Assert.AreEqual(expectedDisposition, output.Failure?.Disposition);
    }

    [TestMethod]
    public async Task AnalyzeCancelled_ShouldReturnCancelled()
    {
        var analyzer = CreateAnalyzer(FoundryLocalTranscriptionStatus.Cancelled);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Cancelled, output.Status);
        Assert.IsNull(output.Payload);
        Assert.IsNull(output.Failure);
    }

    [TestMethod]
    public async Task AnalyzeOversizedTranscript_ShouldReturnInvalidResponse()
    {
        var service = new StubTranscriptionService
        {
            TranscriptionResult = new(
                FoundryLocalTranscriptionStatus.Succeeded,
                new string('x', 1_000_001)),
        };
        var analyzer = new FoundryLocalSpeechTranscriptAnalyzer(service);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Failed, output.Status);
        Assert.AreEqual(AnalysisFailureCode.InvalidResponse, output.Failure?.Code);
    }

    private static FoundryLocalSpeechTranscriptAnalyzer CreateAnalyzer(
        FoundryLocalTranscriptionStatus status)
    {
        return new FoundryLocalSpeechTranscriptAnalyzer(new StubTranscriptionService
        {
            TranscriptionResult = new(status),
        });
    }

    private static CaptureAnalysisRequest CreateRequest(
        FoundryLocalSpeechTranscriptAnalyzer analyzer)
    {
        return new CaptureAnalysisRequest(
            analyzer.Descriptor,
            Purpose,
            AnalysisProcessingPolicy.LocalOnly(Purpose),
            new MemoryAudioSource([1, 2, 3, 4]));
    }

    private static CaptureAnalysisRequest CreateVideoRequest(
        FoundryLocalSpeechTranscriptAnalyzer analyzer)
    {
        return new CaptureAnalysisRequest(
            analyzer.Descriptor,
            Purpose,
            AnalysisProcessingPolicy.LocalOnly(Purpose),
            new MemoryVideoSource([1, 2, 3, 4]));
    }

    private sealed class StubTranscriptionService : IFoundryLocalSpeechTranscriptionService
    {
        public FoundryLocalModelProvenance? ModelProvenance { get; set; }

        public string LanguageHint { get; set; } = "en";

        public FoundryLocalModelProvenance? PreparationProvenance { get; init; }

        public FoundryLocalSpeechReadyState ReadyState { get; init; } =
            FoundryLocalSpeechReadyState.Ready;

        public FoundryLocalSpeechPreparationStatus PreparationStatus { get; init; } =
            FoundryLocalSpeechPreparationStatus.Unsupported;

        public double[] PreparationProgress { get; init; } = [];

        public FoundryLocalTranscriptionResult TranscriptionResult { get; init; } =
            new(FoundryLocalTranscriptionStatus.Failed);

        public byte[] ReceivedAudio { get; private set; } = [];

        public FoundryLocalSpeechReadyState GetReadyState() => ReadyState;

        public Task<FoundryLocalSpeechPreparationResult> PrepareAsync(
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (double value in PreparationProgress)
            {
                progress?.Report(value);
            }

            ModelProvenance = PreparationProvenance;

            return Task.FromResult(new FoundryLocalSpeechPreparationResult(PreparationStatus));
        }

        public async Task<FoundryLocalTranscriptionResult> TranscribeAsync(
            Stream audio,
            CancellationToken cancellationToken = default)
        {
            using var copy = new MemoryStream();
            await audio.CopyToAsync(copy, cancellationToken);
            ReceivedAudio = copy.ToArray();
            return TranscriptionResult;
        }

        public Task ReleaseModelAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingProgress : IProgress<AnalysisCapabilityPreparationProgress>
    {
        public List<double> Values { get; } = [];

        public void Report(AnalysisCapabilityPreparationProgress value)
        {
            Values.Add(value.FractionComplete);
        }
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

    private sealed class MemoryAudioSource : ICaptureAnalysisSource
    {
        private readonly byte[] _bytes;

        public MemoryAudioSource(byte[] bytes)
        {
            _bytes = bytes;
            DateTimeOffset timestamp = new(2026, 8, 11, 0, 0, 0, TimeSpan.Zero);
            SourceRevision = new(
                bytes.LongLength,
                timestamp,
                ContentFingerprint.Sha256(Convert.ToHexStringLower(SHA256.HashData(bytes))));
        }

        public CaptureId CaptureId { get; } = CaptureId.New();

        public CaptureMediaKind MediaKind => CaptureMediaKind.Audio;

        public SourceRevision SourceRevision { get; }

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stream stream = new MemoryStream(_bytes, writable: false);
            return ValueTask.FromResult(stream);
        }
    }

    private sealed class MemoryVideoSource(byte[] bytes) : ICaptureAnalysisSource
    {
        public CaptureId CaptureId { get; } = CaptureId.New();

        public CaptureMediaKind MediaKind => CaptureMediaKind.Video;

        public SourceRevision SourceRevision { get; } = new(
            bytes.LongLength,
            new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero),
            ContentFingerprint.Sha256(Convert.ToHexStringLower(SHA256.HashData(bytes))));

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stream stream = new MemoryStream(bytes, writable: false);
            return ValueTask.FromResult(stream);
        }
    }
}
