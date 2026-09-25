using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Infrastructure.Tests.Analysis;

[TestClass]
public sealed class AnalysisExecutionStoreTests
{
    public TestContext TestContext { get; set; } = null!;
    private CancellationToken Ct => TestContext.CancellationToken;
    private static readonly Guid Authorization = Guid.Parse("118f2b1a-0308-4ea8-9cdf-8a832c617265");
    internal static MediaAnalysisPlan Plan => new(AnalysisMediaKind.Image, "v1", [
        new(AnalysisCapability.TextRecognition, ["ocr"], TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)),
        new(AnalysisCapability.Description, ["description"], TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)),
    ]);

    [TestMethod]
    public async Task PendingDiscoveryYieldsToClearAndDiscardsWorkFromTheClearedGeneration()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        var scope = await store.GetAdmissionScopeAsync(Ct);
        for (int index = 0; index < 40; index++) await store.AdmitAsync(Request(environment, scope), Authorization, Plan, Ct);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        environment.Files.BeforeRead = async (path, ct) =>
        {
            if (!path.EndsWith(".analysis", StringComparison.Ordinal)) return;
            if (Interlocked.Increment(ref reads) == 1)
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(ct);
            }
        };
        Task<IReadOnlyList<AnalysisWorkItem>> discovery = store.ReadPendingAsync(Ct);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Task<AnalysisCleanupResult> clear = store.ClearAsync(40, Ct);
        release.TrySetResult();
        Assert.IsTrue((await clear).Completed);
        Assert.IsEmpty(await discovery, "A discovery interrupted by clear must discard the previous generation.");
        Assert.IsLessThan(40, reads, "Clear must run before discovery reads every document.");
        Assert.AreEqual(40, (await store.GetAdmissionScopeAsync(Ct)).ReconciliationBoundary);
    }

    [TestMethod]
    public async Task AdmissionSurvivesRestartInFifoOrderAndStaleReplayCannotSupersedeNewWork()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        AnalysisAdmissionScope scope = await store.GetAdmissionScopeAsync(Ct);
        AnalysisRequest first = Request(environment, scope);
        AnalysisRequest second = Request(environment, scope);
        Assert.IsTrue(await store.AdmitAsync(first, Authorization, Plan, Ct));
        Assert.IsTrue(await store.AdmitAsync(second, Authorization, Plan, Ct));
        Assert.IsTrue(await store.AdmitAsync(first, Authorization, Plan, Ct));
        using var restarted = environment.CreateStore();
        IReadOnlyList<AnalysisWorkItem> pending = await restarted.ReadPendingAsync(Ct);
        CollectionAssert.AreEqual(new[] { first.RequestId, second.RequestId }, pending.Select(work => work.Run.Id).ToArray());
        Assert.IsNull(await restarted.GetAsync(first.CaptureId, cancellationToken: Ct));
        AnalysisRequest newer = first with { RequestId = Guid.NewGuid(), ExpectedRunId = first.RequestId };
        Assert.IsTrue(await restarted.AdmitAsync(newer, Authorization, Plan, Ct));
        Assert.IsFalse(await restarted.AdmitAsync(first, Authorization, Plan, Ct));
        Assert.IsFalse(await restarted.BindSourceAsync(pending[0].Token, AnalysisTestEnvironment.Revision(), Ct));
        Assert.IsFalse(await restarted.AdmitAsync(newer with { SourcePath = Path.Combine(environment.Root, "other.png") }, Authorization, Plan, Ct));
    }

    [TestMethod]
    public async Task CommitIsAtomicAndReanalysisKeepsProvenanceWithoutCountingOldSuccessAsCompleted()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        AnalysisRequest request = Request(environment, await store.GetAdmissionScopeAsync(Ct));
        await store.AdmitAsync(request, Authorization, Plan, Ct);
        AnalysisWorkItem work = (await store.GetWorkAsync(request.CaptureId, Ct))!;
        await store.BindSourceAsync(work.Token, AnalysisTestEnvironment.Revision(), Ct);
        AnalysisResult text = Result(AnalysisCapability.TextRecognition, request.RequestId);
        environment.Files.BeforeWrite = (_, _) => throw new IOException("Injected write failure.");
        await Assert.ThrowsExactlyAsync<IOException>(() => store.CommitStepAsync(work.Token,
            new(AnalysisCapability.TextRecognition, AnalyzerOutcomeKind.Succeeded, null), text, Ct));
        environment.Files.BeforeWrite = null;
        Assert.IsEmpty((await store.GetWorkAsync(request.CaptureId, Ct))!.Run.CompletedSteps);
        Assert.IsEmpty((await store.GetAsync(request.CaptureId, cancellationToken: Ct))!.Results);
        Assert.IsTrue(await store.CommitStepAsync(work.Token, new(AnalysisCapability.TextRecognition, AnalyzerOutcomeKind.Succeeded, null), text, Ct));
        Assert.IsTrue(await store.CommitStepAsync(work.Token, new(AnalysisCapability.Description, AnalyzerOutcomeKind.Succeeded, null),
            Result(AnalysisCapability.Description, request.RequestId), Ct));
        using var restarted = environment.CreateStore();
        Assert.IsEmpty(await restarted.ReadPendingAsync(Ct));
        Assert.AreEqual(AnalysisRunStatus.Completed, (await restarted.GetWorkAsync(request.CaptureId, Ct))!.Run.Status);

        AnalysisRequest refresh = request with { RequestId = Guid.NewGuid(), ExpectedRunId = request.RequestId };
        await restarted.AdmitAsync(refresh, Authorization, Plan, Ct);
        AnalysisWorkItem refreshing = (await restarted.GetWorkAsync(request.CaptureId, Ct))!;
        await restarted.BindSourceAsync(refreshing.Token, AnalysisTestEnvironment.Revision(), Ct);
        Assert.IsEmpty(refreshing.Run.CompletedSteps);
        Assert.IsTrue((await restarted.GetAsync(request.CaptureId, cancellationToken: Ct))!.Results.All(result => result.ProducingRunId == request.RequestId));
        await restarted.CommitStepAsync(refreshing.Token, new(AnalysisCapability.TextRecognition, AnalyzerOutcomeKind.Succeeded, null),
            Result(AnalysisCapability.TextRecognition, refresh.RequestId), Ct);
        await restarted.CommitStepAsync(refreshing.Token, new(AnalysisCapability.Description, AnalyzerOutcomeKind.Failed, "model-failed"), null, Ct);
        CaptureAnalysisRecord record = (await restarted.GetAsync(request.CaptureId, cancellationToken: Ct))!;
        Assert.AreEqual(refresh.RequestId, record.Results.Single(result => result.Payload is TextRecognitionMetadata).ProducingRunId);
        Assert.AreEqual(request.RequestId, record.Results.Single(result => result.Payload is DescriptionMetadata).ProducingRunId);
        Assert.IsFalse(await restarted.CommitStepAsync(refreshing.Token, new(AnalysisCapability.Description, AnalyzerOutcomeKind.Succeeded, null),
            Result(AnalysisCapability.Description, refresh.RequestId), Ct));
    }

    [TestMethod]
    public async Task ClearFencesQueuedAndActiveWorkAcrossRestartAndPersistsReconciliationBoundary()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        AnalysisRequest active = Request(environment, await store.GetAdmissionScopeAsync(Ct));
        AnalysisRequest pending = Request(environment, await store.GetAdmissionScopeAsync(Ct));
        await store.AdmitAsync(active, Authorization, Plan, Ct);
        await store.AdmitAsync(pending, Authorization, Plan, Ct);
        AnalysisRunToken token = (await store.GetWorkAsync(active.CaptureId, Ct))!.Token;
        await store.BindSourceAsync(token, AnalysisTestEnvironment.Revision(), Ct);
        File.Delete(environment.ControlPath); // Explicit recovery has the same fencing guarantees.
        environment.Files.FailCleanup = true;
        Assert.IsFalse((await store.ClearAsync(42, Ct)).Completed);
        using var restarted = environment.CreateStore();
        Assert.IsEmpty(await restarted.ReadPendingAsync(Ct));
        Assert.AreEqual(42, (await restarted.GetAdmissionScopeAsync(Ct)).ReconciliationBoundary);
        Assert.IsFalse(await restarted.AdmitAsync(pending, Authorization, Plan, Ct));
        Assert.IsFalse(await restarted.BindSourceAsync(token, AnalysisTestEnvironment.Revision(), Ct));
        Assert.IsFalse(await restarted.CommitStepAsync(token, new(AnalysisCapability.TextRecognition, AnalyzerOutcomeKind.Succeeded, null),
            Result(AnalysisCapability.TextRecognition, active.RequestId), Ct));
        environment.Files.FailCleanup = false;
        await restarted.InitializeAsync(Ct);
        Assert.IsEmpty(environment.MetadataPaths);
    }

    [TestMethod]
    public async Task TerminalAndChangedSourceRequestsRejectFurtherPublication()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        AnalysisRequest request = Request(environment, await store.GetAdmissionScopeAsync(Ct));
        await store.AdmitAsync(request, Authorization, Plan, Ct);
        AnalysisRunToken token = (await store.GetWorkAsync(request.CaptureId, Ct))!.Token;
        await store.BindSourceAsync(token, AnalysisTestEnvironment.Revision(), Ct);
        Assert.IsFalse(await store.BindSourceAsync(token, AnalysisTestEnvironment.Revision('b'), Ct));
        await store.FinishAsync(token, AnalysisRunStatus.Cancelled, Ct);
        Assert.IsFalse(await store.BindSourceAsync(token, AnalysisTestEnvironment.Revision(), Ct));
        Assert.IsFalse(await store.CommitStepAsync(token, new(AnalysisCapability.TextRecognition, AnalyzerOutcomeKind.Succeeded, null),
            Result(AnalysisCapability.TextRecognition, request.RequestId), Ct));
        Assert.IsEmpty(await store.ReadPendingAsync(Ct));
    }

    [TestMethod]
    public async Task CompletionWithoutItsProducingResultFailsClosed()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        AnalysisRequest request = Request(environment, await store.GetAdmissionScopeAsync(Ct));
        await store.AdmitAsync(request, Authorization, Plan, Ct);
        AnalysisRunToken token = (await store.GetWorkAsync(request.CaptureId, Ct))!.Token;
        await store.BindSourceAsync(token, AnalysisTestEnvironment.Revision(), Ct);
        await store.CommitStepAsync(token, new(AnalysisCapability.TextRecognition, AnalyzerOutcomeKind.Succeeded, null),
            Result(AnalysisCapability.TextRecognition, request.RequestId), Ct);
        string path = environment.MetadataPaths.Single();
        byte[] bytes = environment.Protector.Unprotect(await File.ReadAllBytesAsync(path, Ct));
        var document = System.Text.Json.Nodes.JsonNode.Parse(bytes)!;
        document["Results"]![0]!["ProducingRunId"] = Guid.NewGuid().ToString();
        await File.WriteAllBytesAsync(path, environment.Protector.Protect(System.Text.Encoding.UTF8.GetBytes(document.ToJsonString())), Ct);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.ReadPendingAsync(Ct));
    }

    [TestMethod]
    public async Task AdmissionRacingClearCannotSurviveTheGenerationChange()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        AnalysisRequest request = Request(environment, await store.GetAdmissionScopeAsync(Ct));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        environment.Files.BeforeWrite = async (_, ct) => { entered.TrySetResult(); await release.Task.WaitAsync(ct); };
        Task<bool> admission = store.AdmitAsync(request, Authorization, Plan, Ct);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Task<AnalysisCleanupResult> clear = store.ClearAsync(42, Ct);
        release.SetResult();
        Assert.IsTrue(await admission);
        await clear;
        Assert.IsEmpty(await store.ReadPendingAsync(Ct));
        Assert.IsFalse(await store.AdmitAsync(request, Authorization, Plan, Ct));
    }

    internal static AnalysisRequest Request(AnalysisTestEnvironment environment, AnalysisAdmissionScope scope) =>
        new(CaptureId.New(), AnalysisMediaKind.Image, Path.Combine(environment.Root, "image.png"), Guid.NewGuid(), scope.Generation);

    internal static AnalysisResult Result(AnalysisCapability capability, Guid runId) => new(
        capability == AnalysisCapability.TextRecognition ? new TextRecognitionMetadata([]) : new DescriptionMetadata([new("description")]),
        AnalysisTestEnvironment.Producer(), DateTimeOffset.UtcNow, "v1", runId);
}
