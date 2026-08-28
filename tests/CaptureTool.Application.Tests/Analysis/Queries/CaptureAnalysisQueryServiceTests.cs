using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Analysis.Queries;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Runtime.CompilerServices;

namespace CaptureTool.Application.Tests.Analysis.Queries;

[TestClass]
public sealed class CaptureAnalysisQueryServiceTests
{
    [TestMethod]
    public async Task GetAsync_ReturnsRecordFromCanonicalStore()
    {
        CaptureAnalysisRecord record = CreateRecord();
        var service = new CaptureAnalysisQueryService(new FakeStore(record));

        CaptureAnalysisRecord? result = await service.GetAsync(record.CaptureId);

        Assert.AreSame(record, result);
    }

    [TestMethod]
    public async Task GetCapabilityAsync_ReturnsOnlyMatchingCapabilitySchema()
    {
        CaptureAnalysisRecord record = CreateRecord();
        var service = new CaptureAnalysisQueryService(new FakeStore(record));

        CapabilityAnalysis? current = await service.GetCapabilityAsync(
            record.CaptureId,
            AnalysisCapabilities.MediaPropertiesV1);
        CapabilityAnalysis? future = await service.GetCapabilityAsync(
            record.CaptureId,
            new CapabilityDefinition(
                AnalysisCapabilities.MediaPropertiesV1.Id,
                new CapabilitySchemaVersion(2),
                CapabilityResultClassification.Observation));

        Assert.AreSame(record.Analyses.Single(), current);
        Assert.IsNull(future);
    }

    [TestMethod]
    public async Task ReadAllAsync_ProjectsRecordsWithoutExposingStoreRevisions()
    {
        CaptureAnalysisRecord first = CreateRecord();
        CaptureAnalysisRecord second = CreateRecord();
        var service = new CaptureAnalysisQueryService(new FakeStore(first, second));
        var results = new List<CaptureAnalysisRecord>();

        await foreach (CaptureAnalysisRecord record in service.ReadAllAsync())
        {
            results.Add(record);
        }

        CollectionAssert.AreEqual(new[] { first, second }, results);
    }

    private static CaptureAnalysisRecord CreateRecord()
    {
        CaptureId captureId = CaptureId.New();
        var capturedAt = new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
        var source = new SourceRevision(
            123,
            capturedAt,
            ContentFingerprint.Sha256(new string('a', 64)));
        var recipe = new CaptureAnalysisRecipe(
            new AnalysisRecipeId("query-test"),
            new AnalysisRecipeVersion(1),
            CaptureMediaKind.Image,
            [new RecipeCapability(
                AnalysisCapabilities.MediaPropertiesV1,
                RecipeCapabilityRequirement.Required)]);
        var analyzer = new AnalyzerIdentity(
            "query-test-analyzer",
            "query-test-provider",
            "query-test-model",
            "1",
            "1.0.0",
            "test-runtime",
            "1",
            "1",
            null);
        var result = new CanonicalCapabilityResult(
            captureId,
            source,
            new MediaPropertiesV1(CaptureMediaKind.Image, new PixelSize(10, 10)),
            analyzer,
            ProcessingBoundary.OnDevice,
            capturedAt);
        return new CaptureAnalysisRecord(
            captureId,
            CaptureMediaKind.Image,
            capturedAt,
            source,
            recipe,
            [new CapabilityAnalysis(AnalysisCapabilities.MediaPropertiesV1, result, null)]);
    }

    private sealed class FakeStore(params CaptureAnalysisRecord[] records) : ICaptureAnalysisStore
    {
        public ValueTask<CaptureAnalysisStoreSnapshot?> GetAsync(
            CaptureId captureId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CaptureAnalysisRecord? record = records.FirstOrDefault(value => value.CaptureId == captureId);
            return ValueTask.FromResult(record == null
                ? null
                : new CaptureAnalysisStoreSnapshot(1, record));
        }

        public async IAsyncEnumerable<CaptureAnalysisStoreSnapshot> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            foreach (CaptureAnalysisRecord record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new CaptureAnalysisStoreSnapshot(1, record);
            }
        }
    }
}
