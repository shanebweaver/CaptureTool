using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Text;

namespace CaptureTool.Infrastructure.Tests.Analysis;

public sealed partial class MetadataEnrichmentStoreTests
{
    [TestMethod]
    public async Task BothInsightPayloadsRoundTripProtectedAndAreInvalidatedWithTheirSource()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        var token = await store.StartRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Ct, AnalysisCapability.TextRecognition, AnalysisCapability.CaptureSynopsis, AnalysisCapability.CaptureClassification, AnalysisCapability.ImageAltText, AnalysisCapability.CaptureName);
        var source = Ocr();
        await store.PublishAsync(token, source, Ct);
        foreach (var result in Insights(source)) Assert.IsTrue(await store.PublishAsync(token, result, Ct));
        using var reopened = environment.CreateStore();
        var record = (await reopened.GetAsync(id, cancellationToken: Ct))!;
        var synopsis = (CaptureSynopsisMetadata)record.Results.Single(result => result.Payload is CaptureSynopsisMetadata).Payload;
        Assert.AreEqual("A link is visible.", synopsis.Summary.Single().Text);
        Assert.AreEqual(Value, synopsis.Summary[0].Evidence[0].ResolveText(source));
        var classification = (CaptureClassificationMetadata)record.Results.Single(result => result.Payload is CaptureClassificationMetadata).Payload;
        Assert.AreEqual(CaptureCategory.WebContent, classification.Category);
        Assert.AreEqual("website", classification.Topics[0].Text);
        Assert.AreEqual("A visible link.", ((ImageAltTextMetadata)record.Results.Single(result => result.Payload is ImageAltTextMetadata).Payload).Suggestion!.Text);
        Assert.AreEqual("Private link name", ((CaptureNameMetadata)record.Results.Single(result => result.Payload is CaptureNameMetadata).Payload).Suggestion!.Text);
        foreach (var bytes in environment.Files.PublishedBytes) Assert.DoesNotContain("Private link", Encoding.UTF8.GetString(bytes));
        token = await store.StartRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Ct, AnalysisCapability.TextRecognition, AnalysisCapability.CaptureSynopsis, AnalysisCapability.CaptureClassification, AnalysisCapability.ImageAltText, AnalysisCapability.CaptureName);
        await store.PublishAsync(token, Ocr(), Ct);
        Assert.HasCount(1, (await store.GetAsync(id, cancellationToken: Ct))!.Results);
        foreach (var stale in Insights(source)) Assert.IsFalse(await store.PublishAsync(token, stale, Ct));
        await store.ClearAsync(Ct);
        Assert.IsNull(await store.GetAsync(id, cancellationToken: Ct));
    }

    [TestMethod]
    [DataRow("coverage")]
    [DataRow("summary")]
    [DataRow("evidence")]
    [DataRow("category")]
    [DataRow("vocabulary")]
    [DataRow("topics")]
    public async Task CorruptInsightPayloadsFailClosed(string fault)
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        var token = await store.StartRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Ct, AnalysisCapability.TextRecognition, AnalysisCapability.CaptureSynopsis, AnalysisCapability.CaptureClassification, AnalysisCapability.ImageAltText, AnalysisCapability.CaptureName);
        var source = Ocr();
        await store.PublishAsync(token, source, Ct);
        foreach (var result in Insights(source)) await store.PublishAsync(token, result, Ct);
        await Mutate(environment, document =>
        {
            var synopsis = document["Results"]![1]!;
            var classification = document["Results"]![2]!["Classification"]!;
            switch (fault)
            {
                case "coverage": synopsis["Coverage"] = null; break;
                case "summary": synopsis["Synopsis"]!["Summary"]![0]!["Text"] = new string('x', 401); break;
                case "evidence": synopsis["Synopsis"]!["Summary"]![0]!["Evidence"]![0]!["ResultId"] = Guid.NewGuid(); break;
                case "category": classification["Category"] = 999; break;
                case "vocabulary": classification["VocabularyVersion"] = "future"; break;
                case "topics": classification["Topics"]![0] = null; break;
            }
        });
        using var reopened = environment.CreateStore();
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => reopened.GetAsync(id, cancellationToken: Ct));
    }

    private static AnalysisResult[] Insights(AnalysisResult source)
    {
        var evidence = new AnalysisEvidence(source.ResultId, 0, 4, Value.Length);
        var coverage = new MetadataProcessingCoverage(1, 1, Value.Length + 4, MetadataProcessingLimit.None);
        var derivation = new AnalysisDerivation([new(source.Payload.Capability, source.ResultId)]);
        return [
            new(new CaptureSynopsisMetadata([new("A link is visible.", [evidence])], coverage),
                AnalysisTestEnvironment.Producer(), DateTimeOffset.UtcNow, "v1", derivation: derivation),
            new(new CaptureClassificationMetadata(CaptureCategory.WebContent, [evidence], [new("website", [evidence])], coverage),
                AnalysisTestEnvironment.Producer(), DateTimeOffset.UtcNow, "v1", derivation: derivation),
            new(new ImageAltTextMetadata(new("A visible link.", [evidence]), coverage),
                AnalysisTestEnvironment.Producer(), DateTimeOffset.UtcNow, "v1", derivation: derivation),
            new(new CaptureNameMetadata(new("Private link name", [evidence]), coverage),
                AnalysisTestEnvironment.Producer(), DateTimeOffset.UtcNow, "v1", derivation: derivation),
        ];
    }
}
