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
        CaptureTool.Domain.CaptureId? captureId = null)
    {
        SourceRevision source = AnalysisPersistenceTestData.SourceRevision;
        var preconditions = new AnalysisCommitPreconditions(
            captureId ?? CaptureTool.Domain.CaptureId.New(),
            captureSourceGeneration: 4,
            source.ProvisionalStamp,
            source,
            new AnalysisPurpose("capture-memory-search", 1),
            policyRevision: 2,
            controlGeneration: 3,
            enrollmentGeneration: 1,
            tombstoneGeneration: 0,
            new AnalysisRecipeId("capture-memory-image"),
            new AnalysisRecipeVersion(1),
            resolutionPolicyRevision: 1);
        return new(preconditions, AnalysisCapabilities.MediaPropertiesV1, ProcessingBoundary.OnDevice);
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
