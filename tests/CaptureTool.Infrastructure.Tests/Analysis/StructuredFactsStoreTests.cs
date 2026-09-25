using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Text;
using System.Text.Json.Nodes;

namespace CaptureTool.Infrastructure.Tests.Analysis;

[TestClass]
public sealed class StructuredFactsStoreTests
{
    public TestContext TestContext { get; set; } = null!;
    private CancellationToken Ct => TestContext.CancellationToken;

    [TestMethod]
    [DataRow(AnalysisMediaKind.Image, false)]
    [DataRow(AnalysisMediaKind.Image, true)]
    [DataRow(AnalysisMediaKind.Audio, false)]
    [DataRow(AnalysisMediaKind.Audio, true)]
    [DataRow(AnalysisMediaKind.Video, false)]
    [DataRow(AnalysisMediaKind.Video, true)]
    public async Task ExtractedFactsRoundTripWithCoverageAndEvidenceForEveryMediaKind(AnalysisMediaKind kind, bool limited)
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken token = await store.BeginRunAsync(id, kind, AnalysisTestEnvironment.Revision(), "v1", Ct);
        const string text = "https://private.example.test USD 5 USD 5 EUR 6";
        AnalysisPayload payload = kind switch
        {
            AnalysisMediaKind.Audio => new TranscriptMetadata("en", [new(text, TimeSpan.Zero, TimeSpan.FromSeconds(2))]),
            AnalysisMediaKind.Video => new QrCodeMetadata([new(text, new(.1, .2, .3, .4), TimeSpan.FromSeconds(1))]),
            _ => new TextRecognitionMetadata([new(text, new(.1, .2, .3, .4))]),
        };
        AnalysisResult source = new(payload, AnalysisTestEnvironment.Producer(), DateTimeOffset.UtcNow, "v1");
        await store.TryWriteAsync(token, source, Ct);
        var processor = new StructuredFactsProcessor(limited ? new(new(100, 1000, 1000, TimeSpan.FromSeconds(2)), 2, 1) : null);
        MetadataProcessorInput input = MetadataProcessorInput.Create((await store.GetAsync(id, cancellationToken: Ct))!, processor.Descriptor, Ct);
        AnalyzerOutcome outcome = await processor.ProcessAsync(input, Ct);
        var facts = (StructuredFactsMetadata)outcome.Payload!;
        Assert.AreEqual(limited ? MetadataProcessingLimit.Facts | MetadataProcessingLimit.Evidence : MetadataProcessingLimit.None, facts.Coverage!.Limits);
        AnalysisResult result = new(facts, outcome.Producer!, DateTimeOffset.UtcNow, "v1", derivation: input.Derivation);
        Assert.IsTrue(await store.TryWriteAsync(token, result, Ct));
        using var reopened = environment.CreateStore();
        AnalysisResult loaded = (await reopened.GetAsync(id, cancellationToken: Ct))!.Results.Single(saved => saved.Payload is StructuredFactsMetadata);
        var restored = (StructuredFactsMetadata)loaded.Payload;
        Assert.AreEqual(facts.Coverage, restored.Coverage);
        Assert.AreEqual(result.ResultId, loaded.ResultId);
        Assert.AreEqual(result.Producer, loaded.Producer);
        CollectionAssert.AreEqual(facts.Facts.Select(fact => (fact.Kind, fact.Value)).ToArray(), restored.Facts.Select(fact => (fact.Kind, fact.Value)).ToArray());
        foreach (StructuredFact fact in restored.Facts)
        foreach (AnalysisEvidence evidence in fact.Evidence)
            Assert.AreEqual(fact.Value, evidence.ResolveText(source));
        foreach (byte[] bytes in environment.Files.PublishedBytes)
            Assert.DoesNotContain("private.example", Encoding.UTF8.GetString(bytes));
    }

    [TestMethod]
    public async Task PartialInputAndEmptyOutputStayExplicitAfterReloadWhileLegacyCoverageStaysUnknown()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken token = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Ct);
        var source = new AnalysisResult(new TextRecognitionMetadata([new(new string('x', 100)), new("plain")]), AnalysisTestEnvironment.Producer(), DateTimeOffset.UtcNow, "v1");
        await store.TryWriteAsync(token, source, Ct);
        var processor = new StructuredFactsProcessor(new(new(1, 50, 50, TimeSpan.FromSeconds(2)), 256, 16));
        MetadataProcessorInput input = MetadataProcessorInput.Create((await store.GetAsync(id, cancellationToken: Ct))!, processor.Descriptor, Ct);
        AnalyzerOutcome outcome = await processor.ProcessAsync(input, Ct);
        await store.TryWriteAsync(token, new(outcome.Payload!, outcome.Producer!, DateTimeOffset.UtcNow, "v1", derivation: input.Derivation), Ct);
        using var reopened = environment.CreateStore();
        var facts = (StructuredFactsMetadata)(await reopened.GetAsync(id, cancellationToken: Ct))!.Results.Last().Payload;
        Assert.IsEmpty(facts.Facts);
        Assert.AreEqual(MetadataProcessingLimit.OversizedEntry | MetadataProcessingLimit.InputEntries, facts.Coverage!.Limits);
        Assert.AreEqual(0, facts.Coverage.IncludedEntries);
        Assert.AreEqual(2L, facts.Coverage.AvailableEntries);
        await Mutate(environment, document => document["Results"]![1]!.AsObject().Remove("Coverage"));
        facts = (StructuredFactsMetadata)(await reopened.GetAsync(id, cancellationToken: Ct))!.Results.Last().Payload;
        Assert.IsNull(facts.Coverage, "Older facts never imply complete processing.");
    }

    [TestMethod]
    [DataRow("negative")]
    [DataRow("unknown-limit")]
    [DataRow("unmarked-omission")]
    [DataRow("false-input-limit")]
    [DataRow("no-entries-with-facts")]
    [DataRow("wrong-character-total")]
    [DataRow("wrong-source-total")]
    [DataRow("partial-character-floor")]
    [DataRow("coverage-on-ocr")]
    public async Task InvalidCoverageFailsClosedOnRead(string fault)
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken token = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Ct);
        var source = new AnalysisResult(new TextRecognitionMetadata([new("USD 5")]), AnalysisTestEnvironment.Producer(), DateTimeOffset.UtcNow, "v1");
        await store.TryWriteAsync(token, source, Ct);
        var processor = new StructuredFactsProcessor();
        MetadataProcessorInput input = MetadataProcessorInput.Create((await store.GetAsync(id, cancellationToken: Ct))!, processor.Descriptor, Ct);
        AnalyzerOutcome outcome = await processor.ProcessAsync(input, Ct);
        await store.TryWriteAsync(token, new(outcome.Payload!, outcome.Producer!, DateTimeOffset.UtcNow, "v1", derivation: input.Derivation), Ct);
        await Mutate(environment, document =>
        {
            JsonNode coverage = document["Results"]![1]!["Coverage"]!;
            switch (fault)
            {
                case "negative": coverage["IncludedCharacters"] = -1; break;
                case "unknown-limit": coverage["Limits"] = 128; break;
                case "unmarked-omission": coverage["AvailableEntries"] = 2; break;
                case "false-input-limit": coverage["Limits"] = (int)MetadataProcessingLimit.InputEntries; break;
                case "no-entries-with-facts":
                    coverage["IncludedEntries"] = 0;
                    coverage["IncludedCharacters"] = 0;
                    coverage["Limits"] = (int)MetadataProcessingLimit.InputEntries;
                    break;
                case "wrong-character-total": coverage["IncludedCharacters"] = 100; break;
                case "wrong-source-total": coverage["AvailableEntries"] = 2; coverage["IncludedEntries"] = 2; break;
                case "partial-character-floor":
                    document["Results"]![0]!["Text"]!.AsArray().Add((JsonNode)new JsonObject { ["Text"] = "plain", ["Bounds"] = null, ["TimestampTicks"] = null });
                    coverage["AvailableEntries"] = 2;
                    coverage["IncludedCharacters"] = 1;
                    coverage["Limits"] = (int)MetadataProcessingLimit.InputCharacters;
                    break;
                case "coverage-on-ocr": document["Results"]![0]!["Coverage"] = coverage.DeepClone(); break;
                default: Assert.Fail("Unknown fault."); break;
            }
        });
        byte[] original = await File.ReadAllBytesAsync(environment.MetadataPaths.Single(), Ct);
        using var reopened = environment.CreateStore();
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => reopened.GetAsync(id, cancellationToken: Ct));
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(environment.MetadataPaths.Single(), Ct));
    }

    private async Task Mutate(AnalysisTestEnvironment environment, Action<JsonNode> mutate)
    {
        string path = environment.MetadataPaths.Single();
        JsonNode document = JsonNode.Parse(environment.Protector.Unprotect(await File.ReadAllBytesAsync(path, Ct)))!;
        mutate(document);
        await File.WriteAllBytesAsync(path, environment.Protector.Protect(Encoding.UTF8.GetBytes(document.ToJsonString())), Ct);
    }
}
