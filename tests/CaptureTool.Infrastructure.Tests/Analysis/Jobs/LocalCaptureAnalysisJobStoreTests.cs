using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Jobs;
using CaptureTool.Infrastructure.Analysis.Persistence;
using CaptureTool.Infrastructure.Tests.Analysis.Persistence;
using System.Diagnostics;
using System.Text;

namespace CaptureTool.Infrastructure.Tests.Analysis.Jobs;

[TestClass]
public sealed class LocalCaptureAnalysisJobStoreTests
{
    private static readonly DateTimeOffset EnqueuedAtUtc =
        new(2026, 8, 7, 18, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Enqueue_ShouldPersistBeforeExecutionAndCoalesceExactIntent()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        var protector = new TestDataProtectionService();
        CaptureAnalysisJobKey key = CreateKey();
        using LocalCaptureAnalysisJobStore store = CreateStore(root, protector);

        CaptureAnalysisJobEnqueueResult first = await store.TryEnqueueAsync(key, EnqueuedAtUtc);
        CaptureAnalysisJobEnqueueResult second = await store.TryEnqueueAsync(key, EnqueuedAtUtc);

        Assert.AreEqual(CaptureAnalysisJobEnqueueStatus.Enqueued, first.Status);
        Assert.AreEqual(CaptureAnalysisJobEnqueueStatus.AlreadyExists, second.Status);
        Assert.AreEqual(first.Intent, second.Intent);
        string filePath = store.GetJobFilePath(key);
        Assert.IsTrue(File.Exists(filePath));
        Assert.DoesNotContain(key.CaptureId.ToString(), Path.GetFileName(filePath));
        Assert.HasCount(64, Path.GetFileNameWithoutExtension(filePath));
        string protectedText = Encoding.UTF8.GetString(File.ReadAllBytes(filePath));
        Assert.DoesNotContain("SOURCE-PATH-CANARY", protectedText);
        string plaintext = Encoding.UTF8.GetString(protector.Unprotect(File.ReadAllBytes(filePath)));
        Assert.DoesNotContain("SOURCE-PATH-CANARY", plaintext);
        Assert.DoesNotContain("contentBytes", plaintext, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task ExpiredLease_ShouldRestartSameIntentAndCompleteRecordedResult()
    {
        using LocalCaptureAnalysisJobStore store = CreateStore(
            AnalysisPersistenceTestData.CreateTestFolder());
        CaptureAnalysisJobKey key = CreateKey();
        _ = await store.TryEnqueueAsync(key, EnqueuedAtUtc);
        CaptureAnalysisJobLease firstLease = (await store.TryLeaseNextDueAsync(
            EnqueuedAtUtc,
            TimeSpan.FromMinutes(1)))!;
        CaptureAnalyzerAttempt success = CreateAttempt(
            1,
            CaptureAnalyzerAttemptStatus.Succeeded,
            failure: null);
        Assert.AreEqual(
            CaptureAnalysisJobMutationStatus.Succeeded,
            (await store.TryRecordAttemptAsync(firstLease.LeaseToken, success)).Status);

        Assert.AreEqual(1, await store.RecoverExpiredLeasesAsync(EnqueuedAtUtc.AddMinutes(1)));
        CaptureAnalysisJobLease restarted = (await store.TryLeaseNextDueAsync(
            EnqueuedAtUtc.AddMinutes(1),
            TimeSpan.FromMinutes(1)))!;

        Assert.AreEqual(key, restarted.Intent.Key);
        Assert.AreNotEqual(firstLease.LeaseToken, restarted.LeaseToken);
        Assert.HasCount(1, restarted.Intent.Attempts);
        Assert.AreEqual(
            CaptureAnalysisJobMutationStatus.Succeeded,
            (await store.TryCompleteAsync(restarted.LeaseToken)).Status);
        Assert.AreEqual(CaptureAnalysisJobState.Completed, (await store.GetAsync(key))!.State);
    }

    [TestMethod]
    public async Task Retry_ShouldBecomeLeaseableAtExactDueTime()
    {
        using LocalCaptureAnalysisJobStore store = CreateStore(
            AnalysisPersistenceTestData.CreateTestFolder());
        CaptureAnalysisJobKey key = CreateKey();
        _ = await store.TryEnqueueAsync(key, EnqueuedAtUtc);
        CaptureAnalysisJobLease lease = (await store.TryLeaseNextDueAsync(
            EnqueuedAtUtc,
            TimeSpan.FromMinutes(2)))!;
        var failure = new AnalysisFailure(
            AnalysisFailureCode.ProviderUnavailable,
            AnalysisFailureDisposition.Transient);
        _ = await store.TryRecordAttemptAsync(
            lease.LeaseToken,
            CreateAttempt(1, CaptureAnalyzerAttemptStatus.TransientFailure, failure));
        DateTimeOffset due = EnqueuedAtUtc.AddMinutes(5);
        Assert.AreEqual(
            CaptureAnalysisJobMutationStatus.Succeeded,
            (await store.TryScheduleRetryAsync(lease.LeaseToken, failure, due)).Status);

        Assert.AreEqual(due, await store.GetNextDueTimeAsync());
        Assert.IsNull(await store.TryLeaseNextDueAsync(
            due.AddTicks(-1),
            TimeSpan.FromMinutes(1)));
        Assert.IsNotNull(await store.TryLeaseNextDueAsync(due, TimeSpan.FromMinutes(1)));
    }

    [TestMethod]
    public async Task Requeue_ShouldRestartCompletedIntentWithCleanAttemptHistory()
    {
        using LocalCaptureAnalysisJobStore store = CreateStore(
            AnalysisPersistenceTestData.CreateTestFolder());
        CaptureAnalysisJobKey key = CreateKey();
        _ = await store.TryEnqueueAsync(key, EnqueuedAtUtc);
        CaptureAnalysisJobLease lease = (await store.TryLeaseNextDueAsync(
            EnqueuedAtUtc,
            TimeSpan.FromMinutes(1)))!;
        _ = await store.TryRecordAttemptAsync(
            lease.LeaseToken,
            CreateAttempt(1, CaptureAnalyzerAttemptStatus.Succeeded, failure: null));
        _ = await store.TryCompleteAsync(lease.LeaseToken);

        CaptureAnalysisJobEnqueueResult requeued = await store.TryRequeueAsync(
            key,
            EnqueuedAtUtc.AddMinutes(2));

        Assert.AreEqual(CaptureAnalysisJobEnqueueStatus.Enqueued, requeued.Status);
        Assert.AreEqual(CaptureAnalysisJobState.Pending, requeued.Intent!.State);
        Assert.AreEqual(0, requeued.Intent.AttemptCount);
        Assert.IsEmpty(requeued.Intent.Attempts);
        Assert.AreEqual(EnqueuedAtUtc.AddMinutes(2), requeued.Intent.EnqueuedAtUtc);
    }

    [TestMethod]
    public async Task Requeue_ShouldNotRestartAnActiveIntent()
    {
        using LocalCaptureAnalysisJobStore store = CreateStore(
            AnalysisPersistenceTestData.CreateTestFolder());
        CaptureAnalysisJobKey key = CreateKey();
        _ = await store.TryEnqueueAsync(key, EnqueuedAtUtc);

        CaptureAnalysisJobEnqueueResult result = await store.TryRequeueAsync(
            key,
            EnqueuedAtUtc.AddMinutes(1));

        Assert.AreEqual(CaptureAnalysisJobEnqueueStatus.AlreadyExists, result.Status);
        Assert.AreEqual(CaptureAnalysisJobState.Pending, result.Intent!.State);
    }

    [TestMethod]
    public async Task UnknownVersion_ShouldRemainRetainedAndNeverBeOverwritten()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        var protector = new TestDataProtectionService();
        using LocalCaptureAnalysisJobStore store = CreateStore(root, protector);
        CaptureAnalysisJobKey key = CreateKey();
        string path = store.GetJobFilePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] original = protector.Protect(Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":99,\"future\":true}"));
        File.WriteAllBytes(path, original);

