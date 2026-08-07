using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Tests.Analysis.Domain;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Tests.Analysis.Contracts;

[TestClass]
public sealed class CaptureAnalysisContractInvariantTests
{
    [TestMethod]
    public void AnalyzerRequests_ShouldRejectRemoteWorkBeforeAvailabilityOrContentReadUnderLocalOnlyPolicy()
    {
        CaptureAnalyzerDescriptor remote = CreateDescriptor(
            ProcessingBoundary.Remote,
            "microsoft.azure",
            "azure.vision");
        AnalysisProcessingPolicy localOnly = AnalysisProcessingPolicy.LocalOnly(AnalysisTestData.Purpose);
        var source = new TestAnalysisSource();

        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalyzerAvailabilityRequest(
            remote,
            CaptureMediaKind.Image,
            source.SourceRevision.Length,
            AnalysisTestData.Purpose,
            localOnly));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisRequest(
            remote,
            AnalysisTestData.Purpose,
            localOnly,
            source));
        Assert.AreEqual(0, source.OpenReadCallCount);
    }

    [TestMethod]
    public void AnalyzerRequests_ShouldBindAnEligibleDescriptorAndSourceLimit()
    {
        CaptureAnalyzerDescriptor local = CreateDescriptor(
            ProcessingBoundary.OnDevice,
            "microsoft.windows",
            "windows.ocr",
            maximumSourceBytes: 100);
        AnalysisProcessingPolicy policy = AnalysisProcessingPolicy.LocalOnly(AnalysisTestData.Purpose);
        var source = new TestAnalysisSource();

        var availability = new CaptureAnalyzerAvailabilityRequest(
            local,
            CaptureMediaKind.Image,
            100,
            AnalysisTestData.Purpose,
            policy);
        var request = new CaptureAnalysisRequest(local, AnalysisTestData.Purpose, policy, source);

        Assert.IsTrue(availability.IsEligibleFor(local));
        Assert.IsTrue(request.IsEligibleFor(local));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalyzerAvailabilityRequest(
            local,
            CaptureMediaKind.Image,
            101,
            AnalysisTestData.Purpose,
            policy));
    }

    [TestMethod]
    public void ResolvedAnalyzer_ShouldRequireMatchingEligibleAvailableCandidate()
    {
        CaptureAnalyzerDescriptor descriptor = CreateDescriptor(
            ProcessingBoundary.OnDevice,
            "microsoft.windows",
            "windows.ocr");
        var analyzer = new StubAnalyzer(descriptor);
        var eligible = new CaptureAnalyzerCandidateEvaluation(
            descriptor,
            CaptureAnalyzerEligibilityStatus.Eligible,
            CaptureAnalyzerAvailability.Available);

        CaptureAnalyzerResolution resolution = CaptureAnalyzerResolution.Resolved(analyzer, [eligible]);

        Assert.AreSame(analyzer, resolution.Analyzer);
        Assert.ThrowsExactly<ArgumentException>(() => CaptureAnalyzerResolution.Resolved(analyzer, []));
        Assert.ThrowsExactly<ArgumentException>(() => CaptureAnalyzerResolution.NoEligibleAnalyzer([eligible]));
        Assert.ThrowsExactly<ArgumentException>(() => CaptureAnalyzerResolution.WaitingForPreparation([]));
    }

    [TestMethod]
    public void CaptureChangeBatch_ShouldBeOrderedBoundedAndDefensivelyCopied()
    {
        var changes = new List<CaptureAssetChange>
        {
            CreateAssetChange(1),
            CreateAssetChange(2),
        };
        var batch = new CaptureAssetChangeBatch(0, 2, 3, changes);

        changes.Clear();

        Assert.HasCount(2, batch.Changes);
        Assert.IsTrue(batch.HasMore);
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAssetChangeBatch(
            0,
            2,
            2,
            [CreateAssetChange(2), CreateAssetChange(1)]));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAssetChangeBatch(
            0,
            3,
            3,
            [CreateAssetChange(1), CreateAssetChange(2)]));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAssetChangeBatch(0, 1, 1, [default]));
    }

    [TestMethod]
    public void DurableJobIntent_ShouldEnforceStateAndAttemptInvariants()
    {
        CaptureAnalysisJobKey key = CreateJobKey();
        AnalysisFailure transientFailure = new(
            AnalysisFailureCode.Timeout,
            AnalysisFailureDisposition.Transient);
        var attempts = new List<CaptureAnalyzerAttempt>
        {
            new(
                1,
                AnalysisTestData.CreateAnalyzer(),
                ProcessingBoundary.OnDevice,
                AnalysisTestData.GeneratedAtUtc,
                AnalysisTestData.GeneratedAtUtc.AddSeconds(1),
                CaptureAnalyzerAttemptStatus.TransientFailure,
                transientFailure),
        };
        var intent = new CaptureAnalysisJobIntent(
            key,
            CaptureAnalysisJobState.RetryScheduled,
            1,
            AnalysisTestData.GeneratedAtUtc,
            AnalysisTestData.GeneratedAtUtc.AddMinutes(1),
            transientFailure,
            attempts);

        attempts.Clear();

        Assert.HasCount(1, intent.Attempts);
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisJobIntent(
            key,
            CaptureAnalysisJobState.RetryScheduled,
            1,
            AnalysisTestData.GeneratedAtUtc,
            null,
            transientFailure,
            intent.Attempts));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisJobIntent(
            key,
            CaptureAnalysisJobState.TerminalFailure,
            1,
            AnalysisTestData.GeneratedAtUtc,
            null,
            transientFailure,
            intent.Attempts));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisJobIntent(
            key,
            CaptureAnalysisJobState.Pending,
            0,
            AnalysisTestData.GeneratedAtUtc,
            null,
            null,
            intent.Attempts));
    }

    [TestMethod]
    public void JobLeaseAndResults_ShouldRejectUnknownOrContradictoryState()
    {
        CaptureAnalysisJobKey key = CreateJobKey();
        var running = new CaptureAnalysisJobIntent(
            key,
            CaptureAnalysisJobState.Running,
            0,
            AnalysisTestData.GeneratedAtUtc,
            null,
            null,
            []);
        var lease = new CaptureAnalysisJobLease(
            CaptureAnalysisJobLeaseToken.New(),
            running,
            AnalysisTestData.GeneratedAtUtc.AddMinutes(1));

        Assert.AreSame(running, lease.Intent);
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisJobLease(
            default,
            running,
            AnalysisTestData.GeneratedAtUtc.AddMinutes(1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CaptureAnalysisJobEnqueueResult(
            CaptureAnalysisJobEnqueueStatus.Unknown));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisJobEnqueueResult(
            CaptureAnalysisJobEnqueueStatus.Enqueued));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisJobMutationResult(
            CaptureAnalysisJobMutationStatus.Succeeded));
    }

    [TestMethod]
    public void MemoryContracts_ShouldNormalizeAndBoundQueriesEvidenceAndResults()
    {
        var request = new CaptureMemorySearchRequest("  settings  ", 10);
        var bounds = new CaptureMemoryPixelBounds(1, 2, 10, 5, 100, 50);
        var evidence = new CaptureMemoryMatchEvidence(
            CaptureMemoryMatchKind.OcrText,
            "  Settings  ",
            bounds);
        var result = new CaptureMemorySearchResult(
            AnalysisTestData.CaptureId,
            CaptureMediaKind.Image,
            AnalysisTestData.CapturedAtUtc,
            1.25,
            1,
            evidence);

        Assert.AreEqual("settings", request.Query);
        Assert.AreEqual("Settings", result.Evidence.Snippet);
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureMemorySearchRequest(" ", 10));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CaptureMemorySearchRequest("query", 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CaptureMemoryPixelBounds(
            double.NaN,
            0,
            1,
            1,
            10,
            10));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CaptureMemoryPixelBounds(
            9,
            9,
            2,
            2,
            10,
            10));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CaptureMemorySearchResult(
            AnalysisTestData.CaptureId,
            CaptureMediaKind.Image,
            AnalysisTestData.CapturedAtUtc,
            double.NaN,
            1,
            evidence));
    }

    [TestMethod]
    public void ControlAndPolicyContracts_ShouldFailClosedAndBindAuthorizationRequest()
    {
        AnalysisProcessingPolicy policy = AnalysisProcessingPolicy.LocalOnly(AnalysisTestData.Purpose);
        AnalyzerIdentity analyzer = AnalysisTestData.CreateAnalyzer();
        var request = new CaptureAnalysisAuthorizationRequest(
            AnalysisTestData.CaptureId,
            AnalysisTestData.Purpose,
            AnalysisCapabilities.OcrDocumentV1,
            ProcessingBoundary.OnDevice,
            analyzer,
            CaptureAnalysisAuthorizationStage.CapabilityCommit);
        CaptureAnalysisAuthorizationDecision decision = CaptureAnalysisAuthorizationDecision.Authorized(
            request,
            1,
            0,
            1,
            0,
            new CaptureAnalysisAuthorizationScope(
                AnalysisTestData.Purpose,
                policy,
                [AnalysisCapabilities.OcrDocumentV1]));

        Assert.AreSame(request, decision.Request);
        var unknownControl = new CaptureAnalysisControlState(CaptureAnalysisPolicy.Unknown, []);
        Assert.IsFalse(unknownControl.Policy.IsProcessingAuthorized);
        Assert.ThrowsExactly<ArgumentNullException>(() => new CaptureAnalysisControlState(null!, []));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CaptureAnalysisControlState(
            CaptureAnalysisPolicy.Unknown,
            [],
            captureChangeCheckpoint: -1));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisControlState(
            CaptureAnalysisPolicy.Unknown,
            [
                new CaptureAnalysisEnrollment(
                    AnalysisTestData.CaptureId,
                    CaptureAnalysisEnrollmentState.Enrolled,
                    CaptureAnalysisExclusionReason.None,
                    1,
                    0,
                    1,
                    AnalysisTestData.RecipeId,
                    new AnalysisRecipeVersion(1)),
            ]));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisEnrollment(
            AnalysisTestData.CaptureId,
            CaptureAnalysisEnrollmentState.Enrolled,
            CaptureAnalysisExclusionReason.None,
            1,
            0,
            1,
            default(AnalysisRecipeId),
            new AnalysisRecipeVersion(1)));
    }

    [TestMethod]
    public void FailureAndPersistenceResults_ShouldRejectUnknownSuccessState()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CaptureAnalyzerOutput.Failed(default));
        Assert.ThrowsExactly<ArgumentException>(() => AnalysisCapabilityPreparationState.Failed(default));
        Assert.ThrowsExactly<ArgumentException>(() => CaptureAnalyzerPreparationResult.Failed(default));
        Assert.ThrowsExactly<ArgumentException>(() => CaptureAnalyzerPreparationResult.Unsupported(
            new AnalysisFailure(
                AnalysisFailureCode.ProviderUnavailable,
                AnalysisFailureDisposition.Transient)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CaptureAnalysisStoreWriteResult(
            CaptureAnalysisStoreWriteStatus.Unknown));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisStoreWriteResult(
            CaptureAnalysisStoreWriteStatus.Succeeded));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CaptureAnalysisControlWriteResult(
            CaptureAnalysisControlWriteStatus.Unknown));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisControlWriteResult(
            CaptureAnalysisControlWriteStatus.Succeeded));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisControlWriteResult(
            CaptureAnalysisControlWriteStatus.Conflict));
    }

    private static CaptureAnalyzerDescriptor CreateDescriptor(
        ProcessingBoundary boundary,
        string providerId,
        string analyzerId,
        long? maximumSourceBytes = 1024)
    {
        CaptureAnalyzerDataKind dataSent = boundary == ProcessingBoundary.Remote
            ? CaptureAnalyzerDataKind.SourceMedia
            : CaptureAnalyzerDataKind.None;
        CaptureAnalyzerRequirement requirements = boundary == ProcessingBoundary.Remote
            ? CaptureAnalyzerRequirement.NetworkConnectivity
            : CaptureAnalyzerRequirement.None;
        return new(
            AnalysisCapabilities.OcrDocumentV1,
            AnalysisTestData.CreateAnalyzer(analyzerId, providerId),
            [CaptureMediaKind.Image],
            boundary,
            dataSent,
            requirements,
            CaptureAnalyzerWorkloadClass.Lightweight,
            maximumSourceBytes,
            1);
    }

    private static CaptureAssetChange CreateAssetChange(long sequence)
    {
        return new(
            sequence,
            AnalysisTestData.CaptureId,
            sequence,
            CaptureAssetChangeType.Finalized,
            AnalysisTestData.CapturedAtUtc.AddSeconds(sequence));
    }

    private static CaptureAnalysisJobKey CreateJobKey()
    {
        return new(
            AnalysisTestData.CreatePreconditions(),
            AnalysisCapabilities.OcrDocumentV1,
            ProcessingBoundary.OnDevice);
    }

    private sealed class TestAnalysisSource : ICaptureAnalysisSource
    {
        public CaptureId CaptureId => AnalysisTestData.CaptureId;

        public CaptureMediaKind MediaKind => CaptureMediaKind.Image;

        public SourceRevision SourceRevision => AnalysisTestData.CreateSource();

        public int OpenReadCallCount { get; private set; }

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            OpenReadCallCount++;
            return ValueTask.FromResult<Stream>(new MemoryStream());
        }
    }

    private sealed class StubAnalyzer(CaptureAnalyzerDescriptor descriptor) : ICaptureAnalyzer
    {
        public CaptureAnalyzerDescriptor Descriptor { get; } = descriptor;

        public ValueTask<CaptureAnalyzerAvailability> GetAvailabilityAsync(
            CaptureAnalyzerAvailabilityRequest request,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(CaptureAnalyzerAvailability.Available);
        }

        public Task<CaptureAnalyzerOutput> AnalyzeAsync(
            CaptureAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CaptureAnalyzerOutput.Cancelled);
        }
    }
}
