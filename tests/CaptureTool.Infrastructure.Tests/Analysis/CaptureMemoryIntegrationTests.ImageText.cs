using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using System.Drawing;

namespace CaptureTool.Infrastructure.Tests.Analysis;

public sealed partial class CaptureMemoryIntegrationTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ImageTextSurvivesReopeningAndRestartWithoutPreparingOrRunningModels(bool empty)
    {
        using var environment = new AnalysisTestEnvironment();
        var ocr = new ImageTextAnalyzer(empty ? new TextRecognitionMetadata([]) : ImageText());
        var qr = new ImageTextAnalyzer(new QrCodeMetadata([]));
        CaptureAsset asset;
        Guid resultId;
        await using (var app = new App(environment, mediaAnalyzers: [qr, ocr]))
        {
            await app.EnableAsync(Ct);
            asset = app.Asset();
            await app.Memory.RegisterCaptureAsync(asset, Ct);
            var first = await ExtractImageTextAsync(app, new(asset.SourcePath));
            Assert.AreEqual(TextExtractionStatus.Success, first!.Status);
            Assert.IsTrue(first.Document!.HasTextResults);
            Assert.IsTrue(first.Document.HasQrCodeResults);
            if (!empty)
            {
                Assert.AreEqual("https://example.com/ocr", first.Document.Text);
                Assert.AreEqual(new RectangleF(10, 10, 50, 10), first.Document.Regions.Single().Bounds);
                Assert.AreEqual(2, first.Document.Regions.Single().LineIndex);
            }
            var reopened = await ExtractImageTextAsync(app, new(asset.SourcePath));
            Assert.AreEqual(first.Document.Text, reopened!.Document!.Text);
            resultId = (await app.Store.GetAsync(asset.Id, cancellationToken: Ct))!.Results.Single(result => result.Payload is TextRecognitionMetadata).ResultId;
            Assert.AreEqual(1, ocr.Calls);
            Assert.AreEqual(1, qr.Calls);
        }
        var restartedOcr = new ImageTextAnalyzer(ImageText());
        var restartedQr = new ImageTextAnalyzer(new QrCodeMetadata([]));
        await using var restarted = new App(environment, mediaAnalyzers: [restartedQr, restartedOcr]);
        await restarted.Memory.InitializeAsync(Ct);
        var cached = await ExtractImageTextAsync(restarted, new(asset.SourcePath));
        Assert.AreEqual(TextExtractionStatus.Success, cached!.Status);
        Assert.AreEqual(empty ? "" : "https://example.com/ocr", cached.Document!.Text);
        Assert.AreEqual(0, restartedOcr.Preparations + restartedOcr.Calls + restartedQr.Preparations + restartedQr.Calls);
        Assert.AreEqual(resultId, (await restarted.Store.GetAsync(asset.Id, cancellationToken: Ct))!.Results.Single(result => result.Payload is TextRecognitionMetadata).ResultId);
        await restarted.Memory.AnalyzeAsync(asset.SourcePath, AnalysisCapability.TextRecognition, Ct);
        var reused = await restarted.CompletedAsync(asset, Ct);
        Assert.AreEqual(resultId, reused.Run.CompletedSteps.Single().ReusedResultId);
        Assert.AreEqual(0, restartedOcr.Preparations + restartedOcr.Calls);
    }

    [TestMethod]
    public async Task RetryingFailedQrScanReusesSuccessfulOcr()
    {
        using var environment = new AnalysisTestEnvironment();
        var ocr = new ImageTextAnalyzer(ImageText());
        var qr = new ImageTextAnalyzer(new QrCodeMetadata([])) { Fail = true };
        await using var app = new App(environment, mediaAnalyzers: [qr, ocr]);
        await app.EnableAsync(Ct);
        var asset = app.Asset();
        var partial = await ExtractImageTextAsync(app, new(asset.SourcePath));
        Assert.AreEqual(TextExtractionStatus.Success, partial!.Status);
        Assert.IsTrue(partial.Document!.HasTextResults);
        Assert.IsFalse(partial.Document.HasQrCodeResults);
        qr.Fail = false;
        var complete = await ExtractImageTextAsync(app, new(asset.SourcePath));
        Assert.IsTrue(complete!.Document!.HasQrCodeResults);
        Assert.AreEqual(1, ocr.Calls);
        Assert.AreEqual(1, ocr.Preparations);
        Assert.AreEqual(2, qr.Calls);
    }

    [TestMethod]
    public async Task ImageTextReusesPaneScanAndRescansChangedSource()
    {
        using var environment = new AnalysisTestEnvironment();
        var ocr = new ImageTextAnalyzer(ImageText());
        var qr = new ImageTextAnalyzer(new QrCodeMetadata([]));
        await using var app = new App(environment, mediaAnalyzers: [qr, ocr]);
        await app.EnableAsync(Ct);
        var asset = app.Asset();
        await app.Memory.RegisterCaptureAsync(asset, Ct);
        await app.Memory.ScanTextAsync(asset.SourcePath, Ct);
        await app.CompletedAsync(asset, Ct);
        var first = await ExtractImageTextAsync(app, new(asset.SourcePath));
        Assert.AreEqual(TextExtractionStatus.Success, first!.Status);
        Assert.AreEqual(1, ocr.Calls);
        // Let the worker release its source lease after publishing the final step.
        await app.Memory.CancelAnalysisAsync(asset.SourcePath, Ct);
        await File.WriteAllTextAsync(asset.SourcePath, "new source bytes", Ct);
        ocr.Payload = new TextRecognitionMetadata([new("changed")]);
        var changed = await ExtractImageTextAsync(app, new(asset.SourcePath));
        Assert.AreEqual("changed", changed!.Document!.Text);
        Assert.AreEqual(2, ocr.Calls);
        Assert.AreEqual(2, qr.Calls);
    }

    [TestMethod]
    public async Task ImageTextMatchesWorkingCopyToSavedSourceBeforePersisting()
    {
        using var environment = new AnalysisTestEnvironment();
        var ocr = new ImageTextAnalyzer(ImageText());
        var qr = new ImageTextAnalyzer(new QrCodeMetadata([]));
        await using var app = new App(environment, mediaAnalyzers: [qr, ocr]);
        await app.EnableAsync(Ct);
        var asset = app.Asset();
        var working = app.Asset();
        var image = new ImageFile(working.SourcePath, asset.SourcePath);
        var result = await ExtractImageTextAsync(app, image);
        Assert.AreEqual(TextExtractionStatus.Success, result!.Status);
        Assert.AreEqual(asset.SourcePath, (await app.Catalog.ReadAllAsync(Ct)).Single().SourcePath);
        await File.WriteAllTextAsync(working.SourcePath, "unsaved edited bytes", Ct);
        Assert.IsNull(await ExtractImageTextAsync(app, image));
        Assert.AreEqual(1, ocr.Calls);
    }

    [TestMethod]
    public async Task ImageTextProvidesPersistedOcrToMetadataProcessing()
    {
        using var environment = new AnalysisTestEnvironment();
        var ocr = new ImageTextAnalyzer(ImageText());
        var qr = new ImageTextAnalyzer(new QrCodeMetadata([]));
        var facts = new StructuredFactsProcessor();
        var configuration = new CaptureAnalysisConfiguration([new(AnalysisMediaKind.Image, "test-v1", [
            new(AnalysisCapability.QrCodeDetection, [qr.Descriptor.Id], TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)),
            new(AnalysisCapability.TextRecognition, [ocr.Descriptor.Id], TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)),
            new(AnalysisCapability.StructuredFacts, [facts.Descriptor.Id], TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1))])]);
        await using var app = new App(environment, processors: [facts], mediaAnalyzers: [qr, ocr], configuration: configuration);
        await app.EnableAsync(Ct);
        var asset = app.Asset();
        await app.Memory.RegisterCaptureAsync(asset, Ct);
        await ExtractImageTextAsync(app, new(asset.SourcePath));
        var original = (await app.Store.GetAsync(asset.Id, cancellationToken: Ct))!.Results.Single(result => result.Payload is TextRecognitionMetadata);
        await app.Memory.AnalyzeAsync(asset.SourcePath, AnalysisCapability.StructuredFacts, Ct);
        await app.CompletedAsync(asset, Ct);
        var derived = (await app.Store.GetAsync(asset.Id, cancellationToken: Ct))!.Results.Single(result => result.Payload is StructuredFactsMetadata);
        Assert.AreEqual("https://example.com/ocr", ((StructuredFactsMetadata)derived.Payload).Facts.Single().Value);
        Assert.IsTrue(derived.Derivation!.Inputs.Any(input => input.ResultId == original.ResultId));
        Assert.AreEqual(1, ocr.Calls);
        Assert.AreEqual(1, ocr.Preparations);
    }

    [TestMethod]
    public async Task ImageTextJoinsPendingScanWithoutRepeatingOcr()
    {
        using var environment = new AnalysisTestEnvironment();
        var entered = Signal();
        var release = Signal();
        var ocr = new ImageTextAnalyzer(ImageText()) { BeforeComplete = async ct => { entered.TrySetResult(); await release.Task.WaitAsync(ct); } };
        var qr = new ImageTextAnalyzer(new QrCodeMetadata([]));
        await using var app = new App(environment, mediaAnalyzers: [qr, ocr]);
        await app.EnableAsync(Ct);
        var asset = app.Asset();
        await app.Memory.RegisterCaptureAsync(asset, Ct);
        await app.Memory.ScanTextAsync(asset.SourcePath, Ct);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var joined = app.Memory.TryExtractAsync(new(asset.SourcePath), new(100, 50), cancelled.Token);
        await cancelled.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => joined);
        Assert.IsTrue((await app.Store.GetWorkAsync(asset.Id, Ct))!.Run.IsPending, "A joined scan belongs to the original requester.");
        release.TrySetResult();
        var result = await ExtractImageTextAsync(app, new(asset.SourcePath));
        Assert.AreEqual(TextExtractionStatus.Success, result!.Status);
        Assert.AreEqual(1, ocr.Calls);
    }

    [TestMethod]
    public async Task CancellingImageTextStopsItsRequestAndAllowsRetryWithoutRepeatingCompletedQr()
    {
        using var environment = new AnalysisTestEnvironment();
        var entered = Signal();
        var stopped = Signal();
        var ocr = new ImageTextAnalyzer(ImageText()) { BeforeComplete = async ct =>
        {
            entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, ct); }
            finally { stopped.TrySetResult(); }
        } };
        var qr = new ImageTextAnalyzer(new QrCodeMetadata([]));
        await using var app = new App(environment, mediaAnalyzers: [qr, ocr]);
        await app.EnableAsync(Ct);
        var asset = app.Asset();
        await app.Memory.RegisterCaptureAsync(asset, Ct);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var scan = app.Memory.TryExtractAsync(new(asset.SourcePath), new(100, 50), cancelled.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await cancelled.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => scan);
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Assert.AreEqual(AnalysisRunStatus.Cancelled, (await app.Store.GetWorkAsync(asset.Id, Ct))!.Run.Status);
        Assert.IsFalse((await app.Store.GetAsync(asset.Id, cancellationToken: Ct))!.Results.Any(result => result.Payload is TextRecognitionMetadata));
        ocr.BeforeComplete = null;
        Assert.AreEqual(TextExtractionStatus.Success, (await ExtractImageTextAsync(app, new(asset.SourcePath)))!.Status);
        Assert.AreEqual(2, ocr.Calls);
        Assert.AreEqual(1, qr.Calls);
    }

    private Task<TextExtractionResult?> ExtractImageTextAsync(App app, ImageFile image) =>
        app.Memory.TryExtractAsync(image, new(100, 50), Ct).WaitAsync(TimeSpan.FromSeconds(10), Ct);

    private static TextRecognitionMetadata ImageText() => new([new("https://example.com/ocr", new(.1, .2, .5, .2), lineIndex: 2, wordIndex: 0)]);

    private sealed class ImageTextAnalyzer(AnalysisPayload payload) : IMediaAnalyzer
    {
        public AnalysisPayload Payload { get; set; } = payload;
        public int Calls { get; private set; }
        public int Preparations { get; private set; }
        public bool Fail { get; set; }
        public Func<CancellationToken, Task>? BeforeComplete { get; set; }
        public MediaAnalyzerDescriptor Descriptor { get; } = new(payload is QrCodeMetadata ? "test-qr" : "test-ocr", payload.Capability, [AnalysisMediaKind.Image]);
        public ValueTask<AnalyzerAvailability> GetAvailabilityAsync(AnalysisMediaKind kind, string? language, CancellationToken ct) => ValueTask.FromResult(AnalyzerAvailability.PreparationRequired);
        public Task<AnalyzerAvailability> PrepareAsync(IProgress<AnalysisProgress>? progress, CancellationToken ct)
        {
            Preparations++;
            return Task.FromResult(AnalyzerAvailability.Ready);
        }
        public async Task<AnalyzerOutcome> AnalyzeAsync(AnalysisInput input, IProgress<AnalysisProgress>? progress, CancellationToken ct)
        {
            Calls++;
            if (BeforeComplete != null) await BeforeComplete(ct);
            if (Fail) return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, "test-failure");
            return AnalyzerOutcome.Success(Payload, new(Descriptor.Id, "local", "test-model", "1"));
        }
    }
}
