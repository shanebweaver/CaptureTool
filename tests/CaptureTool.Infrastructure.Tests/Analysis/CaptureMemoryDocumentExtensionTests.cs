using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;
using System.Text;
using System.Text.Json.Nodes;

namespace CaptureTool.Infrastructure.Tests.Analysis;

public sealed partial class CaptureMemoryIntegrationTests
{
    [TestMethod]
    public async Task ExtraDocumentFieldsDoNotBlockStartupNewCaptureOrTextScan()
    {
        using var environment = new AnalysisTestEnvironment();
        CaptureAsset existing;
        Guid existingResult;
        await using (var app = new App(environment))
        {
            await app.EnableAsync(Ct);
            existing = app.Asset();
            await app.RequestAsync(existing, Ct);
            await app.CompletedAsync(existing, Ct);
            existingResult = (await app.Store.GetAsync(existing.Id, cancellationToken: Ct))!.Results.Single().ResultId;
        }
        string catalogPath = Path.Combine(environment.Root, "CaptureAssets", "catalog.bin");
        // Reproduce the on-disk shape that blocked startup after unused fields were removed.
        await ExtendDocumentAsync(environment.ControlPath, node => node["ReconciliationBoundary"] = 12);
        await ExtendDocumentAsync(catalogPath, node =>
        {
            node["Sequence"] = 12;
            node["NamingEpoch"] = Guid.NewGuid();
            node["AutomaticNamingEnabled"] = false;
            var asset = node["Assets"]![0]!;
            asset["Sequence"] = 12;
            asset["AutomaticAuthorization"] = Guid.NewGuid();
            asset["AutomaticName"] = false;
            asset["NamingEpoch"] = Guid.NewGuid();
            asset["Name"] = "Accepted capture name";
        });
        await ExtendDocumentAsync(environment.MetadataPaths.Single(), node =>
            node["Results"]![0]!["Producer"]!["AdditionalDiagnostics"] = new JsonObject { ["Sample"] = true });
        byte[] controlBefore = await File.ReadAllBytesAsync(environment.ControlPath, Ct);
        byte[] catalogBefore = await File.ReadAllBytesAsync(catalogPath, Ct);
        byte[] recordBefore = await File.ReadAllBytesAsync(environment.MetadataPaths.Single(), Ct);

        var ocr = new StartupScanAnalyzer(AnalysisCapability.TextRecognition);
        await using var restarted = new App(environment, mediaAnalyzers: [new StartupScanAnalyzer(AnalysisCapability.QrCodeDetection), ocr]);
        await restarted.Memory.InitializeAsync(Ct);
        Assert.IsNull(restarted.Memory.State.FailureCode);
        Assert.IsTrue(restarted.Memory.State.Storage.IsAvailable);
        Assert.IsTrue(restarted.Memory.State.CanScan);
        Assert.IsEmpty(await restarted.Store.ReadPendingAsync(Ct));
        Assert.AreEqual(0, ocr.Calls, "Loading saved data must not schedule analysis.");
        Assert.AreEqual(existingResult, (await restarted.Store.GetAsync(existing.Id, cancellationToken: Ct))!.Results.Single().ResultId);
        Assert.AreEqual("Accepted capture name", (await restarted.Catalog.GetAsync(existing.Id, Ct))!.Name!.Text);
        CollectionAssert.AreEqual(controlBefore, await File.ReadAllBytesAsync(environment.ControlPath, Ct));
        CollectionAssert.AreEqual(catalogBefore, await File.ReadAllBytesAsync(catalogPath, Ct));
        CollectionAssert.AreEqual(recordBefore, await File.ReadAllBytesAsync(environment.MetadataPaths.Single(), Ct));

        var capture = restarted.Asset();
        await restarted.Memory.RegisterCaptureAsync(capture, Ct);
        await restarted.Memory.ScanTextAsync(capture.SourcePath, Ct);
        Assert.IsNull(restarted.Memory.State.FailureCode);
        await restarted.CompletedAsync(capture, Ct);
        Assert.IsNull(restarted.Memory.State.FailureCode);
        var result = (await restarted.Store.GetAsync(capture.Id, cancellationToken: Ct))!.Results.Single(result => result.Payload is TextRecognitionMetadata);
        Assert.AreEqual("Recognized capture text", ((TextRecognitionMetadata)result.Payload).Regions.Single().Text);
        Assert.AreEqual(1, ocr.Calls);
        Assert.HasCount(2, await restarted.Catalog.ReadAllAsync(Ct));
        Assert.AreEqual("Accepted capture name", (await restarted.Catalog.GetAsync(existing.Id, Ct))!.Name!.Text);
        Assert.IsTrue(File.Exists(existing.SourcePath));

        async Task ExtendDocumentAsync(string path, Action<JsonNode> update)
        {
            var node = JsonNode.Parse(environment.Protector.Unprotect(await File.ReadAllBytesAsync(path, Ct)))!;
            update(node);
            await File.WriteAllBytesAsync(path, environment.Protector.Protect(Encoding.UTF8.GetBytes(node.ToJsonString())), Ct);
        }
    }

    private sealed class StartupScanAnalyzer(AnalysisCapability capability) : IMediaAnalyzer
    {
        public int Calls { get; private set; }
        public MediaAnalyzerDescriptor Descriptor { get; } = new("startup-" + capability.Name, capability, [AnalysisMediaKind.Image]);
        public ValueTask<AnalyzerAvailability> GetAvailabilityAsync(AnalysisMediaKind kind, string? language, CancellationToken ct) =>
            ValueTask.FromResult(AnalyzerAvailability.Ready);
        public Task<AnalyzerAvailability> PrepareAsync(IProgress<AnalysisProgress>? progress, CancellationToken ct) =>
            Task.FromResult(AnalyzerAvailability.Ready);
        public Task<AnalyzerOutcome> AnalyzeAsync(AnalysisInput input, IProgress<AnalysisProgress>? progress, CancellationToken ct)
        {
            Calls++;
            AnalysisPayload payload = capability == AnalysisCapability.TextRecognition
                ? new TextRecognitionMetadata([new("Recognized capture text")]) : new QrCodeMetadata([]);
            return Task.FromResult(AnalyzerOutcome.Success(payload, new(Descriptor.Id, "local", "test-model", "1")));
        }
    }
}
