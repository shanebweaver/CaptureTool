using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows.Analyzers;
using System.Security.Cryptography;

namespace CaptureTool.Infrastructure.Analysis.Windows.Tests.Analyzers;

[TestClass]
public sealed class WindowsOcrDocumentAnalyzerTests
{
    private static readonly AnalysisPurpose Purpose = new("capture-memory-search", 1);

    [TestMethod]
    public void Descriptor_ShouldRecordCompleteWindowsOcrProvenance()
    {
        var analyzer = CreateAnalyzer(TextExtractionAnalysisResult.Unavailable);
        AnalyzerIdentity identity = analyzer.Descriptor.Identity;

        CaptureAnalyzerContractAssertions.AssertOnDeviceImageAnalyzer(
            analyzer,
            AnalysisCapabilities.OcrDocumentV1);
        Assert.AreEqual(AnalysisCapabilities.OcrDocumentV1, analyzer.Descriptor.Capability);
        Assert.AreEqual(ProcessingBoundary.OnDevice, analyzer.Descriptor.ProcessingBoundary);
        Assert.AreEqual(CaptureAnalyzerDataKind.None, analyzer.Descriptor.DataSent);
        Assert.AreEqual(CaptureAnalyzerWorkloadClass.Lightweight, analyzer.Descriptor.WorkloadClass);
        Assert.AreEqual("microsoft-windows", identity.ProviderId);
        Assert.AreEqual("windows-media-ocr", identity.ModelId);
        Assert.AreEqual(AnalyzerIdentity.Unknown, identity.ModelVersion);
        Assert.AreEqual(WindowsOcrDocumentAnalyzer.AdapterVersion, identity.AdapterVersion);
        Assert.AreEqual("windows-media-ocr", identity.RuntimeId);
        Assert.AreEqual(AnalyzerIdentity.Unknown, identity.RuntimeVersion);
        CollectionAssert.AreEqual(
            new[] { CaptureMediaKind.Image },
            analyzer.Descriptor.SupportedMediaKinds.ToArray());
    }

    [TestMethod]
    public async Task AnalyzeMultilineUnicodeFixture_ShouldNormalizeTextLanguagesOrderAndBounds()
    {
        var document = new TextExtractionAnalysisDocument(
            new TextExtractionRasterSize(200, 100),
            "Cafe\u0301\r\n世界\r\n",
            [
                new TextExtractionLanguageCandidate("ja-JP", Order: 1, Confidence: null),
                new TextExtractionLanguageCandidate("en-US", Order: 0, Confidence: 0.75),
            ],
            [
                new TextExtractionAnalysisRegion(
                    new TextExtractionPixelBounds(10, 50, 60, 20),
                    Order: 1,
                    [new TextExtractionAnalysisLine(
                        "世界",
                        new TextExtractionPixelBounds(10, 50, 60, 20),
                        Order: 0,
                        [new TextExtractionAnalysisWord(
                            "世界",
                            new TextExtractionPixelBounds(10, 50, 60, 20),
                            Order: 0)])]),
                new TextExtractionAnalysisRegion(
                    new TextExtractionPixelBounds(5, 5, 100, 35),
                    Order: 0,
                    [
                        new TextExtractionAnalysisLine(
                            "second",
                            new TextExtractionPixelBounds(5, 25, 60, 15),
                            Order: 1,
                            [new TextExtractionAnalysisWord(
                                "second",
                                new TextExtractionPixelBounds(5, 25, 60, 15),
                                Order: 0)]),
                        new TextExtractionAnalysisLine(
                            "Café first",
                            new TextExtractionPixelBounds(5, 5, 100, 15),
                            Order: 0,
                            [
                                new TextExtractionAnalysisWord(
                                    "first",
                                    new TextExtractionPixelBounds(55, 5, 50, 15),
                                    Order: 1),
                                new TextExtractionAnalysisWord(
                                    "Cafe\u0301",
                                    new TextExtractionPixelBounds(5, 5, 45, 15),
                                    Order: 0,
                                    Confidence: 0.9),
                            ]),
                    ]),
            ]);
        var analyzer = CreateAnalyzer(TextExtractionAnalysisResult.Succeeded(document));

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        CaptureAnalyzerContractAssertions.AssertCompatibleSuccess(analyzer, output);
        var payload = (OcrDocumentV1)output.Payload!;
        Assert.AreEqual(new PixelSize(200, 100), payload.RasterSize);
        Assert.AreEqual("Café\n世界", payload.FullText);
        CollectionAssert.AreEqual(
            new[] { "en-US", "ja-JP" },
            payload.Languages.Select(language => language.LanguageTag).ToArray());
        Assert.AreEqual(0.75, payload.Languages[0].Confidence);
        Assert.HasCount(2, payload.Regions);
        Assert.AreEqual("Café first", payload.Regions[0].Lines[0].Text);
        CollectionAssert.AreEqual(
            new[] { "Café", "first" },
            payload.Regions[0].Lines[0].Words.Select(word => word.Text).ToArray());
        Assert.AreEqual(new PixelRect(5, 5, 45, 15), payload.Regions[0].Lines[0].Words[0].Bounds);
        Assert.IsNull(payload.Regions[0].Lines[0].Confidence);
        Assert.AreEqual("世界", payload.Regions[1].Lines[0].Words[0].Text);
    }

