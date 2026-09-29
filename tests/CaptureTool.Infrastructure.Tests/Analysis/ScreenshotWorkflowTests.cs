using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Infrastructure.Tests.Analysis;

public sealed partial class CaptureAnalysisWorkerTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task IndependentActionsCacheOnlyRequestedOutputsAcrossWorkerAndStoreRestarts(bool abstain)
    {
        var name = new ScreenshotProcessor(AnalysisCapability.CaptureName) { Abstain = abstain };
        var summary = new ScreenshotProcessor(AnalysisCapability.CaptureSynopsis) { Abstain = abstain };
        var alt = new ScreenshotProcessor(AnalysisCapability.ImageAltText) { Abstain = abstain };
        var classification = new ScreenshotProcessor(AnalysisCapability.CaptureClassification) { Abstain = abstain };
        IMetadataProcessor[] processors = [name, summary, alt, classification];
        using var fixture = new Fixture(processors: processors);
        fixture.Preferred.Execute = (_, _) => Task.FromResult(SourceSuccess(fixture));
        var request = await fixture.EnqueueAsync(Ct, capabilities: ImageAction(fixture, AnalysisCapability.CaptureName));
        await DrainAsync(fixture.Worker, Ct);
        var initial = (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!;
        Assert.HasCount(3, initial.Results);
        Assert.AreEqual(0, summary.Calls);
        Assert.AreEqual(0, alt.Calls);
        Assert.AreEqual(0, classification.Calls);
        fixture.Calls.Clear();
        foreach (var capability in new[] { AnalysisCapability.CaptureSynopsis, AnalysisCapability.ImageAltText,
            AnalysisCapability.CaptureName, AnalysisCapability.CaptureSynopsis, AnalysisCapability.ImageAltText, AnalysisCapability.CaptureClassification })
        {
            // Reopen the protected store and construct a new worker for every explicit action.
            using var reopened = fixture.Environment.CreateStore();
            using var worker = new CaptureAnalysisWorker(reopened, fixture.Authorization, fixture.Source,
                fixture.Configuration, fixture.Adapters, fixture.Catalog, processors);
            request = request with { RequestId = Guid.NewGuid(), ExpectedRunId = request.RequestId, ReuseExisting = true,
                Capabilities = ImageAction(fixture, capability) };
            Assert.IsTrue(await worker.EnqueueAsync(request, Ct));
            await DrainAsync(worker, Ct);
            var saved = (await reopened.GetAsync(request.CaptureId, cancellationToken: Ct))!;
            foreach (var original in initial.Results)
                Assert.AreEqual(original.ResultId, saved.Results.Single(result => result.Payload.Capability == original.Payload.Capability).ResultId);
        }
        Assert.IsEmpty(fixture.Calls, "Cached prerequisites must never prepare or run a model again.");
        Assert.AreEqual(1, name.Calls);
        Assert.AreEqual(1, summary.Calls);
        Assert.AreEqual(1, alt.Calls);
        Assert.AreEqual(1, classification.Calls);
        using var finalStore = fixture.Environment.CreateStore();
        var final = (await finalStore.GetAsync(request.CaptureId, cancellationToken: Ct))!;
        Assert.HasCount(6, final.Results);
        Assert.IsNull(((CaptureSynopsisMetadata)final.Results.Single(result => result.Payload is CaptureSynopsisMetadata).Payload).Title);

        // QR-dependent classification is invalidated; name, summary, and alt text stay cached.
        var withQr = final.WithResult(new(new QrCodeMetadata([new("https://example.com", new(.1, .1, .2, .2))]),
            new("qr", "test", "test", "1"), DateTimeOffset.UtcNow, final.PlanVersion));
        Assert.HasCount(6, withQr.Results);
        Assert.IsTrue(withQr.Results.Any(result => result.Payload is CaptureNameMetadata));
        Assert.IsTrue(withQr.Results.Any(result => result.Payload is CaptureSynopsisMetadata));
        Assert.IsTrue(withQr.Results.Any(result => result.Payload is ImageAltTextMetadata));
    }

    [TestMethod]
    public async Task ExplicitMultipleOutputsFinishBeforeReleasingModelResources()
    {
        var resources = new TestResources();
        var calls = new List<AnalysisCapability>();
        var processors = MetadataEnrichmentConfiguration.LanguageModelCapabilities.Select(capability =>
            new ScreenshotProcessor(capability) { Starting = () =>
            {
                Assert.AreEqual(0, resources.Releases, "The worker must not unload models between companion requests.");
                calls.Add(capability);
            }}).ToArray();
        using var fixture = new Fixture(processors: processors, resources: [resources]);
        fixture.Preferred.Execute = (_, _) => Task.FromResult(SourceSuccess(fixture));
        var committed = new List<AnalysisCapability>();
        fixture.Worker.ResultCommitted += (_, capability) => committed.Add(capability);
        await fixture.EnqueueAsync(Ct, capabilities: [AnalysisCapability.TextRecognition, AnalysisCapability.Description,
            AnalysisCapability.ImageAltText, AnalysisCapability.CaptureSynopsis, AnalysisCapability.CaptureName, AnalysisCapability.CaptureClassification]);
        await DrainAsync(fixture.Worker, Ct);
        CollectionAssert.AreEqual(new[] { AnalysisCapability.ImageAltText, AnalysisCapability.CaptureSynopsis,
            AnalysisCapability.CaptureName, AnalysisCapability.CaptureClassification }, calls);
        CollectionAssert.AreEqual(calls, committed.Skip(2).ToArray());
        Assert.IsGreaterThanOrEqualTo(1, resources.Releases, "Idle cleanup must still unload the model.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SlowClassificationCannotDiscardCompanionsOrPublishLateOutput(bool faults)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var classification = new ScreenshotProcessor(AnalysisCapability.CaptureClassification) { WaitForCompletion = completion.Task };
        var name = new ScreenshotProcessor(AnalysisCapability.CaptureName);
        var summary = new ScreenshotProcessor(AnalysisCapability.CaptureSynopsis);
        var alt = new ScreenshotProcessor(AnalysisCapability.ImageAltText);
        using var fixture = new Fixture(TimeSpan.FromMilliseconds(40), processors: [classification, name, summary, alt]);
        fixture.Preferred.Execute = (_, _) => Task.FromResult(SourceSuccess(fixture));
        // An explicit multi-output request must retain its remaining work after a timeout.
        var request = await fixture.EnqueueAsync(Ct, capabilities: [AnalysisCapability.TextRecognition, AnalysisCapability.Description,
            AnalysisCapability.CaptureClassification, AnalysisCapability.CaptureName, AnalysisCapability.CaptureSynopsis, AnalysisCapability.ImageAltText]);
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Task runner = fixture.Worker.RunAsync(shutdown.Token);
        try
        {
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.ProviderUnavailable, Ct);
            var waiting = (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!;
            Assert.IsTrue(waiting.Run.IsPending);
            Assert.AreEqual("provider-not-stopped", waiting.Run.CompletedSteps.Last().FailureCode);
            Assert.AreEqual(0, alt.Calls, "No companion may overlap the still-running native call.");
            if (faults) completion.SetException(new InvalidOperationException("Late native failure."));
            else completion.SetResult();
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.Idle, Ct);
            var saved = (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!;
            Assert.IsFalse(saved.Results.Any(result => result.Payload is CaptureClassificationMetadata), "Late classification must not publish.");
            Assert.IsTrue(saved.Results.Any(result => result.Payload is ImageAltTextMetadata));
            Assert.IsTrue(saved.Results.Any(result => result.Payload is CaptureNameMetadata));
            Assert.IsTrue(saved.Results.Any(result => result.Payload is CaptureSynopsisMetadata));
            Assert.AreEqual(1, classification.Calls);
            Assert.AreEqual(1, alt.Calls);
            Assert.AreEqual(AnalysisRunStatus.Completed, (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!.Run.Status);
        }
        finally
        {
            completion.TrySetResult();
            shutdown.Cancel();
            await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        }
    }

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
            ImageAction(fixture, AnalysisCapability.CaptureSynopsis));
        await DrainAsync(fixture.Worker, Ct);
        var original = (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!;
        fixture.Calls.Clear();
        var next = request with { RequestId = Guid.NewGuid(), ExpectedRunId = request.RequestId, ReuseExisting = true,
            Capabilities = ImageAction(fixture, AnalysisCapability.CaptureSynopsis) };
        Assert.IsTrue(await fixture.Worker.EnqueueAsync(next, Ct));
        await DrainAsync(fixture.Worker, Ct);
        CollectionAssert.AreEqual(cachedOcr && !cachedDescription ? new[] { "description" } : Array.Empty<string>(), fixture.Calls.ToArray());
        Assert.AreEqual(1, summary.Calls);
        var saved = (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!;
        foreach (var source in original.Results)
            Assert.AreEqual(source.ResultId, saved.Results.Single(result => result.Payload.Capability == source.Payload.Capability).ResultId);

        fixture.Calls.Clear();
        Assert.IsTrue(await fixture.Worker.EnqueueAsync(next with { RequestId = Guid.NewGuid(), ExpectedRunId = next.RequestId,
            Capabilities = ImageAction(fixture, AnalysisCapability.ImageAltText) }, Ct));
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
        var request = await fixture.EnqueueAsync(Ct, capabilities: ImageAction(fixture, AnalysisCapability.ImageAltText));
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
    public async Task FailedNameDoesNotDiscardSummaryAndRetryDoesNotRescanInputs()
    {
        var name = new ScreenshotProcessor(AnalysisCapability.CaptureName) { FailuresRemaining = 1 };
        var summary = new ScreenshotProcessor(AnalysisCapability.CaptureSynopsis);
        using var fixture = new Fixture(processors: [name, summary]);
        fixture.Preferred.Execute = (_, _) => Task.FromResult(SourceSuccess(fixture));
        var request = await fixture.EnqueueAsync(Ct, capabilities: ImageAction(fixture, AnalysisCapability.CaptureSynopsis));
        await DrainAsync(fixture.Worker, Ct);
        var original = (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!;
        fixture.Calls.Clear();
        for (int attempt = 0; attempt < 2; attempt++)
        {
            request = request with { RequestId = Guid.NewGuid(), ExpectedRunId = request.RequestId, ReuseExisting = true,
                Capabilities = ImageAction(fixture, AnalysisCapability.CaptureName) };
            Assert.IsTrue(await fixture.Worker.EnqueueAsync(request, Ct));
            await DrainAsync(fixture.Worker, Ct);
            var saved = (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!;
            foreach (var result in original.Results) Assert.IsTrue(saved.Results.Any(item => item.ResultId == result.ResultId));
        }
        Assert.IsEmpty(fixture.Calls);
        Assert.AreEqual(2, name.Calls);
        Assert.AreEqual(1, summary.Calls);
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
            ReuseExisting = true, Capabilities = ImageAction(fixture, AnalysisCapability.CaptureSynopsis) }, Ct));
        await DrainAsync(fixture.Worker, Ct);
        CollectionAssert.AreEqual(new[] { "preferred", "description" }, fixture.Calls.ToArray());
        Assert.AreEqual(2, summary.Calls);
        Assert.AreEqual(fixture.Source.Revision, (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!.SourceRevision);
    }

    private static IReadOnlyList<AnalysisCapability> ImageAction(Fixture fixture, AnalysisCapability capability) =>
        CaptureAnalysisConfiguration.ForAction(AnalysisMediaKind.Image, capability)
            .Where(requested => fixture.Configuration.Plans[0].Steps.Any(step => step.Capability == requested)).ToArray();

    private sealed class ScreenshotProcessor(AnalysisCapability capability) : IMetadataProcessor
    {
        public MetadataProcessorDescriptor Descriptor { get; } = MetadataEnrichmentConfiguration.CreateSemantic("fixture", capability);
        public int Calls { get; private set; }
        public int FailuresRemaining { get; set; }
        public bool Abstain { get; set; }
        public Action? Starting { get; init; }
        public Task? WaitForCompletion { get; init; }
        public async Task<AnalyzerOutcome> ProcessAsync(MetadataProcessorInput input, CancellationToken cancellationToken)
        {
            Calls++;
            Starting?.Invoke();
            if (WaitForCompletion != null) await WaitForCompletion; // Simulates native code that ignores cancellation.
            if (FailuresRemaining-- > 0) return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, "invalid-text-evidence");
            Assert.IsTrue(input.Inputs.Any(item => item.Capability == AnalysisCapability.TextRecognition && item.ResultId != null));
            Assert.IsTrue(input.Inputs.Any(item => item.Capability == AnalysisCapability.Description && item.ResultId != null));
            var entry = input.Entries[0];
            var text = Abstain ? null : new SuggestedText("A grounded image description.", [new(entry.ResultId, entry.EntryIndex, 0, entry.Text.Length)]);
            AnalysisPayload payload = capability == AnalysisCapability.ImageAltText ? new ImageAltTextMetadata(text, input.Coverage)
                : capability == AnalysisCapability.CaptureName ? new CaptureNameMetadata(text, input.Coverage)
                : capability == AnalysisCapability.CaptureClassification ? new CaptureClassificationMetadata(null, [], [], input.Coverage)
                : new CaptureSynopsisMetadata(null, text == null ? [] : [text], input.Coverage);
            return AnalyzerOutcome.Success(payload, new(Descriptor.Id, "fixture", "fixture", "1"));
        }
    }
}
