using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Tests.Analysis.Domain;

[TestClass]
public sealed class CaptureAnalysisPolicyTests
{
    [TestMethod]
    public void Unknown_ShouldFailClosed()
    {
        CaptureAnalysisPolicy policy = CaptureAnalysisPolicy.Unknown;

        Assert.AreEqual(CaptureAnalysisConsentState.Unknown, policy.ConsentState);
        Assert.IsFalse(policy.IsProcessingAuthorized);
        Assert.IsFalse(policy.IsFutureCaptureAdmissionEnabled);
        Assert.IsFalse(policy.IsFutureCaptureEligible(1));
        Assert.IsFalse(policy.IsExistingCaptureBackfillEligible(1));
        Assert.ThrowsExactly<InvalidOperationException>(() => policy.ResumeFutureCaptures(1));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            policy.AuthorizeExistingCaptureBackfill(1));
        Assert.ThrowsExactly<InvalidOperationException>(() => policy.ClearMemory(1));
    }

    [TestMethod]
    public void GrantFutureCaptures_ShouldPersistReviewedScopeAndUseCurrentWatermark()
    {
        AnalysisProcessingPolicy processingPolicy = CreateLocalPolicy(AnalysisTestData.Purpose);
        CaptureAnalysisAuthorizationScope scope = CreateScope(
            AnalysisTestData.Purpose,
            processingPolicy);

        CaptureAnalysisPolicy granted = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            scope,
            currentSequence: 10);

        Assert.AreEqual(CaptureAnalysisConsentState.Granted, granted.ConsentState);
        Assert.AreEqual(1, granted.PolicyRevision);
        Assert.AreEqual(0, granted.ControlGeneration);
        Assert.AreSame(scope, granted.AuthorizationScope);
        Assert.AreEqual(AnalysisTestData.Purpose, granted.AuthorizedPurpose);
        Assert.AreSame(processingPolicy, granted.ProcessingPolicy);
        Assert.IsTrue(granted.IsProcessingAuthorized);
        Assert.IsTrue(granted.IsFutureCaptureAdmissionEnabled);
        Assert.AreEqual(10, granted.FutureCaptureSequenceWatermark);
        Assert.AreEqual(CaptureAnalysisBackfillState.NotAuthorized, granted.BackfillState);
        Assert.IsFalse(granted.IsFutureCaptureEligible(10));
        Assert.IsTrue(granted.IsFutureCaptureEligible(11));
        Assert.IsFalse(granted.IsExistingCaptureBackfillEligible(1));
    }

    [TestMethod]
    public void ExplicitGrantRenewal_ShouldAdvancePolicyFenceAndReplaceExactScope()
    {
        CaptureAnalysisPolicy granted = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            CreateScope(AnalysisTestData.Purpose),
            currentSequence: 10);
        AnalysisPurpose changedPurpose = new(AnalysisTestData.Purpose.Id, 2);
        CaptureAnalysisAuthorizationScope changedScope = CreateScope(changedPurpose);

        CaptureAnalysisPolicy renewed = granted.GrantFutureCaptures(
            changedScope,
            currentSequence: 20);

        Assert.AreEqual(granted.PolicyRevision + 1, renewed.PolicyRevision);
        Assert.AreSame(changedScope, renewed.AuthorizationScope);
        Assert.AreEqual(20, renewed.FutureCaptureSequenceWatermark);
        Assert.AreEqual(CaptureAnalysisBackfillState.NotAuthorized, renewed.BackfillState);
    }

    [TestMethod]
    public void StopAndResumeFutureCaptures_ShouldPreserveScopeBackfillAndExistingWorkFence()
    {
        CaptureAnalysisPolicy granted = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            CreateScope(AnalysisTestData.Purpose),
            currentSequence: 10);
        CaptureAnalysisPolicy withBackfill = granted.AuthorizeExistingCaptureBackfill(15);

        CaptureAnalysisPolicy stopped = withBackfill.StopFutureCaptures(20);
        CaptureAnalysisPolicy resumed = stopped.ResumeFutureCaptures(30);

        Assert.AreEqual(granted.PolicyRevision, stopped.PolicyRevision);
        Assert.AreEqual(granted.ControlGeneration, stopped.ControlGeneration);
        Assert.IsFalse(stopped.IsFutureCaptureAdmissionEnabled);
        Assert.AreEqual(20, stopped.FutureCaptureSequenceWatermark);
        Assert.AreEqual(CaptureAnalysisBackfillState.Authorized, stopped.BackfillState);
        Assert.IsTrue(stopped.IsExistingCaptureBackfillEligible(15));
        Assert.AreEqual(granted.PolicyRevision, resumed.PolicyRevision);
        Assert.AreEqual(granted.ControlGeneration, resumed.ControlGeneration);
        Assert.AreSame(stopped.AuthorizationScope, resumed.AuthorizationScope);
        Assert.AreEqual(30, resumed.FutureCaptureSequenceWatermark);
        Assert.IsFalse(resumed.IsFutureCaptureEligible(30));
        Assert.IsTrue(resumed.IsFutureCaptureEligible(31));
        Assert.AreEqual(CaptureAnalysisBackfillState.Authorized, resumed.BackfillState);
        Assert.IsTrue(resumed.IsExistingCaptureBackfillEligible(15));
    }

    [TestMethod]
    public void ResumeWhileAlreadyEnabled_ShouldBeIdempotent()
    {
        CaptureAnalysisPolicy granted = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            CreateScope(AnalysisTestData.Purpose),
            currentSequence: 10);

        CaptureAnalysisPolicy replayed = granted.ResumeFutureCaptures(20);

        Assert.AreSame(granted, replayed);
        Assert.AreEqual(10, replayed.FutureCaptureSequenceWatermark);
    }

    [TestMethod]
    public void AuthorizeExistingCaptureBackfill_ShouldCreateSeparateBoundedScope()
    {
        CaptureAnalysisPolicy granted = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            CreateScope(AnalysisTestData.Purpose),
            currentSequence: 10);

        CaptureAnalysisPolicy authorized = granted.AuthorizeExistingCaptureBackfill(20);

        Assert.AreEqual(granted.PolicyRevision, authorized.PolicyRevision);
        Assert.AreEqual(granted.ControlGeneration, authorized.ControlGeneration);
        Assert.AreEqual(CaptureAnalysisBackfillState.Authorized, authorized.BackfillState);
        Assert.AreEqual(20, authorized.BackfillUpperSequence);
        Assert.AreEqual(0, authorized.BackfillCheckpoint);
        Assert.IsTrue(authorized.IsExistingCaptureBackfillEligible(1));
        Assert.IsTrue(authorized.IsExistingCaptureBackfillEligible(20));
        Assert.IsFalse(authorized.IsExistingCaptureBackfillEligible(21));
    }

    [TestMethod]
    public void RehydratedBackfillCheckpoint_ShouldExcludeProcessedSequences()
    {
        var policy = new CaptureAnalysisPolicy(
            CaptureAnalysisConsentState.Granted,
            policyRevision: 4,
            controlGeneration: 2,
            CreateScope(AnalysisTestData.Purpose),
            isFutureCaptureAdmissionEnabled: true,
            futureCaptureSequenceWatermark: 10,
            CaptureAnalysisBackfillState.InProgress,
            backfillUpperSequence: 20,
            backfillCheckpoint: 7);

        Assert.IsFalse(policy.IsExistingCaptureBackfillEligible(7));
        Assert.IsTrue(policy.IsExistingCaptureBackfillEligible(8));
        Assert.IsTrue(policy.IsExistingCaptureBackfillEligible(20));
    }

    [TestMethod]
    public void Revoke_ShouldAdvanceBothFencesAndRemoveAuthorization()
    {
        CaptureAnalysisPolicy granted = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            CreateScope(AnalysisTestData.Purpose),
            currentSequence: 10);
        CaptureAnalysisPolicy withBackfill = granted.AuthorizeExistingCaptureBackfill(15);

        CaptureAnalysisPolicy revoked = withBackfill.Revoke();

        Assert.AreEqual(CaptureAnalysisConsentState.Denied, revoked.ConsentState);
        Assert.AreEqual(withBackfill.PolicyRevision + 1, revoked.PolicyRevision);
        Assert.AreEqual(withBackfill.ControlGeneration + 1, revoked.ControlGeneration);
        Assert.IsNull(revoked.AuthorizationScope);
        Assert.IsFalse(revoked.IsProcessingAuthorized);
        Assert.IsFalse(revoked.IsFutureCaptureAdmissionEnabled);
        Assert.AreEqual(10, revoked.FutureCaptureSequenceWatermark);
        Assert.AreEqual(CaptureAnalysisBackfillState.NotAuthorized, revoked.BackfillState);
        Assert.IsFalse(revoked.IsFutureCaptureEligible(21));
        Assert.IsFalse(revoked.IsExistingCaptureBackfillEligible(1));
        Assert.IsTrue(withBackfill.IsProcessingAuthorized);
    }

    [TestMethod]
    public void ClearMemory_ShouldAdvanceOnlyControlFenceAndRetainFutureConsent()
    {
        CaptureAnalysisPolicy granted = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            CreateScope(AnalysisTestData.Purpose),
            currentSequence: 10);
        CaptureAnalysisPolicy withBackfill = granted.AuthorizeExistingCaptureBackfill(15);

        CaptureAnalysisPolicy cleared = withBackfill.ClearMemory(20);

        Assert.AreEqual(CaptureAnalysisConsentState.Granted, cleared.ConsentState);
        Assert.AreEqual(withBackfill.PolicyRevision, cleared.PolicyRevision);
        Assert.AreEqual(withBackfill.ControlGeneration + 1, cleared.ControlGeneration);
        Assert.AreSame(withBackfill.AuthorizationScope, cleared.AuthorizationScope);
        Assert.IsTrue(cleared.IsFutureCaptureAdmissionEnabled);
        Assert.AreEqual(20, cleared.FutureCaptureSequenceWatermark);
        Assert.AreEqual(CaptureAnalysisBackfillState.NotAuthorized, cleared.BackfillState);
        Assert.IsFalse(cleared.IsFutureCaptureEligible(20));
        Assert.IsTrue(cleared.IsFutureCaptureEligible(21));
        Assert.IsFalse(cleared.IsExistingCaptureBackfillEligible(15));
    }

    [TestMethod]
    public void Constructor_ShouldRejectInconsistentPersistedState()
    {
        CaptureAnalysisAuthorizationScope scope = CreateScope(AnalysisTestData.Purpose);

        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisPolicy(
            CaptureAnalysisConsentState.Granted,
            1,
            0,
            null,
            false,
            0,
            CaptureAnalysisBackfillState.NotAuthorized,
            0,
            0));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisPolicy(
            CaptureAnalysisConsentState.Denied,
            1,
            1,
            scope,
            false,
            0,
            CaptureAnalysisBackfillState.NotAuthorized,
            0,
            0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CaptureAnalysisPolicy(
            CaptureAnalysisConsentState.Granted,
            0,
            0,
            scope,
            true,
            0,
            CaptureAnalysisBackfillState.NotAuthorized,
            0,
            0));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisPolicy(
            CaptureAnalysisConsentState.Granted,
            1,
            0,
            scope,
            true,
            0,
            CaptureAnalysisBackfillState.NotAuthorized,
            10,
            0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CaptureAnalysisPolicy(
            CaptureAnalysisConsentState.Granted,
            1,
            0,
            scope,
            true,
            0,
            CaptureAnalysisBackfillState.Authorized,
            10,
            11));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisPolicy(
            CaptureAnalysisConsentState.Granted,
            1,
            0,
            scope,
            true,
            0,
            CaptureAnalysisBackfillState.Authorized,
            10,
            1));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisPolicy(
            CaptureAnalysisConsentState.Granted,
            1,
            0,
            scope,
            true,
            0,
            CaptureAnalysisBackfillState.Completed,
            10,
            9));
    }

    [TestMethod]
    public void AuthorizationScope_ShouldNormalizeAndDefensivelyBindCapabilities()
    {
        var mutableCapabilities = new List<CapabilityDefinition>
        {
            AnalysisCapabilities.OcrDocumentV1,
            AnalysisCapabilities.MediaPropertiesV1,
        };
        var scope = new CaptureAnalysisAuthorizationScope(
            AnalysisTestData.Purpose,
            CreateLocalPolicy(AnalysisTestData.Purpose),
            mutableCapabilities);
        var reordered = new CaptureAnalysisAuthorizationScope(
            AnalysisTestData.Purpose,
            CreateLocalPolicy(AnalysisTestData.Purpose),
            [AnalysisCapabilities.MediaPropertiesV1, AnalysisCapabilities.OcrDocumentV1]);
        mutableCapabilities.Clear();

        Assert.HasCount(2, scope.Capabilities);
        Assert.IsTrue(scope.IsEquivalentTo(reordered));
        Assert.IsTrue(scope.Allows(AnalysisCapabilities.OcrDocumentV1));
        Assert.IsFalse(scope.Allows(AnalysisCapabilities.ImageDescriptionV1));
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<CapabilityDefinition>)scope.Capabilities).Add(
                AnalysisCapabilities.ImageDescriptionV1));
        CapabilityDefinition changedSchema = new(
            AnalysisCapabilities.OcrDocumentV1.Id,
            new CapabilitySchemaVersion(2),
            AnalysisCapabilities.OcrDocumentV1.Classification);
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisAuthorizationScope(
            AnalysisTestData.Purpose,
            CreateLocalPolicy(AnalysisTestData.Purpose),
            [AnalysisCapabilities.OcrDocumentV1, changedSchema]));
    }

    [TestMethod]
    public void Transitions_ShouldRejectARegressedCurrentSequence()
    {
        CaptureAnalysisPolicy granted = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            CreateScope(AnalysisTestData.Purpose),
            currentSequence: 10);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => granted.StopFutureCaptures(9));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => granted.ResumeFutureCaptures(9));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            granted.AuthorizeExistingCaptureBackfill(9));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => granted.ClearMemory(9));
    }

    private static CaptureAnalysisAuthorizationScope CreateScope(
        AnalysisPurpose purpose,
        AnalysisProcessingPolicy? processingPolicy = null)
    {
        return new(
            purpose,
            processingPolicy ?? CreateLocalPolicy(purpose),
            [
                AnalysisCapabilities.MediaPropertiesV1,
                AnalysisCapabilities.OcrDocumentV1,
                AnalysisCapabilities.ImageDescriptionV1,
            ]);
    }

    private static AnalysisProcessingPolicy CreateLocalPolicy(AnalysisPurpose purpose)
    {
        return AnalysisProcessingPolicy.LocalOnly(purpose);
    }
}