    [TestMethod]
    public async Task AnalyzeNoTextFixture_ShouldReturnEmptyNormalizedDocument()
    {
        var document = new TextExtractionAnalysisDocument(
            new TextExtractionRasterSize(64, 32),
            string.Empty,
            [],
            []);
        var analyzer = CreateAnalyzer(TextExtractionAnalysisResult.Succeeded(document));

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Succeeded, output.Status);
        var payload = (OcrDocumentV1)output.Payload!;
        Assert.AreEqual(string.Empty, payload.FullText);
        Assert.IsEmpty(payload.Languages);
        Assert.IsEmpty(payload.Regions);
    }

    [TestMethod]
    public async Task AnalyzeOrientationFixture_ShouldUseCorrectedRasterCoordinates()
    {
        var document = new TextExtractionAnalysisDocument(
            new TextExtractionRasterSize(40, 80),
            "portrait",
            [],
            [new TextExtractionAnalysisRegion(
                new TextExtractionPixelBounds(4, 50, 30, 20),
                Order: 0,
                [new TextExtractionAnalysisLine(
                    "portrait",
                    new TextExtractionPixelBounds(4, 50, 30, 20),
                    Order: 0,
                    [new TextExtractionAnalysisWord(
                        "portrait",
                        new TextExtractionPixelBounds(4, 50, 30, 20),
                        Order: 0)])])]);
        var analyzer = CreateAnalyzer(TextExtractionAnalysisResult.Succeeded(document));

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        var payload = (OcrDocumentV1)output.Payload!;
        Assert.AreEqual(new PixelSize(40, 80), payload.RasterSize);
        Assert.AreEqual(new PixelRect(4, 50, 30, 20), payload.Regions[0].Bounds);
    }

    [TestMethod]
    public async Task AnalyzeOutOfBoundsFixture_ShouldReturnBoundedTerminalFailure()
    {
        var document = new TextExtractionAnalysisDocument(
            new TextExtractionRasterSize(40, 80),
            "outside",
            [],
            [new TextExtractionAnalysisRegion(
                new TextExtractionPixelBounds(35, 50, 10, 20),
                Order: 0,
                [])]);
        var analyzer = CreateAnalyzer(TextExtractionAnalysisResult.Succeeded(document));

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        AssertFailure(
            output,
            CaptureAnalyzerOutputStatus.Failed,
            AnalysisFailureCode.InvalidResponse,
            AnalysisFailureDisposition.Terminal);
    }

    [TestMethod]
    [DataRow(TextExtractionAnalysisStatus.Unavailable)]
    [DataRow(TextExtractionAnalysisStatus.TransientFailure)]
    [DataRow(TextExtractionAnalysisStatus.TerminalFailure)]
    public async Task AnalyzeFailureFixture_ShouldMapToBoundedOutcome(
        TextExtractionAnalysisStatus status)
    {
        TextExtractionAnalysisResult result = status switch
        {
            TextExtractionAnalysisStatus.Unavailable => TextExtractionAnalysisResult.Unavailable,
            TextExtractionAnalysisStatus.TransientFailure => TextExtractionAnalysisResult.TransientFailure,
            TextExtractionAnalysisStatus.TerminalFailure => TextExtractionAnalysisResult.TerminalFailure,
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
        var analyzer = CreateAnalyzer(result);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        if (status == TextExtractionAnalysisStatus.Unavailable)
        {
            AssertFailure(
                output,
                CaptureAnalyzerOutputStatus.Unsupported,
                AnalysisFailureCode.CapabilityUnavailable,
                AnalysisFailureDisposition.Terminal);
        }
        else if (status == TextExtractionAnalysisStatus.TransientFailure)
        {
            AssertFailure(
                output,
                CaptureAnalyzerOutputStatus.Failed,
                AnalysisFailureCode.ProviderUnavailable,
                AnalysisFailureDisposition.Transient);
        }
        else
        {
            AssertFailure(
                output,
                CaptureAnalyzerOutputStatus.Failed,
                AnalysisFailureCode.InvalidResponse,
                AnalysisFailureDisposition.Terminal);
        }
    }

    [TestMethod]
    public async Task AnalyzeCancelledFixture_ShouldReturnCancelledWithoutProviderDetails()
    {
        var analyzer = CreateAnalyzer(TextExtractionAnalysisResult.Cancelled);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Cancelled, output.Status);
        Assert.IsNull(output.Payload);
        Assert.IsNull(output.Failure);
    }

    [TestMethod]
    public async Task AnalyzeWhenProviderThrowsCancellation_ShouldReturnCancelled()
    {
        var service = new StubTextExtractionAnalysisService(
            TextExtractionAnalysisResult.Unavailable,
            (_, _) => throw new OperationCanceledException());
        var analyzer = new WindowsOcrDocumentAnalyzer(service);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Cancelled, output.Status);
    }

    [TestMethod]
    [DataRow(TextExtractionReadyState.Ready, CaptureAnalyzerAvailabilityStatus.Available)]
    [DataRow(TextExtractionReadyState.PreparationNeeded, CaptureAnalyzerAvailabilityStatus.PreparationRequired)]
    [DataRow(TextExtractionReadyState.Disabled, CaptureAnalyzerAvailabilityStatus.Disabled)]
    [DataRow(TextExtractionReadyState.NotSupported, CaptureAnalyzerAvailabilityStatus.Unsupported)]
    [DataRow(TextExtractionReadyState.Unknown, CaptureAnalyzerAvailabilityStatus.TemporarilyUnavailable)]
    public async Task Availability_ShouldMapApplicationReadyState(
        TextExtractionReadyState readyState,
        CaptureAnalyzerAvailabilityStatus expected)
    {
        var service = new StubTextExtractionAnalysisService(
            TextExtractionAnalysisResult.Unavailable,
            readyState: readyState);
        var analyzer = new WindowsOcrDocumentAnalyzer(service);
        var request = new CaptureAnalyzerAvailabilityRequest(
            analyzer.Descriptor,
            CaptureMediaKind.Image,
            sourceLength: 1,
            Purpose,
            AnalysisProcessingPolicy.LocalOnly(Purpose));

        CaptureAnalyzerAvailability availability = await analyzer.GetAvailabilityAsync(request);

        Assert.AreEqual(expected, availability.Status);
        if (expected == CaptureAnalyzerAvailabilityStatus.Unsupported)
        {
            Assert.AreEqual(AnalysisFailureCode.CapabilityUnavailable, availability.Failure?.Code);
        }
        else if (expected == CaptureAnalyzerAvailabilityStatus.TemporarilyUnavailable)
        {
            Assert.AreEqual(AnalysisFailureDisposition.Transient, availability.Failure?.Disposition);
        }
    }

    private static WindowsOcrDocumentAnalyzer CreateAnalyzer(TextExtractionAnalysisResult result)
    {
        return new WindowsOcrDocumentAnalyzer(new StubTextExtractionAnalysisService(result));
    }

    private static CaptureAnalysisRequest CreateRequest(WindowsOcrDocumentAnalyzer analyzer)
    {
        return new CaptureAnalysisRequest(
            analyzer.Descriptor,
            Purpose,
            AnalysisProcessingPolicy.LocalOnly(Purpose),
            new MemoryAnalysisSource([1, 2, 3]));
    }

    private static void AssertFailure(
        CaptureAnalyzerOutput output,
        CaptureAnalyzerOutputStatus expectedStatus,
        AnalysisFailureCode expectedCode,
        AnalysisFailureDisposition expectedDisposition)
    {
        Assert.AreEqual(expectedStatus, output.Status);
        Assert.IsNull(output.Payload);
        Assert.AreEqual(expectedCode, output.Failure?.Code);
        Assert.AreEqual(expectedDisposition, output.Failure?.Disposition);
    }

    private sealed class StubTextExtractionAnalysisService : ITextExtractionAnalysisService
    {
        private readonly TextExtractionAnalysisResult _result;
        private readonly Func<Stream, CancellationToken, Task<TextExtractionAnalysisResult>>? _handler;
        private readonly TextExtractionReadyState _readyState;

        public StubTextExtractionAnalysisService(
            TextExtractionAnalysisResult result,
            Func<Stream, CancellationToken, Task<TextExtractionAnalysisResult>>? handler = null,
            TextExtractionReadyState readyState = TextExtractionReadyState.Ready)
        {
            _result = result;
            _handler = handler;
            _readyState = readyState;
        }

        public TextExtractionModelDescriptor ModelDescriptor { get; } = new(
            "microsoft-windows",
            "windows-media-ocr",
            ModelVersion: null,
            "windows-media-ocr",
            RuntimeVersion: null);

        public TextExtractionReadyState GetReadyState()
        {
            return _readyState;
        }

        public Task<TextExtractionAnalysisResult> ExtractAnalysisAsync(
            Stream sourceImage,
            CancellationToken cancellationToken = default)
        {
            return _handler?.Invoke(sourceImage, cancellationToken) ?? Task.FromResult(_result);
        }
    }

    private sealed class MemoryAnalysisSource : ICaptureAnalysisSource
    {
        private readonly byte[] _bytes;

        public MemoryAnalysisSource(byte[] bytes)
        {
            _bytes = bytes;
            DateTimeOffset timestamp = new(2026, 8, 7, 0, 0, 0, TimeSpan.Zero);
            SourceRevision = new(
                bytes.LongLength,
                timestamp,
                ContentFingerprint.Sha256(Convert.ToHexStringLower(SHA256.HashData(bytes))));
        }

        public CaptureId CaptureId { get; } = CaptureId.New();

        public CaptureMediaKind MediaKind => CaptureMediaKind.Image;

        public SourceRevision SourceRevision { get; }

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stream stream = new MemoryStream(_bytes, writable: false);
            return ValueTask.FromResult(stream);
        }
    }
}
