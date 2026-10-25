using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace CaptureTool.Infrastructure.Tests.Analysis;

[TestClass]
public sealed partial class MetadataEnrichmentStoreTests
{
    private const string Value = "https://private.example.test";
    public TestContext TestContext { get; set; } = null!;
    private CancellationToken Ct => TestContext.CancellationToken;

    [TestMethod]
    public async Task ProtectedRoundTripPreservesIdentityEvidenceInputsAndOriginalProvenance()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisRunToken token = await store.StartRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Ct, AnalysisCapability.TextRecognition, AnalysisCapability.StructuredFacts);
        AnalysisResult source = Ocr();
        AnalysisResult facts = Facts(source, absentQr: true);
        Assert.IsTrue(await store.PublishAsync(token, source, Ct));
        Assert.IsTrue(await store.PublishAsync(token, facts, Ct));
        using var reopened = environment.CreateStore();
        CaptureAnalysisRecord record = (await reopened.GetAsync(id, cancellationToken: Ct))!;
        AnalysisResult loaded = record.Results.Single(result => result.Payload is StructuredFactsMetadata);
        Assert.AreEqual(facts.ResultId, loaded.ResultId);
        Assert.AreEqual(facts.Producer, loaded.Producer);
        Assert.AreEqual(facts.GeneratedAt, loaded.GeneratedAt);
        Assert.AreEqual(facts.PlanVersion, loaded.PlanVersion);
        Assert.AreEqual(token.RunId, loaded.ProducingRunId);
        CollectionAssert.AreEqual(facts.Derivation!.Inputs.ToArray(), loaded.Derivation!.Inputs.ToArray());
        StructuredFact fact = ((StructuredFactsMetadata)loaded.Payload).Facts.Single();
        Assert.AreEqual(StructuredFactKind.Url, fact.Kind);
        Assert.AreEqual(Value, fact.Value);
        Assert.AreEqual(Value, fact.Evidence.Single().ResolveText(record.Results.Single(result => result.ResultId == source.ResultId)));
        foreach (byte[] bytes in environment.Files.PublishedBytes)
        {
            Assert.DoesNotContain(Value, Encoding.UTF8.GetString(bytes));
            Assert.DoesNotContain("structured-facts", Encoding.UTF8.GetString(bytes));
        }
        Assert.IsEmpty(Directory.GetFiles(environment.Root, "*.tmp", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task StaleDerivedWriteIsRejectedAndInputReplacementRemovesExistingFactsAtomically()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisRunToken token = await store.StartRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Ct, AnalysisCapability.TextRecognition, AnalysisCapability.Description, AnalysisCapability.StructuredFacts);
        AnalysisResult source = Ocr();
        await store.PublishAsync(token, source, Ct);
        await store.PublishAsync(token, AnalysisTestEnvironment.Description(), Ct);
        await store.PublishAsync(token, Facts(source), Ct);
        Assert.HasCount(3, (await store.GetAsync(id, cancellationToken: Ct))!.Results);

        token = await store.StartRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Ct, AnalysisCapability.TextRecognition, AnalysisCapability.StructuredFacts);
        int writes = environment.Files.PublishedBytes.Count;
        AnalysisResult replacement = Ocr();
        await store.PublishAsync(token, replacement, Ct);
        Assert.HasCount(writes + 1, environment.Files.PublishedBytes, "Replacement and invalidation use one atomic publication.");
        Assert.IsFalse(await store.PublishAsync(token, Facts(source), Ct));
        Assert.HasCount(writes + 1, environment.Files.PublishedBytes);
        using var reopened = environment.CreateStore();
        CaptureAnalysisRecord record = (await reopened.GetAsync(id, cancellationToken: Ct))!;
        Assert.HasCount(2, record.Results);
        Assert.IsFalse(record.Results.Any(result => result.Payload is StructuredFactsMetadata));
        Assert.IsTrue(await store.PublishAsync(token, Facts(replacement), Ct));
    }

    [TestMethod]
    public async Task NewlyAvailableOptionalInputInvalidatesFactsAndRejectsOlderSnapshot()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisRunToken token = await store.StartRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Ct, AnalysisCapability.TextRecognition, AnalysisCapability.StructuredFacts);
        AnalysisResult source = Ocr();
        await store.PublishAsync(token, source, Ct);
        await store.PublishAsync(token, Facts(source, absentQr: true), Ct);
        token = await store.StartRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Ct, AnalysisCapability.QrCodeDetection, AnalysisCapability.StructuredFacts);
        var qr = new AnalysisResult(new QrCodeMetadata([]), source.Producer, source.GeneratedAt, "v1");
        await store.PublishAsync(token, qr, Ct);
        Assert.HasCount(2, (await store.GetAsync(id, cancellationToken: Ct))!.Results);
        Assert.IsFalse(await store.PublishAsync(token, Facts(source, absentQr: true), Ct));
    }

    [TestMethod]
    public async Task FailedReplacementPreservesBothSourceAndDerivedResult()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisRunToken token = await store.StartRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Ct, AnalysisCapability.TextRecognition, AnalysisCapability.StructuredFacts);
        AnalysisResult source = Ocr();
        AnalysisResult facts = Facts(source);
        await store.PublishAsync(token, source, Ct);
        await store.PublishAsync(token, facts, Ct);
        token = await store.StartRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Ct, AnalysisCapability.TextRecognition);
        byte[] original = await File.ReadAllBytesAsync(environment.MetadataPaths.Single(), Ct);
        environment.Protector.FailProtection = true;
        await Assert.ThrowsExactlyAsync<CryptographicException>(() => store.PublishAsync(token, Ocr(), Ct));
        environment.Protector.FailProtection = false;
        environment.Files.BeforeWrite = (_, _) => throw new IOException("Injected publication failure.");
        await Assert.ThrowsExactlyAsync<IOException>(() => store.PublishAsync(token, Ocr(), Ct));
        environment.Files.BeforeWrite = null;
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(environment.MetadataPaths.Single(), Ct));
        using var reopened = environment.CreateStore();
        CollectionAssert.AreEqual(new[] { source.ResultId, facts.ResultId },
            (await reopened.GetAsync(id, cancellationToken: Ct))!.Results.Select(result => result.ResultId).ToArray());
    }

    [TestMethod]
    public async Task RefreshSourceChangeAndDeleteFenceDerivedWritesAcrossRestartAndCleanupRetry()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisRunToken initial = await store.StartRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Ct, AnalysisCapability.TextRecognition, AnalysisCapability.StructuredFacts);
        AnalysisResult source = Ocr();
        AnalysisResult facts = Facts(source);
        await store.PublishAsync(initial, source, Ct);
        await store.PublishAsync(initial, facts, Ct);
        AnalysisRunToken refresh = await store.StartRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Ct, AnalysisCapability.TextRecognition, AnalysisCapability.StructuredFacts);
        Assert.HasCount(2, (await store.GetAsync(id, cancellationToken: Ct))!.Results);
        Assert.IsFalse(await store.PublishAsync(initial, Facts(source), Ct));
        AnalysisRunToken changed = await store.StartRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision('b'), "v1", Ct, AnalysisCapability.TextRecognition, AnalysisCapability.StructuredFacts);
        Assert.IsEmpty((await store.GetAsync(id, cancellationToken: Ct))!.Results);
        Assert.IsFalse(await store.PublishAsync(refresh, Facts(source), Ct));
        Assert.IsFalse(await store.PublishAsync(changed, Facts(source), Ct));
        source = Ocr();
        await store.PublishAsync(changed, source, Ct);
        await store.PublishAsync(changed, Facts(source), Ct);
        environment.Files.FailCleanup = true;
        Assert.IsFalse((await store.ClearAsync(Ct)).Completed);
        Assert.IsFalse(await store.PublishAsync(changed, Facts(source), Ct));
        Assert.IsNull(await store.GetAsync(id, cancellationToken: Ct));
        AnalysisRunToken afterDelete = await store.StartRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision('b'), "v1", Ct, AnalysisCapability.TextRecognition, AnalysisCapability.StructuredFacts);
        source = Ocr();
        facts = Facts(source);
        await store.PublishAsync(afterDelete, source, Ct);
        await store.PublishAsync(afterDelete, facts, Ct);
        environment.Files.FailCleanup = false;
        using var reopened = environment.CreateStore();
        Assert.IsTrue((await reopened.InitializeAsync(Ct)).Completed);
        Assert.AreEqual(facts.ResultId, (await reopened.GetAsync(id, cancellationToken: Ct))!.Results.Last().ResultId);
        await reopened.ClearAsync(Ct);
        Assert.IsEmpty(await reopened.ReadAllAsync(Ct));
    }

    [TestMethod]
    [DataRow("stale-input")]
    [DataRow("absent-input")]
    [DataRow("undeclared-evidence")]
    [DataRow("range")]
    [DataRow("value")]
    [DataRow("schema")]
    [DataRow("ambiguous")]
    [DataRow("missing-inputs")]
    [DataRow("null-input")]
    [DataRow("null-evidence")]
    [DataRow("missing-provenance")]
    [DataRow("missing-identity")]
    [DataRow("empty-identity")]
    public async Task CorruptDerivedDocumentsFailClosedWithoutRewriting(string fault)
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisRunToken token = await store.StartRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Ct, AnalysisCapability.TextRecognition, AnalysisCapability.StructuredFacts);
        AnalysisResult source = Ocr();
        await store.PublishAsync(token, source, Ct);
        await store.PublishAsync(token, Facts(source), Ct);
        await Mutate(environment, document =>
        {
            JsonNode result = document["Results"]![1]!;
            switch (fault)
            {
                case "stale-input": result["Inputs"]![0]!["ResultId"] = Guid.NewGuid(); break;
                case "absent-input": result["Inputs"]![0]!["ResultId"] = null; break;
                case "undeclared-evidence": result["Facts"]![0]!["Evidence"]![0]!["ResultId"] = Guid.NewGuid(); break;
                case "range": result["Facts"]![0]!["Evidence"]![0]!["Start"] = int.MaxValue; break;
                case "value": result["Facts"]![0]!["Value"] = "invented"; break;
                case "schema": result["SchemaVersion"] = 99; break;
                case "ambiguous": result["Text"] = new JsonArray(); break;
                case "missing-inputs": result.AsObject().Remove("Inputs"); break;
                case "null-input": result["Inputs"]![0] = null; break;
                case "null-evidence": result["Facts"]![0]!["Evidence"]![0] = null; break;
                case "missing-provenance": result.AsObject().Remove("ProducingRunId"); break;
                case "missing-identity": result.AsObject().Remove("ResultId"); break;
                case "empty-identity": result["ResultId"] = Guid.Empty; break;
                default: Assert.Fail("Unknown test fault."); break;
            }
        });
        byte[] corrupt = await File.ReadAllBytesAsync(environment.MetadataPaths.Single(), Ct);
        using var reopened = environment.CreateStore();
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => reopened.GetAsync(id, cancellationToken: Ct));
        CollectionAssert.AreEqual(corrupt, await File.ReadAllBytesAsync(environment.MetadataPaths.Single(), Ct));
    }

    [TestMethod]
    public async Task DurableStepCommitChecksInputSnapshotAndRefreshInvalidatesOldDerivedSuccess()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        var plan = new MediaAnalysisPlan(AnalysisMediaKind.Image, "v1", [
            new(AnalysisCapability.TextRecognition, ["ocr"], TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)),
            new(AnalysisCapability.StructuredFacts, ["facts"], TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)),
        ]);
        AnalysisRequest request = AnalysisExecutionStoreTests.Request(environment, await store.GetAdmissionScopeAsync(Ct));
        Guid authorization = Guid.NewGuid();
        await store.AdmitAsync(request, authorization, plan, Ct);
        AnalysisRunToken token = (await store.GetWorkAsync(request.CaptureId, Ct))!.Token;
        await store.BindSourceAsync(token, AnalysisTestEnvironment.Revision(), Ct);
        AnalysisResult source = Ocr(request.RequestId);
        await store.CommitStepAsync(token, Success(AnalysisCapability.TextRecognition), source, Ct);
        await store.CommitStepAsync(token, Success(AnalysisCapability.StructuredFacts), Facts(source, run: request.RequestId), Ct);
        request = request with { RequestId = Guid.NewGuid(), ExpectedRunId = request.RequestId };
        await store.AdmitAsync(request, authorization, plan, Ct);
        token = (await store.GetWorkAsync(request.CaptureId, Ct))!.Token;
        await store.BindSourceAsync(token, AnalysisTestEnvironment.Revision(), Ct);
        AnalysisResult replacement = Ocr(request.RequestId);
        await store.CommitStepAsync(token, Success(AnalysisCapability.TextRecognition), replacement, Ct);
        Assert.HasCount(1, (await store.GetAsync(request.CaptureId, cancellationToken: Ct))!.Results);
        Assert.IsFalse(await store.CommitStepAsync(token, Success(AnalysisCapability.StructuredFacts), Facts(source, run: request.RequestId), Ct));
        Assert.HasCount(1, (await store.GetWorkAsync(request.CaptureId, Ct))!.Run.CompletedSteps);
        Assert.IsTrue(await store.CommitStepAsync(token, Success(AnalysisCapability.StructuredFacts), Facts(replacement, run: request.RequestId), Ct));
        using var reopened = environment.CreateStore();
        Assert.AreEqual(AnalysisRunStatus.Completed, (await reopened.GetWorkAsync(request.CaptureId, Ct))!.Run.Status);
    }

    private async Task Mutate(AnalysisTestEnvironment environment, Action<JsonNode> mutate)
    {
        string path = environment.MetadataPaths.Single();
        JsonNode document = JsonNode.Parse(environment.Protector.Unprotect(await File.ReadAllBytesAsync(path, Ct)))!;
        mutate(document);
        await File.WriteAllBytesAsync(path, environment.Protector.Protect(Encoding.UTF8.GetBytes(document.ToJsonString())), Ct);
    }

    private static AnalysisStepCompletion Success(AnalysisCapability capability) => new(capability, AnalyzerOutcomeKind.Succeeded, null);
    private static AnalysisResult Ocr(Guid? run = null) =>
        new(new TextRecognitionMetadata([new("See " + Value, new(.1, .2, .3, .4))]),
            AnalysisTestEnvironment.Producer(), DateTimeOffset.UtcNow, "v1", run);
    private static AnalysisResult Facts(AnalysisResult source, bool absentQr = false, Guid? run = null)
    {
        List<AnalysisInputReference> inputs = [new(source.Payload.Capability, source.ResultId)];
        if (absentQr) inputs.Add(new(AnalysisCapability.QrCodeDetection, null));
        return new(new StructuredFactsMetadata([new(StructuredFactKind.Url, Value, [new(source.ResultId, 0, 4, Value.Length)])], new(1, 1, Value.Length + 4, MetadataProcessingLimit.None)),
            AnalysisTestEnvironment.Producer(), DateTimeOffset.UtcNow, "v1", run, derivation: new(inputs));
    }
}