        Assert.IsNull(await store.GetAsync(key));
        Assert.AreEqual(
            CaptureAnalysisJobEnqueueStatus.Rejected,
            (await store.TryEnqueueAsync(key, EnqueuedAtUtc)).Status);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
    }

    [TestMethod]
    public async Task CorruptIntent_ShouldBeQuarantinedWithoutBlockingHealthyJobs()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        using LocalCaptureAnalysisJobStore store = CreateStore(root);
        CaptureAnalysisJobKey healthy = CreateKey();
        CaptureAnalysisJobKey corrupt = CreateKey(captureId: CaptureTool.Domain.CaptureId.New());
        _ = await store.TryEnqueueAsync(healthy, EnqueuedAtUtc);
        string corruptPath = store.GetJobFilePath(corrupt);
        Directory.CreateDirectory(Path.GetDirectoryName(corruptPath)!);
        File.WriteAllBytes(corruptPath, [0x01, 0x02, 0x03]);

        List<CaptureAnalysisJobIntent> jobs = [];
        await foreach (CaptureAnalysisJobIntent intent in store.ReadAllAsync())
        {
            jobs.Add(intent);
        }

        Assert.HasCount(1, jobs);
        Assert.AreEqual(healthy, jobs[0].Key);
        Assert.IsFalse(File.Exists(corruptPath));
        string quarantine = Path.Combine(
            root,
            LocalCaptureAnalysisStore.AnalysisDirectoryName,
            LocalCaptureAnalysisJobStore.JobsVersionDirectoryName,
            LocalCaptureAnalysisJobStore.QuarantineDirectoryName);
        Assert.HasCount(1, Directory.GetFiles(quarantine, "*.corrupt"));
    }

    [TestMethod]
    public async Task WakeChannel_WhenFull_ShouldStayNonBlockingAndCoalesceWakeups()
    {
        var wake = new CaptureAnalysisWakeChannel();
        var stopwatch = Stopwatch.StartNew();

        Assert.IsTrue(wake.TrySignal());
        _ = wake.TrySignal();
        stopwatch.Stop();
        await wake.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.IsLessThan(TimeSpan.FromMilliseconds(100), stopwatch.Elapsed);
        stopwatch.Restart();
        await wake.WaitAsync(TimeSpan.Zero);
        stopwatch.Stop();
        Assert.IsLessThan(TimeSpan.FromMilliseconds(100), stopwatch.Elapsed);
    }

    [TestMethod]
    public async Task WaitingIntent_ShouldResumeRenewLeaseAndPersistTerminalFailure()
    {
        using LocalCaptureAnalysisJobStore store = CreateStore(
            AnalysisPersistenceTestData.CreateTestFolder());
        CaptureAnalysisJobKey key = CreateKey();
        _ = await store.TryEnqueueAsync(key, EnqueuedAtUtc);
        CaptureAnalysisJobLease firstLease = (await store.TryLeaseNextDueAsync(
            EnqueuedAtUtc,
            TimeSpan.FromMinutes(2)))!;

        CaptureAnalysisJobMutationResult renewed = await store.TryRenewLeaseAsync(
            firstLease.LeaseToken,
            EnqueuedAtUtc.AddSeconds(30),
            TimeSpan.FromMinutes(2));
        var waitingReason = new AnalysisFailure(
            AnalysisFailureCode.ModelNotReady,
            AnalysisFailureDisposition.Transient);
        CaptureAnalysisJobMutationResult waiting = await store.TryWaitForCapabilityAsync(
            firstLease.LeaseToken,
            waitingReason);
        DateTimeOffset due = EnqueuedAtUtc.AddMinutes(5);
        int resumed = await store.ResumeWaitingForCapabilityAsync(
            key.Capability,
            key.AuthorizedProcessingBoundary,
            due);
        CaptureAnalysisJobLease resumedLease = (await store.TryLeaseNextDueAsync(
            due,
            TimeSpan.FromMinutes(2)))!;
        var terminalFailure = new AnalysisFailure(
            AnalysisFailureCode.UnsupportedMedia,
            AnalysisFailureDisposition.Terminal);
        CaptureAnalysisJobMutationResult recorded = await store.TryRecordAttemptAsync(
            resumedLease.LeaseToken,
            CreateAttempt(1, CaptureAnalyzerAttemptStatus.TerminalFailure, terminalFailure));
        CaptureAnalysisJobMutationResult failed = await store.TryFailTerminalAsync(
            resumedLease.LeaseToken,
            terminalFailure);

        Assert.AreEqual(CaptureAnalysisJobMutationStatus.Succeeded, renewed.Status);
        Assert.AreEqual(CaptureAnalysisJobMutationStatus.Succeeded, waiting.Status);
        Assert.AreEqual(CaptureAnalysisJobState.WaitingForCapability, waiting.Intent!.State);
        Assert.AreEqual(1, resumed);
        Assert.AreEqual(CaptureAnalysisJobMutationStatus.Succeeded, recorded.Status);
        Assert.AreEqual(CaptureAnalysisJobMutationStatus.Succeeded, failed.Status);
        Assert.AreEqual(CaptureAnalysisJobState.TerminalFailure, failed.Intent!.State);
    }

    [TestMethod]
    public async Task WaitingIntentWithAttemptHistory_ShouldResumeWithoutRewritingHistory()
    {
        using LocalCaptureAnalysisJobStore store = CreateStore(
            AnalysisPersistenceTestData.CreateTestFolder());
        CaptureAnalysisJobKey key = CreateKey();
        _ = await store.TryEnqueueAsync(key, EnqueuedAtUtc);
        CaptureAnalysisJobLease lease = (await store.TryLeaseNextDueAsync(
            EnqueuedAtUtc,
            TimeSpan.FromMinutes(2)))!;
        var attemptFailure = new AnalysisFailure(
            AnalysisFailureCode.ProviderUnavailable,
            AnalysisFailureDisposition.Transient);
        CaptureAnalyzerAttempt attempt = CreateAttempt(
            1,
            CaptureAnalyzerAttemptStatus.TransientFailure,
            attemptFailure);
        _ = await store.TryRecordAttemptAsync(lease.LeaseToken, attempt);
        _ = await store.TryWaitForCapabilityAsync(
            lease.LeaseToken,
            new AnalysisFailure(
                AnalysisFailureCode.ModelNotReady,
                AnalysisFailureDisposition.Transient));
        DateTimeOffset due = EnqueuedAtUtc.AddMinutes(5);

        int resumed = await store.ResumeWaitingForCapabilityAsync(
            key.Capability,
            key.AuthorizedProcessingBoundary,
            due);

        Assert.AreEqual(1, resumed);
        CaptureAnalysisJobIntent restored = (await store.GetAsync(key))!;
        Assert.AreEqual(CaptureAnalysisJobState.RetryScheduled, restored.State);
        Assert.AreEqual(due, restored.NextAttemptAtUtc);
        Assert.AreEqual(
            new AnalysisFailure(
                AnalysisFailureCode.CapabilityUnavailable,
                AnalysisFailureDisposition.Transient),
            restored.LatestFailure);
        Assert.AreEqual(1, restored.AttemptCount);
        CollectionAssert.AreEqual(new[] { attempt }, restored.Attempts.ToArray());
        Assert.IsNull(await store.TryLeaseNextDueAsync(
            due.AddTicks(-1),
            TimeSpan.FromMinutes(1)));
        Assert.IsNotNull(await store.TryLeaseNextDueAsync(due, TimeSpan.FromMinutes(1)));
    }

    [TestMethod]
    public async Task DependencyCompletion_ShouldResumeOnlyMatchingCaptureConsumers()
    {
        using LocalCaptureAnalysisJobStore store = CreateStore(
            AnalysisPersistenceTestData.CreateTestFolder());
        CaptureTool.Domain.CaptureId captureId = CaptureTool.Domain.CaptureId.New();
        CaptureAnalysisJobKey dependent = CreateKey(
            captureId,
            capability: AnalysisCapabilities.ImageDescriptionV1,
            dependencies: [AnalysisCapabilities.OcrDocumentV1]);
        CaptureAnalysisJobKey unrelated = CreateKey(
            capability: AnalysisCapabilities.ImageDescriptionV1,
            dependencies: [AnalysisCapabilities.OcrDocumentV1]);
        _ = await store.TryEnqueueAsync(dependent, EnqueuedAtUtc);
        _ = await store.TryEnqueueAsync(unrelated, EnqueuedAtUtc.AddTicks(1));
        CaptureAnalysisJobLease first = (await store.TryLeaseNextDueAsync(
            EnqueuedAtUtc,
            TimeSpan.FromMinutes(1)))!;
        var attemptFailure = new AnalysisFailure(
            AnalysisFailureCode.Timeout,
            AnalysisFailureDisposition.Transient);
        CaptureAnalyzerAttempt attempt = CreateAttempt(
            1,
            CaptureAnalyzerAttemptStatus.TransientFailure,
            attemptFailure);
        _ = await store.TryRecordAttemptAsync(first.LeaseToken, attempt);
        _ = await store.TryWaitForCapabilityAsync(first.LeaseToken, reason: null);
        CaptureAnalysisJobLease second = (await store.TryLeaseNextDueAsync(
            EnqueuedAtUtc.AddTicks(1),
            TimeSpan.FromMinutes(1)))!;
        _ = await store.TryWaitForCapabilityAsync(second.LeaseToken, reason: null);

        DateTimeOffset due = EnqueuedAtUtc.AddMinutes(1);
        int resumed = await store.ResumeWaitingForDependencyAsync(
            captureId,
            AnalysisCapabilities.OcrDocumentV1,
            due);

        Assert.AreEqual(1, resumed);
        CaptureAnalysisJobIntent restored = (await store.GetAsync(dependent))!;
        Assert.AreEqual(CaptureAnalysisJobState.RetryScheduled, restored.State);
        Assert.AreEqual(1, restored.AttemptCount);
        CollectionAssert.AreEqual(new[] { attempt }, restored.Attempts.ToArray());
        CollectionAssert.AreEqual(
            new[] { AnalysisCapabilities.OcrDocumentV1 },
            restored.Key.Dependencies.ToArray());
        Assert.AreEqual(
            CaptureAnalysisJobState.WaitingForCapability,
            (await store.GetAsync(unrelated))!.State);
    }

    [TestMethod]
    public void DurableIdentity_ShouldIncludeNormalizedCapabilityDependencies()
    {
        using LocalCaptureAnalysisJobStore store = CreateStore(
            AnalysisPersistenceTestData.CreateTestFolder());
        CaptureTool.Domain.CaptureId captureId = CaptureTool.Domain.CaptureId.New();
        CaptureAnalysisJobKey ocrDependent = CreateKey(
            captureId,
            capability: AnalysisCapabilities.ImageDescriptionV1,
            dependencies:
            [
                AnalysisCapabilities.OcrDocumentV1,
                AnalysisCapabilities.MediaPropertiesV1,
            ]);
        CaptureAnalysisJobKey mediaDependent = CreateKey(
            captureId,
            capability: AnalysisCapabilities.ImageDescriptionV1,
            dependencies: [AnalysisCapabilities.MediaPropertiesV1]);

        Assert.AreNotEqual(
            store.GetJobFilePath(ocrDependent),
            store.GetJobFilePath(mediaDependent));
        Assert.AreEqual(
            store.GetJobFilePath(ocrDependent),
            store.GetJobFilePath(new CaptureAnalysisJobKey(
                ocrDependent.Preconditions,
                ocrDependent.Capability,
                ocrDependent.AuthorizedProcessingBoundary,
                ocrDependent.Dependencies.Reverse())));
    }

    [TestMethod]
    public async Task LeaseMutation_ShouldRejectLostLeaseAndInvalidTransitions()
    {
        using LocalCaptureAnalysisJobStore store = CreateStore(
            AnalysisPersistenceTestData.CreateTestFolder());
        CaptureAnalysisJobKey key = CreateKey();
        _ = await store.TryEnqueueAsync(key, EnqueuedAtUtc);
        CaptureAnalysisJobLease lease = (await store.TryLeaseNextDueAsync(
            EnqueuedAtUtc,
            TimeSpan.FromMinutes(2)))!;
        var transientFailure = new AnalysisFailure(
            AnalysisFailureCode.Timeout,
            AnalysisFailureDisposition.Transient);

        CaptureAnalysisJobMutationResult lost = await store.TryRenewLeaseAsync(
            CaptureAnalysisJobLeaseToken.New(),
            EnqueuedAtUtc,
            TimeSpan.FromMinutes(1));
        CaptureAnalysisJobMutationResult incomplete = await store.TryCompleteAsync(
            lease.LeaseToken);
        CaptureAnalysisJobMutationResult retryWithoutAttempt = await store.TryScheduleRetryAsync(
            lease.LeaseToken,
            transientFailure,
            EnqueuedAtUtc.AddMinutes(1));
        _ = await store.TryWaitForCapabilityAsync(lease.LeaseToken, transientFailure);
        CaptureAnalysisJobMutationResult noLongerRunning = await store.TryRenewLeaseAsync(
            lease.LeaseToken,
            EnqueuedAtUtc.AddSeconds(1),
            TimeSpan.FromMinutes(1));

        Assert.AreEqual(CaptureAnalysisJobMutationStatus.LeaseLost, lost.Status);
        Assert.AreEqual(CaptureAnalysisJobMutationStatus.InvalidTransition, incomplete.Status);
        Assert.AreEqual(CaptureAnalysisJobMutationStatus.InvalidTransition, retryWithoutAttempt.Status);
        Assert.AreEqual(CaptureAnalysisJobMutationStatus.LeaseLost, noLongerRunning.Status);
    }

    [TestMethod]
    public async Task Cancellation_ShouldSupportSingleCaptureAndControlGenerationFences()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        using LocalCaptureAnalysisJobStore store = CreateStore(root);
        CaptureTool.Domain.CaptureId captureId = CaptureTool.Domain.CaptureId.New();
        CaptureAnalysisJobKey first = CreateKey(captureId, resolutionPolicyRevision: 1);
        CaptureAnalysisJobKey second = CreateKey(captureId, resolutionPolicyRevision: 2);
        _ = await store.TryEnqueueAsync(first, EnqueuedAtUtc);
        _ = await store.TryEnqueueAsync(second, EnqueuedAtUtc.AddSeconds(1));

        int captureCancelled = await store.CancelCaptureAsync(
            captureId,
            minimumTombstoneGeneration: 1);
        CaptureAnalysisJobMutationResult alreadyCancelled = await store.TryCancelAsync(first);
        CaptureAnalysisJobMutationResult missing = await store.TryCancelAsync(CreateKey());

        Assert.AreEqual(2, captureCancelled);
        Assert.AreEqual(CaptureAnalysisJobMutationStatus.Succeeded, alreadyCancelled.Status);
        Assert.AreEqual(CaptureAnalysisJobState.Cancelled, alreadyCancelled.Intent!.State);
        Assert.AreEqual(CaptureAnalysisJobMutationStatus.NotFound, missing.Status);

        using LocalCaptureAnalysisJobStore controlStore = CreateStore(
            AnalysisPersistenceTestData.CreateTestFolder());
        CaptureAnalysisJobKey oldControl = CreateKey(controlGeneration: 2);
        CaptureAnalysisJobKey currentControl = CreateKey(controlGeneration: 4);
        _ = await controlStore.TryEnqueueAsync(oldControl, EnqueuedAtUtc);
        _ = await controlStore.TryEnqueueAsync(currentControl, EnqueuedAtUtc.AddSeconds(1));

        int controlCancelled = await controlStore.CancelBeforeControlGenerationAsync(4);

        Assert.AreEqual(1, controlCancelled);
        Assert.AreEqual(CaptureAnalysisJobState.Cancelled, (await controlStore.GetAsync(oldControl))!.State);
        Assert.AreEqual(CaptureAnalysisJobState.Pending, (await controlStore.GetAsync(currentControl))!.State);
    }

    private static LocalCaptureAnalysisJobStore CreateStore(
        string root,
        TestDataProtectionService? protector = null)
    {
        return new(
            new TestLocalCachePathProvider(root),
            protector ?? new TestDataProtectionService(),
            new AtomicFileWriter(),
            new TestLogService());
    }

    private static CaptureAnalysisJobKey CreateKey(
        CaptureTool.Domain.CaptureId? captureId = null,
        long controlGeneration = 3,
        long tombstoneGeneration = 0,
        long resolutionPolicyRevision = 1,
        CapabilityDefinition? capability = null,
        IEnumerable<CapabilityDefinition>? dependencies = null)
    {
        SourceRevision source = AnalysisPersistenceTestData.SourceRevision;
        var preconditions = new AnalysisCommitPreconditions(
            captureId ?? CaptureTool.Domain.CaptureId.New(),
            captureSourceGeneration: 4,
            source.ProvisionalStamp,
            source,
            new AnalysisPurpose("capture-memory-search", 1),
            policyRevision: 2,
            controlGeneration,
            enrollmentGeneration: 1,
            tombstoneGeneration,
            new AnalysisRecipeId("capture-memory-image"),
            new AnalysisRecipeVersion(1),
            resolutionPolicyRevision);
        return new(
            preconditions,
            capability ?? AnalysisCapabilities.MediaPropertiesV1,
            ProcessingBoundary.OnDevice,
            dependencies);
    }

    private static CaptureAnalyzerAttempt CreateAttempt(
        int number,
        CaptureAnalyzerAttemptStatus status,
        AnalysisFailure? failure)
    {
        return new(
            number,
            AnalysisPersistenceTestData.Analyzer,
            ProcessingBoundary.OnDevice,
            EnqueuedAtUtc,
            EnqueuedAtUtc.AddSeconds(1),
            status,
            failure);
    }
}
