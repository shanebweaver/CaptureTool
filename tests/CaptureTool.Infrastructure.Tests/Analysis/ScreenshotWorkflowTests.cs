using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Infrastructure.Tests.Analysis;

public sealed partial class CaptureAnalysisWorkerTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task ScreenshotActionsReusePrerequisitesAndKeepIndependentOutputs(bool cachedOcr, bool cachedDescription)
    {
        var summary = new ScreenshotProcessor(AnalysisCapability.CaptureSynopsis);
        var alt = new ScreenshotProcessor(AnalysisCapability.ImageAltText);
        using var fixture = new Fixture(processors: [summary, alt]);
        fixture.Preferred.Execute = (_, _) => Task.FromResult(SourceSuccess(fixture));
        AnalysisCapability[] initial = cachedDescription ? [AnalysisCapability.TextRecognition, AnalysisCapability.Description]
            : [AnalysisCapability.TextRecognition];
        var request = await fixture.EnqueueAsync(Ct, capabilities: cachedOcr ? initial :
            CaptureAnalysisConfiguration.ForAction(AnalysisMediaKind.Image, AnalysisCapability.CaptureSynopsis));
        await DrainAsync(fixture.Worker, Ct);
        var original = (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!;
        fixture.Calls.Clear();
        var next = request with { RequestId = Guid.NewGuid(), ExpectedRunId = request.RequestId, ReuseExisting = true,
            Capabilities = CaptureAnalysisConfiguration.ForAction(AnalysisMediaKind.Image, AnalysisCapability.CaptureSynopsis) };
        Assert.IsTrue(await fixture.Worker.EnqueueAsync(next, Ct));
        await DrainAsync(fixture.Worker, Ct);
        CollectionAssert.AreEqual(cachedOcr && !cachedDescription ? new[] { "description" } : Array.Empty<string>(), fixture.Calls.ToArray());
        Assert.AreEqual(1, summary.Calls);
        var saved = (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!;
        foreach (var source in original.Results)
            Assert.AreEqual(source.ResultId, saved.Results.Single(result => result.Payload.Capability == source.Payload.Capability).ResultId);

        fixture.Calls.Clear();
        Assert.IsTrue(await fixture.Worker.EnqueueAsync(next with { RequestId = Guid.NewGuid(), ExpectedRunId = next.RequestId,
            Capabilities = CaptureAnalysisConfiguration.ForAction(AnalysisMediaKind.Image, AnalysisCapability.ImageAltText) }, Ct));
        await DrainAsync(fixture.Worker, Ct);
        Assert.IsEmpty(fixture.Calls);
        Assert.AreEqual(1, summary.Calls);
        Assert.AreEqual(1, alt.Calls);
        using var reopened = fixture.Environment.CreateStore();
        var record = (await reopened.GetAsync(request.CaptureId, cancellationToken: Ct))!;
        Assert.HasCount(4, record.Results);
        Assert.AreEqual(saved.Results.Single(result => result.Payload is CaptureSynopsisMetadata).ResultId,
            record.Results.Single(result => result.Payload is CaptureSynopsisMetadata).ResultId);
        var work = (await reopened.GetWorkAsync(request.CaptureId, Ct))!;
        Assert.AreEqual(AnalysisRunStatus.Completed, work.Run.Status);
        Assert.HasCount(2, work.Run.CompletedSteps.Where(step => step.ReusedResultId != null));
        Assert.IsNotNull(((ImageAltTextMetadata)record.Results.Single(result => result.Payload is ImageAltTextMetadata).Payload).Suggestion);
    }

    [TestMethod]
    public async Task FailedAltTextCanBeRetriedWithoutRepeatingItsSuccessfulPrerequisites()
    {
        var alt = new ScreenshotProcessor(AnalysisCapability.ImageAltText) { FailuresRemaining = 1 };
        using var fixture = new Fixture(processors: [alt]);
        fixture.Preferred.Execute = (_, _) => Task.FromResult(SourceSuccess(fixture));
        var request = await fixture.EnqueueAsync(Ct, capabilities: CaptureAnalysisConfiguration.ForAction(AnalysisMediaKind.Image, AnalysisCapability.ImageAltText));
        await DrainAsync(fixture.Worker, Ct);
        var before = (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!;
        Assert.HasCount(2, before.Results);
        fixture.Calls.Clear();
        Assert.IsTrue(await fixture.Worker.EnqueueAsync(request with { RequestId = Guid.NewGuid(), ExpectedRunId = request.RequestId, ReuseExisting = true }, Ct));
        await DrainAsync(fixture.Worker, Ct);
        Assert.IsEmpty(fixture.Calls);
        Assert.AreEqual(2, alt.Calls);
        var after = (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!;
        Assert.HasCount(3, after.Results);
        foreach (var result in before.Results) Assert.IsTrue(after.Results.Any(saved => saved.ResultId == result.ResultId));
    }

    [TestMethod]
    public async Task ChangedImageCannotReusePreviouslySavedPrerequisitesOrSummary()
    {
        var summary = new ScreenshotProcessor(AnalysisCapability.CaptureSynopsis);
        using var fixture = new Fixture(processors: [summary]);
        fixture.Preferred.Execute = (_, _) => Task.FromResult(SourceSuccess(fixture));
        var request = await fixture.EnqueueAsync(Ct);
        await DrainAsync(fixture.Worker, Ct);
        fixture.Calls.Clear();
        fixture.Source.Revision = new(new string('b', 64));
        Assert.IsTrue(await fixture.Worker.EnqueueAsync(request with { RequestId = Guid.NewGuid(), ExpectedRunId = request.RequestId,
            ReuseExisting = true, Capabilities = CaptureAnalysisConfiguration.ForAction(AnalysisMediaKind.Image, AnalysisCapability.CaptureSynopsis) }, Ct));
        await DrainAsync(fixture.Worker, Ct);
        CollectionAssert.AreEqual(new[] { "preferred", "description" }, fixture.Calls.ToArray());
        Assert.AreEqual(2, summary.Calls);
        Assert.AreEqual(fixture.Source.Revision, (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!.SourceRevision);
    }

    private sealed class ScreenshotProcessor(AnalysisCapability capability) : IMetadataProcessor
    {
        public MetadataProcessorDescriptor Descriptor { get; } = MetadataEnrichmentConfiguration.CreateSemantic("fixture", capability);
        public int Calls { get; private set; }
        public int FailuresRemaining { get; set; }
        public Task<AnalyzerOutcome> ProcessAsync(MetadataProcessorInput input, CancellationToken cancellationToken)
        {
            Calls++;
            if (FailuresRemaining-- > 0) return Task.FromResult(AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, "invalid-text-evidence"));
            Assert.IsTrue(input.Inputs.Any(item => item.Capability == AnalysisCapability.TextRecognition && item.ResultId != null));
            Assert.IsTrue(input.Inputs.Any(item => item.Capability == AnalysisCapability.Description && item.ResultId != null));
            var entry = input.Entries[0];
            var text = new SuggestedText("A grounded image description.", [new(entry.ResultId, entry.EntryIndex, 0, entry.Text.Length)]);
            AnalysisPayload payload = capability == AnalysisCapability.ImageAltText ? new ImageAltTextMetadata(text, input.Coverage)
                : new CaptureSynopsisMetadata(text, [text], input.Coverage);
            return Task.FromResult(AnalyzerOutcome.Success(payload, new(Descriptor.Id, "fixture", "fixture", "1")));
        }
    }
}
