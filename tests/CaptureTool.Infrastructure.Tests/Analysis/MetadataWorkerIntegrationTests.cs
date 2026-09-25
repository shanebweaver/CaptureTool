using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Infrastructure.Tests.Analysis;

public sealed partial class CaptureAnalysisWorkerTests
{
    [TestMethod]
    [DataRow(AnalysisMediaKind.Image)]
    [DataRow(AnalysisMediaKind.Audio)]
    [DataRow(AnalysisMediaKind.Video)]
    public async Task EnrichmentConsumesCommittedSourcesInOrderForEachMediaKind(AnalysisMediaKind kind)
    {
        var synopsis = new TestProcessor("synopsis");
        using var fixture = new Fixture(processors: [new StructuredFactsProcessor(), synopsis], kind: kind);
        fixture.Preferred.Execute = (_, _) => Task.FromResult(SourceSuccess(fixture, "Invoice USD 12.00", kind));
        var request = await fixture.EnqueueAsync(Ct);
        await DrainAsync(fixture.Worker, Ct);
        var record = (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!;
        Assert.HasCount(1, ((StructuredFactsMetadata)record.Results.Single(result => result.Payload is StructuredFactsMetadata).Payload).Facts);
        var result = record.Results.Single(result => result.Payload is CaptureSynopsisMetadata);
        Assert.AreEqual(request.RequestId, result.ProducingRunId);
        Assert.IsTrue(result.Derivation!.Matches(record.Results));
        Assert.AreEqual("Invoice USD 12.00", ((CaptureSynopsisMetadata)result.Payload).Title!.Text);
        Assert.AreEqual(1, synopsis.Calls);
        Assert.AreEqual(AnalysisActivity.Idle, fixture.Worker.Progress.Activity);
        using var reopened = fixture.Environment.CreateStore();
        Assert.IsNotNull((await reopened.GetAsync(request.CaptureId, cancellationToken: Ct))!.Results.Single(result => result.Payload is CaptureSynopsisMetadata));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MissingOrEmptyInputsSkipPreparationAndInference(bool failedSource)
    {
        var processor = new TestProcessor("synopsis");
        using var fixture = new Fixture(processors: [processor]);
        if (failedSource)
            foreach (var analyzer in new[] { fixture.Preferred, fixture.Fallback })
                analyzer.Execute = (_, _) => Task.FromResult(AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, "source-failed"));
        await fixture.EnqueueAsync(Ct);
        await DrainAsync(fixture.Worker, Ct);
        Assert.AreEqual(0, processor.Probes);
        Assert.AreEqual(0, processor.Calls);
    }

    [TestMethod]
    [DataRow(AnalyzerOutcomeKind.InvalidSource)]
    [DataRow(AnalyzerOutcomeKind.Failed)]
    [DataRow(AnalyzerOutcomeKind.TemporarilyUnavailable)]
    public async Task MetadataFailuresUseFallbackWithoutInvalidatingTheMedia(AnalyzerOutcomeKind failure)
    {
        var preferred = new TestProcessor("semantic-preferred") { Execute = (_, _) => Task.FromResult(AnalyzerOutcome.Unsuccessful(failure, "bad-metadata")) };
        var fallback = new TestProcessor("semantic-fallback");
        using var fixture = new Fixture(processors: [preferred, fallback]);
        fixture.Preferred.Execute = (_, _) => Task.FromResult(SourceSuccess(fixture));
        var request = await fixture.EnqueueAsync(Ct);
        await DrainAsync(fixture.Worker, Ct);
        Assert.AreEqual(1, preferred.Calls);
        Assert.AreEqual(1, fallback.Calls);
        Assert.AreEqual(AnalysisRunStatus.Completed, (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!.Run.Status);
        Assert.AreEqual("semantic-fallback", (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!.Results.Last().Producer.AnalyzerId);
    }

    [TestMethod]
    public async Task InvalidEvidenceIsRejectedBeforePublicationAndFallsBack()
    {
        var bad = new TestProcessor("bad");
        bad.Execute = (input, _) => Task.FromResult(AnalyzerOutcome.Success(new CaptureSynopsisMetadata(
            new("Invented", [new(Guid.NewGuid(), 0, 0, 1)]), [], input.Coverage), new("bad", "test", "test", "1")));
        var good = new TestProcessor("good");
        using var fixture = new Fixture(processors: [bad, good]);
        fixture.Preferred.Execute = (_, _) => Task.FromResult(SourceSuccess(fixture));
        await fixture.EnqueueAsync(Ct);
        await DrainAsync(fixture.Worker, Ct);
        Assert.AreEqual(1, good.Calls);
        Assert.IsFalse(fixture.Worker.Progress.LastRunHadFailures);
    }

    [TestMethod]
    [DataRow("delete")]
    [DataRow("revoke")]
    [DataRow("source")]
    [DataRow("protect")]
    public async Task LateEnrichmentCannotPublishAcrossPrivacySourceOrStorageBoundaries(string action)
    {
        var processor = new TestProcessor("synopsis");
        using var fixture = new Fixture(processors: [processor]);
        fixture.Preferred.Execute = (_, _) => Task.FromResult(SourceSuccess(fixture));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        processor.Execute = async (input, _) => { started.SetResult(); await release.Task; return processor.Success(input); };
        var request = await fixture.EnqueueAsync(Ct);
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Task running = fixture.Worker.RunAsync(shutdown.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            Assert.AreEqual(AnalysisActivity.Analyzing, fixture.Worker.Progress.Activity);
            if (action == "delete") await fixture.Worker.ClearAsync(0, Ct);
            if (action == "revoke") await fixture.Authorization.ChangeAsync(false, Ct);
            if (action == "source") fixture.Source.Unchanged = false;
            if (action == "protect") fixture.Environment.Protector.FailProtection = true;
            release.SetResult();
            await WaitForSnapshotAsync(fixture.Worker, progress => progress.Activity is AnalysisActivity.Idle or AnalysisActivity.StorageUnavailable, Ct);
            fixture.Environment.Protector.FailProtection = false;
            var record = await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct);
            Assert.IsFalse(record?.Results.Any(result => result.Payload is DerivedAnalysisPayload) ?? false);
            if (action == "delete") Assert.IsNull(record);
            if (action == "protect") Assert.AreEqual(AnalysisActivity.StorageUnavailable, fixture.Worker.Progress.Activity);
        }
        finally { release.TrySetResult(); shutdown.Cancel(); await running.WaitAsync(TimeSpan.FromSeconds(5), Ct); }
    }

    [TestMethod]
    public async Task RestartResumesEnrichmentWithoutRepeatingCommittedMediaSteps()
    {
        var processor = new TestProcessor("synopsis");
        using var fixture = new Fixture(processors: [processor]);
        var request = await fixture.EnqueueAsync(Ct);
        var work = (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!;
        await fixture.Store.BindSourceAsync(work.Token, AnalysisTestEnvironment.Revision(), Ct);
        var source = new AnalysisResult(new TextRecognitionMetadata([new("Resume evidence")]), new("preferred", "test", "model", "1"),
            DateTimeOffset.UtcNow, "v1", work.Run.Id);
        await fixture.Store.CommitStepAsync(work.Token, new(AnalysisCapability.TextRecognition, AnalyzerOutcomeKind.Succeeded, null), source, Ct);
        await fixture.Store.CommitStepAsync(work.Token, new(AnalysisCapability.Description, AnalyzerOutcomeKind.Unsupported, "unsupported"), null, Ct);
        using var reopened = fixture.Environment.CreateStore();
        using var worker = new CaptureAnalysisWorker(reopened, fixture.Authorization, fixture.Source, fixture.Configuration, fixture.Adapters, fixture.Catalog, [processor]);
        await DrainAsync(worker, Ct);
        Assert.IsEmpty(fixture.Calls);
        Assert.AreEqual(1, processor.Calls);
        Assert.AreEqual(AnalysisRunStatus.Completed, (await reopened.GetWorkAsync(request.CaptureId, Ct))!.Run.Status);
    }

    private static AnalyzerOutcome SourceSuccess(Fixture fixture, string text = "Grounded source", AnalysisMediaKind kind = AnalysisMediaKind.Image) =>
        AnalyzerOutcome.Success(kind == AnalysisMediaKind.Audio ? new TranscriptMetadata(null, [new(text, TimeSpan.Zero, TimeSpan.FromSeconds(1))]) :
            new TextRecognitionMetadata([new(text)]), new(fixture.Preferred.Descriptor.Id, "test", "model", "1"));

    [TestMethod]
    public async Task CooperativeMetadataTimeoutFallsBackThroughTheSharedAttemptPolicy()
    {
        var slow = new TestProcessor("slow") { Execute = async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); throw new InvalidOperationException(); } };
        var fallback = new TestProcessor("fallback-metadata");
        using var fixture = new Fixture(TimeSpan.FromMilliseconds(40), [slow, fallback]);
        fixture.Preferred.Execute = (_, _) => Task.FromResult(SourceSuccess(fixture));
        await fixture.EnqueueAsync(Ct);
        await DrainAsync(fixture.Worker, Ct);
        Assert.AreEqual(1, fallback.Calls);
        Assert.IsFalse(fixture.Worker.Progress.LastRunHadFailures);
    }

    [TestMethod]
    public async Task SameSourceRefreshCanUseRetainedSuccessfulInputsWithOriginalProvenance()
    {
        var processor = new TestProcessor("synopsis");
        using var fixture = new Fixture(processors: [processor]);
        fixture.Preferred.Execute = (_, _) => Task.FromResult(SourceSuccess(fixture));
        var request = await fixture.EnqueueAsync(Ct);
        await DrainAsync(fixture.Worker, Ct);
        var old = (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!.Results.Single(result => result.Payload is TextRecognitionMetadata);
        fixture.Preferred.Execute = fixture.Fallback.Execute = (_, _) => Task.FromResult(AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, "refresh-failed"));
        var refresh = request with { RequestId = Guid.NewGuid(), ExpectedRunId = request.RequestId };
        Assert.IsTrue(await fixture.Worker.EnqueueAsync(refresh, Ct));
        await DrainAsync(fixture.Worker, Ct);
        var record = (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!;
        Assert.AreEqual(old.ResultId, record.Results.Single(result => result.Payload is TextRecognitionMetadata).ResultId);
        var insight = record.Results.Single(result => result.Payload is CaptureSynopsisMetadata);
        Assert.AreEqual(refresh.RequestId, insight.ProducingRunId);
        Assert.Contains(new AnalysisInputReference(AnalysisCapability.TextRecognition, old.ResultId), insight.Derivation!.Inputs);
    }

    private sealed class TestProcessor : IMetadataProcessor
    {
        public MetadataProcessorDescriptor Descriptor { get; }
        public Func<MetadataProcessorInput, CancellationToken, Task<AnalyzerOutcome>> Execute { get; set; }
        public int Calls { get; private set; }
        public int Probes { get; private set; }
        public TestProcessor(string id)
        {
            Descriptor = new(id, "1", AnalysisCapability.CaptureSynopsis, [AnalysisCapability.TextRecognition, AnalysisCapability.Transcription],
                new(10, 1000, 1000, TimeSpan.FromSeconds(5)));
            Execute = (input, _) => Task.FromResult(Success(input));
        }
        public ValueTask<AnalyzerAvailability> GetAvailabilityAsync(CancellationToken ct) { Probes++; return ValueTask.FromResult(AnalyzerAvailability.Ready); }
        public Task<AnalyzerOutcome> ProcessAsync(MetadataProcessorInput input, CancellationToken ct) { Calls++; return Execute(input, ct); }
        public AnalyzerOutcome Success(MetadataProcessorInput input)
        {
            var entry = input.Entries[0];
            return AnalyzerOutcome.Success(new CaptureSynopsisMetadata(new(entry.Text, [new(entry.ResultId, entry.EntryIndex, 0, entry.Text.Length)]), [], input.Coverage),
                new(Descriptor.Id, "test", "model", "1"));
        }
    }
}
