using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Analysis.Policy;
using CaptureTool.Application.Tests.Analysis.Domain;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;
using Moq;

namespace CaptureTool.Application.Tests.Analysis.Policy;

[TestClass]
public sealed class CaptureAnalysisPolicyServiceTests
{
    [TestMethod]
    public void BackgroundAnalysisConsent_ShouldNotDependOnInteractiveAiConsent()
    {
        Type[] dependencies = typeof(CaptureAnalysisPolicyService)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        CollectionAssert.DoesNotContain(dependencies, typeof(IAiFeatureConsentService));
        Assert.AreNotEqual(
            CaptureToolSettings.Settings_CaptureAnalysisConsent.Key,
            CaptureToolSettings.Settings_AiConsent_TextExtraction.Key);
        Assert.AreNotEqual(
            CaptureToolSettings.Settings_CaptureAnalysisConsent.Key,
            CaptureToolSettings.Settings_AiConsent_ImageDescription.Key);
    }

    [TestMethod]
    public async Task FeatureDisabled_ShouldDenyWhileExposingOnlyContentFreeControlState()
    {
        var store = new TestControlStore(CreateSnapshot(CaptureAnalysisPolicy.Unknown));
        var settings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Unknown);
        var features = new TestFeatureAvailability(false);
        CaptureAnalysisPolicyService service = CreateService(store, settings, features, 10);
        var request = CreateAuthorizationRequest();

        CaptureAnalysisAuthorizationDecision decision = await service.AuthorizeAsync(request);

        Assert.IsFalse(decision.IsAuthorized);
        Assert.AreEqual(CaptureAnalysisPolicyDenialReason.FeatureDisabled, decision.DenialReason);
        Assert.AreEqual(1, store.ReadCalls);
        Assert.AreEqual(0, features.ProviderChecks);
        Assert.AreEqual(0, features.AnalyzerChecks);
    }

    [TestMethod]
    public async Task UnknownAndDeniedConsent_ShouldFailClosed()
    {
        var unknownStore = new TestControlStore(CreateSnapshot(CaptureAnalysisPolicy.Unknown));
        var unknownSettings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Unknown);
        CaptureAnalysisPolicyService unknownService = CreateService(
            unknownStore,
            unknownSettings,
            new TestFeatureAvailability(true),
            1);

        CaptureAnalysisAuthorizationDecision unknown = await unknownService.AuthorizeAsync(
            CreateAuthorizationRequest());

        Assert.AreEqual(CaptureAnalysisPolicyDenialReason.ConsentUnknown, unknown.DenialReason);

        CaptureAnalysisPolicy deniedPolicy = CaptureAnalysisPolicy.Unknown.Revoke();
        var deniedStore = new TestControlStore(CreateSnapshot(deniedPolicy));
        var deniedSettings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Denied);
        CaptureAnalysisPolicyService deniedService = CreateService(
            deniedStore,
            deniedSettings,
            new TestFeatureAvailability(true),
            1);

        CaptureAnalysisAuthorizationDecision denied = await deniedService.AuthorizeAsync(
            CreateAuthorizationRequest());

        Assert.AreEqual(CaptureAnalysisPolicyDenialReason.ConsentDenied, denied.DenialReason);
    }

    [TestMethod]
    public async Task GrantFutureCaptures_ShouldCommitAuthoritativeControlBeforeConsentLatch()
    {
        var ordering = new List<string>();
        var store = new TestControlStore(CreateSnapshot(CaptureAnalysisPolicy.Unknown), ordering);
        var settings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Unknown, ordering);
        CaptureAnalysisPolicyService service = CreateService(
            store,
            settings,
            new TestFeatureAvailability(true),
            17);
        CaptureAnalysisConsentResponse response = CreateConsentResponse(
            CaptureAnalysisConsentDecision.GrantedForFutureCaptures);

        CaptureAnalysisPolicyChangeResult result = await service.ApplyConsentDecisionAsync(
            response,
            1);

        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Succeeded, result.Status);
        CollectionAssert.AreEqual(new[] { "control:write", "settings:granted" }, ordering);
        CaptureAnalysisPolicy policy = store.Snapshot.State.Policy;
        Assert.AreEqual(CaptureAnalysisConsentState.Granted, policy.ConsentState);
        Assert.AreEqual(17, policy.FutureCaptureSequenceWatermark);
        Assert.IsTrue(policy.IsFutureCaptureAdmissionEnabled);
        Assert.AreEqual(CaptureAnalysisBackfillState.NotAuthorized, policy.BackfillState);
        Assert.AreEqual(CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurpose, policy.AuthorizedPurpose);
        Assert.AreSame(response.Disclosure.AuthorizationScope, policy.AuthorizationScope);
        Assert.IsTrue(policy.ProcessingPolicy!.IsEquivalentTo(
            CaptureAnalysisPolicyDefaults.CreateLocalOnlyPolicy()));
        Assert.IsTrue(result.Policy.IsProcessingAuthorized);
    }

    [TestMethod]
    public async Task Grant_WhenControlWriteFails_ShouldLeaveSettingsUnchangedAndDeny()
    {
        var store = new TestControlStore(CreateSnapshot(CaptureAnalysisPolicy.Unknown))
        {
            WriteStatus = CaptureAnalysisControlWriteStatus.Unavailable,
        };
        var settings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Unknown);
        CaptureAnalysisPolicyService service = CreateService(
            store,
            settings,
            new TestFeatureAvailability(true),
            12);

        CaptureAnalysisPolicyChangeResult result = await service.ApplyConsentDecisionAsync(
            CreateConsentResponse(CaptureAnalysisConsentDecision.GrantedForFutureCaptures),
            1);
        CaptureAnalysisAuthorizationDecision decision = await service.AuthorizeAsync(
            CreateAuthorizationRequest());

        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Unavailable, result.Status);
        Assert.AreEqual(CaptureAnalysisPolicySnapshotStatus.Available, result.Policy.Status);
        Assert.AreEqual(CaptureAnalysisConsentState.Unknown, settings.State);
        Assert.AreEqual(CaptureAnalysisConsentState.Unknown, store.Snapshot.State.ConsentState);
        Assert.AreEqual(0, settings.SetCalls);
        Assert.IsFalse(decision.IsAuthorized);
        Assert.AreEqual(CaptureAnalysisPolicyDenialReason.ConsentUnknown, decision.DenialReason);
    }

    [TestMethod]
    public async Task Grant_WhenConsentLatchWriteFails_ShouldLeaveCommittedControlFailClosed()
    {
        var store = new TestControlStore(CreateSnapshot(CaptureAnalysisPolicy.Unknown));
        var settings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Unknown)
        {
            SaveResult = SettingsMutationResult.PersistenceFailed,
        };
        CaptureAnalysisPolicyService service = CreateService(
            store,
            settings,
            new TestFeatureAvailability(true),
            currentAssetSequence: 12);

        CaptureAnalysisPolicyChangeResult result = await service.ApplyConsentDecisionAsync(
            CreateConsentResponse(CaptureAnalysisConsentDecision.GrantedForFutureCaptures),
            1);
        CaptureAnalysisAuthorizationDecision decision = await service.AuthorizeAsync(
            CreateAuthorizationRequest());

        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.ReconciliationRequired, result.Status);
        Assert.AreEqual(CaptureAnalysisPolicySnapshotStatus.ConsentMismatch, result.Policy.Status);
        Assert.AreEqual(CaptureAnalysisConsentState.Granted, store.Snapshot.State.ConsentState);
        Assert.AreEqual(CaptureAnalysisConsentState.Unknown, settings.State);
        Assert.IsFalse(decision.IsAuthorized);
        Assert.AreEqual(CaptureAnalysisPolicyDenialReason.ConsentMismatch, decision.DenialReason);
    }

    [TestMethod]
    public async Task Renew_WhenControlWriteFails_ShouldNotReactivateOldGrant()
    {
        CaptureAnalysisPolicy oldPolicy = CreateGrantedPolicy(watermark: 4);
        var store = new TestControlStore(CreateSnapshot(oldPolicy, CreateEnrollment(5)))
        {
            WriteStatus = CaptureAnalysisControlWriteStatus.Unavailable,
        };
        var settings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Unknown);
        CaptureAnalysisPolicyService service = CreateService(
            store,
            settings,
            new TestFeatureAvailability(true),
            currentAssetSequence: 50);

        CaptureAnalysisPolicyChangeResult result = await service.ApplyConsentDecisionAsync(
            CreateConsentResponse(CaptureAnalysisConsentDecision.GrantedForFutureCaptures),
            1);
        CaptureAnalysisAuthorizationDecision decision = await service.AuthorizeAsync(
            CreateAuthorizationRequest());

        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Unavailable, result.Status);
        Assert.AreEqual(CaptureAnalysisConsentState.Unknown, settings.State);
        Assert.AreSame(oldPolicy, store.Snapshot.State.Policy);
        Assert.HasCount(1, store.Snapshot.State.Enrollments);
        Assert.IsFalse(decision.IsAuthorized);
        Assert.AreEqual(CaptureAnalysisPolicyDenialReason.ConsentMismatch, decision.DenialReason);
    }

    [TestMethod]
    public async Task PurposeChange_ShouldRequireTheExactReviewedDisclosure()
    {
        AnalysisPurpose previousPurpose = new(
            CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurposeId,
            CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurposeVersion + 1);
        CaptureAnalysisPolicy previousPolicy = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            new CaptureAnalysisAuthorizationScope(
                previousPurpose,
                AnalysisProcessingPolicy.LocalOnly(previousPurpose),
                CaptureAnalysisPolicyDefaults.CaptureMemorySearchCapabilities),
            currentSequence: 4);
        var store = new TestControlStore(CreateSnapshot(previousPolicy, CreateEnrollment(5)));
        var settings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Granted);
        CaptureAnalysisPolicyService service = CreateService(
            store,
            settings,
            new TestFeatureAvailability(true),
            currentAssetSequence: 20);

        CaptureAnalysisPolicySnapshot reviewRequired = await service.GetCurrentAsync();
        CaptureAnalysisPolicyChangeResult resumed =
            await service.ResumeFutureCaptureAdmissionAsync(1);
        CaptureAnalysisPolicyChangeResult renewed = await service.ApplyConsentDecisionAsync(
            CreateConsentResponse(CaptureAnalysisConsentDecision.GrantedForFutureCaptures),
            1);

        Assert.AreEqual(
            CaptureAnalysisPolicySnapshotStatus.ConsentReviewRequired,
            reviewRequired.Status);
        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Rejected, resumed.Status);
        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Succeeded, renewed.Status);
        Assert.AreEqual(
            CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurpose,
            store.Snapshot.State.AuthorizedPurpose);
        Assert.AreEqual(previousPolicy.PolicyRevision + 1, store.Snapshot.State.PolicyRevision);
        Assert.IsEmpty(store.Snapshot.State.Enrollments);
    }

    [TestMethod]
    public async Task CapabilityScopeChange_ShouldRequireConsentReviewAfterRehydration()
    {
        var incompleteScope = new CaptureAnalysisAuthorizationScope(
            CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurpose,
            CaptureAnalysisPolicyDefaults.CreateLocalOnlyPolicy(),
            [AnalysisCapabilities.OcrDocumentV1]);
        CaptureAnalysisPolicy incompletePolicy = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            incompleteScope,
            currentSequence: 4);
        var store = new TestControlStore(CreateSnapshot(incompletePolicy));
        CaptureAnalysisPolicyService service = CreateService(
            store,
            new ConsentSettingsHarness(CaptureAnalysisConsentState.Granted),
            new TestFeatureAvailability(true),
            currentAssetSequence: 20);

        CaptureAnalysisPolicySnapshot snapshot = await service.GetCurrentAsync();
        CaptureAnalysisAuthorizationDecision decision = await service.AuthorizeAsync(
            CreateAuthorizationRequest());

        Assert.AreEqual(
            CaptureAnalysisPolicySnapshotStatus.ConsentReviewRequired,
            snapshot.Status);
        Assert.AreEqual(
            CaptureAnalysisPolicyDenialReason.ConsentReviewRequired,
            decision.DenialReason);
    }

    [TestMethod]
    public async Task Grant_ShouldRejectAChangedDisclosureBeforeAnyMutationOrCatalogRead()
    {
        CaptureAnalysisPolicy policy = CreateGrantedPolicy(watermark: 4);
        var store = new TestControlStore(CreateSnapshot(policy));
        var settings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Granted);
        var catalog = new Mock<ICaptureAssetCatalog>(MockBehavior.Strict);
        var service = new CaptureAnalysisPolicyService(
            catalog.Object,
            store,
            new TestFeatureAvailability(true),
            settings.Service);
        var changedDisclosure = new CaptureAnalysisConsentDisclosure(
            CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurpose,
            CaptureAnalysisPolicyDefaults.CreateLocalOnlyPolicy(),
            [AnalysisCapabilities.OcrDocumentV1]);

        CaptureAnalysisPolicyChangeResult result = await service.ApplyConsentDecisionAsync(
            new CaptureAnalysisConsentResponse(
                changedDisclosure,
                CaptureAnalysisConsentDecision.GrantedForFutureCaptures),
            1);

        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Rejected, result.Status);
        Assert.AreEqual(0, store.WriteCalls);
        Assert.AreEqual(0, settings.SetCalls);
        catalog.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task CancelAndDecline_ShouldRemainSeparateFailClosedDecisions()
    {
        var cancelledStore = new TestControlStore(CreateSnapshot(CaptureAnalysisPolicy.Unknown));
        var cancelledSettings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Unknown);
        CaptureAnalysisPolicyService cancelledService = CreateService(
            cancelledStore,
            cancelledSettings,
            new TestFeatureAvailability(true),
            currentAssetSequence: 10);

        CaptureAnalysisPolicyChangeResult cancelled =
            await cancelledService.ApplyConsentDecisionAsync(
                CreateConsentResponse(CaptureAnalysisConsentDecision.Cancelled),
                1);

        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Rejected, cancelled.Status);
        Assert.AreEqual(0, cancelledStore.WriteCalls);
        Assert.AreEqual(0, cancelledSettings.SetCalls);

        var declinedStore = new TestControlStore(CreateSnapshot(CaptureAnalysisPolicy.Unknown));
        var declinedSettings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Unknown);
        var catalog = new Mock<ICaptureAssetCatalog>(MockBehavior.Strict);
        var declinedService = new CaptureAnalysisPolicyService(
            catalog.Object,
            declinedStore,
            new TestFeatureAvailability(true),
            declinedSettings.Service);

        CaptureAnalysisPolicyChangeResult declined =
            await declinedService.ApplyConsentDecisionAsync(
                CreateConsentResponse(CaptureAnalysisConsentDecision.Declined),
                1);

        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Succeeded, declined.Status);
        Assert.AreEqual(CaptureAnalysisConsentState.Denied, declinedStore.Snapshot.State.ConsentState);
        Assert.AreEqual(CaptureAnalysisConsentState.Denied, declinedSettings.State);
        catalog.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task Backfill_ShouldRequireItsOwnBoundedAuthorization()
    {
        CaptureAnalysisPolicy policy = CreateGrantedPolicy(watermark: 10);
        var store = new TestControlStore(new CaptureAnalysisControlSnapshot(
            1,
            new CaptureAnalysisControlState(
                policy,
                [],
                captureChangeCheckpoint: 17)));
        var settings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Granted);
        CaptureAnalysisPolicyService service = CreateService(
            store,
            settings,
            new TestFeatureAvailability(true),
            25);
        var oldCapture = new CaptureAnalysisAdmissionRequest(
            CreateFinalization(8),
            AnalysisTestData.Purpose,
            CaptureAnalysisAdmissionKind.ExistingCaptureBackfill);

        CaptureAnalysisAdmissionDecision before = await service.AuthorizeAdmissionAsync(oldCapture);
        CaptureAnalysisPolicyChangeResult result =
            await service.AuthorizeExistingCaptureBackfillAsync(1);
        CaptureAnalysisAdmissionDecision after = await service.AuthorizeAdmissionAsync(oldCapture);

        Assert.AreEqual(CaptureAnalysisPolicyDenialReason.BackfillNotAuthorized, before.DenialReason);
        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Succeeded, result.Status);
        Assert.AreEqual(CaptureAnalysisBackfillState.Authorized, store.Snapshot.State.BackfillState);
        Assert.AreEqual(25, store.Snapshot.State.BackfillUpperSequence);
        Assert.AreEqual(policy.PolicyRevision, store.Snapshot.State.PolicyRevision);
        Assert.AreEqual(policy.ControlGeneration, store.Snapshot.State.ControlGeneration);
        Assert.AreEqual(17, store.Snapshot.State.CaptureChangeCheckpoint);
        Assert.IsTrue(after.IsAuthorized);
        Assert.AreEqual(0, after.EnrollmentGeneration);
    }

    [TestMethod]
    public async Task Admission_ShouldEnforceWatermarkAndPrivateCaptureBeforeEnrollment()
    {
        CaptureAnalysisPolicy policy = CreateGrantedPolicy(watermark: 20);
        var store = new TestControlStore(CreateSnapshot(policy));
        CaptureAnalysisPolicyService service = CreateService(
            store,
            new ConsentSettingsHarness(CaptureAnalysisConsentState.Granted),
            new TestFeatureAvailability(true),
            20);

        CaptureAnalysisAdmissionDecision atWatermark = await service.AuthorizeAdmissionAsync(
            new CaptureAnalysisAdmissionRequest(
                CreateFinalization(20),
                AnalysisTestData.Purpose,
                CaptureAnalysisAdmissionKind.FutureCapture));
        CaptureAnalysisAdmissionDecision afterWatermark = await service.AuthorizeAdmissionAsync(
            new CaptureAnalysisAdmissionRequest(
                CreateFinalization(21),
                AnalysisTestData.Purpose,
                CaptureAnalysisAdmissionKind.FutureCapture));
        CaptureAnalysisAdmissionDecision privateCapture = await service.AuthorizeAdmissionAsync(
            new CaptureAnalysisAdmissionRequest(
                CreateFinalization(21),
                AnalysisTestData.Purpose,
                CaptureAnalysisAdmissionKind.FutureCapture,
                isPrivateCapture: true));

        Assert.AreEqual(
            CaptureAnalysisPolicyDenialReason.CaptureBeforeFutureWatermark,
            atWatermark.DenialReason);
        Assert.IsTrue(afterWatermark.IsAuthorized);
        Assert.AreEqual(CaptureAnalysisPolicyDenialReason.PrivateCapture, privateCapture.DenialReason);
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisAdmissionRequest(
            new CaptureAssetChange(
                21,
                AnalysisTestData.CaptureId,
                2,
                CaptureAssetChangeType.PreferredLocationChanged,
                AnalysisTestData.CapturedAtUtc),
            AnalysisTestData.Purpose,
            CaptureAnalysisAdmissionKind.FutureCapture));
    }

    [TestMethod]
    public async Task StopAndReenable_ShouldSkipStoppedIntervalWithoutStalingEnrolledWork()
    {
        CaptureAnalysisPolicy policy = CreateGrantedPolicy(watermark: 5);
        var store = new TestControlStore(CreateSnapshot(policy));
        var settings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Granted);
        var catalog = new Mock<ICaptureAssetCatalog>();
        long sequence = 10;
        catalog.Setup(value => value.GetLatestChangeSequence()).Returns(() => sequence);
        var service = new CaptureAnalysisPolicyService(
            catalog.Object,
            store,
            new TestFeatureAvailability(true),
            settings.Service);

        CaptureAnalysisPolicyChangeResult stopped = await service.StopFutureCapturesAsync(1);
        long stoppedPolicyRevision = store.Snapshot.State.PolicyRevision;
        long stoppedControlGeneration = store.Snapshot.State.ControlGeneration;
        sequence = 20;
        CaptureAnalysisPolicyChangeResult reenabled =
            await service.ResumeFutureCaptureAdmissionAsync(2);

        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Succeeded, stopped.Status);
        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Succeeded, reenabled.Status);
        Assert.AreEqual(20, store.Snapshot.State.FutureCaptureSequenceWatermark);
        Assert.IsTrue(store.Snapshot.State.IsFutureCaptureAdmissionEnabled);
        Assert.AreEqual(stoppedPolicyRevision, store.Snapshot.State.PolicyRevision);
        Assert.AreEqual(stoppedControlGeneration, store.Snapshot.State.ControlGeneration);
        Assert.IsFalse(store.Snapshot.State.Policy.IsFutureCaptureEligible(20));
        Assert.IsTrue(store.Snapshot.State.Policy.IsFutureCaptureEligible(21));
    }

    [TestMethod]
    public async Task Revoke_ShouldFenceControlBeforeUpdatingSettings()
    {
        var ordering = new List<string>();
        CaptureAnalysisPolicy policy = CreateGrantedPolicy(watermark: 3);
        var store = new TestControlStore(CreateSnapshot(policy), ordering);
        var settings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Granted, ordering);
        CaptureAnalysisPolicyService service = CreateService(
            store,
            settings,
            new TestFeatureAvailability(true),
            30);

        CaptureAnalysisPolicyChangeResult result =
            await service.RevokeAsync(1);

        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Succeeded, result.Status);
        CollectionAssert.AreEqual(new[] { "control:write", "settings:denied" }, ordering);
        Assert.AreEqual(CaptureAnalysisConsentState.Denied, store.Snapshot.State.ConsentState);
        Assert.AreEqual(policy.PolicyRevision + 1, store.Snapshot.State.PolicyRevision);
        Assert.AreEqual(policy.ControlGeneration + 1, store.Snapshot.State.ControlGeneration);
        Assert.IsFalse(result.Policy.IsProcessingAuthorized);
    }

    [TestMethod]
    public async Task Revocation_ShouldRejectAnInFlightCapabilityCompletion()
    {
        CaptureAnalysisPolicy policy = CreateGrantedPolicy(watermark: 3);
        var store = new TestControlStore(CreateSnapshot(policy, CreateEnrollment(4)));
        var settings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Granted);
        CaptureAnalysisPolicyService service = CreateService(
            store,
            settings,
            new TestFeatureAvailability(true),
            currentAssetSequence: 10);
        var commitRequest = new CaptureAnalysisAuthorizationRequest(
            AnalysisTestData.CaptureId,
            AnalysisTestData.Purpose,
            AnalysisCapabilities.OcrDocumentV1,
            ProcessingBoundary.OnDevice,
            AnalysisTestData.CreateAnalyzer(),
            CaptureAnalysisAuthorizationStage.CapabilityCommit);

        CaptureAnalysisAuthorizationDecision before = await service.AuthorizeAsync(commitRequest);
        CaptureAnalysisPolicyChangeResult revoked = await service.RevokeAsync(1);
        CaptureAnalysisAuthorizationDecision after = await service.AuthorizeAsync(commitRequest);

        Assert.IsTrue(before.IsAuthorized);
        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Succeeded, revoked.Status);
        Assert.IsFalse(after.IsAuthorized);
        Assert.AreEqual(CaptureAnalysisPolicyDenialReason.ConsentDenied, after.DenialReason);
        Assert.AreNotEqual(before.PolicyRevision, store.Snapshot.State.PolicyRevision);
        Assert.AreNotEqual(before.ControlGeneration, store.Snapshot.State.ControlGeneration);
    }

    [TestMethod]
    public async Task Revoke_WhenSettingsUpdateFails_ShouldRemainDeniedAndRequireReconciliation()
    {
        CaptureAnalysisPolicy policy = CreateGrantedPolicy(watermark: 3);
        var store = new TestControlStore(CreateSnapshot(policy));
        var settings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Granted)
        {
            SaveResult = SettingsMutationResult.PersistenceFailed,
        };
        CaptureAnalysisPolicyService service = CreateService(
            store,
            settings,
            new TestFeatureAvailability(true),
            40);

        CaptureAnalysisPolicyChangeResult result =
            await service.RevokeAsync(1);
        CaptureAnalysisAuthorizationDecision decision = await service.AuthorizeAsync(
            CreateAuthorizationRequest());

        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.ReconciliationRequired, result.Status);
        Assert.AreEqual(CaptureAnalysisConsentState.Denied, store.Snapshot.State.ConsentState);
        Assert.AreEqual(CaptureAnalysisPolicySnapshotStatus.ConsentMismatch, result.Policy.Status);
        Assert.IsFalse(decision.IsAuthorized);
        Assert.AreEqual(CaptureAnalysisPolicyDenialReason.ConsentMismatch, decision.DenialReason);
    }

    [TestMethod]
    public async Task FeatureDisabled_ShouldStillExposeRevisionAndAllowRevocationWithoutCatalog()
    {
        CaptureAnalysisPolicy policy = CreateGrantedPolicy(watermark: 3);
        var store = new TestControlStore(CreateSnapshot(policy, CreateEnrollment(4)));
        var settings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Granted);
        var catalog = new Mock<ICaptureAssetCatalog>(MockBehavior.Strict);
        var service = new CaptureAnalysisPolicyService(
            catalog.Object,
            store,
            new TestFeatureAvailability(false),
            settings.Service);

        CaptureAnalysisPolicySnapshot disabled = await service.GetCurrentAsync();
        CaptureAnalysisPolicyChangeResult revoked =
            await service.RevokeAsync(disabled.ControlDocumentRevision);

        Assert.AreEqual(CaptureAnalysisPolicySnapshotStatus.FeatureDisabled, disabled.Status);
        Assert.AreEqual(1, disabled.ControlDocumentRevision);
        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Succeeded, revoked.Status);
        Assert.AreEqual(CaptureAnalysisPolicySnapshotStatus.FeatureDisabled, revoked.Policy.Status);
        Assert.AreEqual(CaptureAnalysisConsentState.Denied, store.Snapshot.State.ConsentState);
        Assert.IsEmpty(store.Snapshot.State.Enrollments);
        catalog.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RevokeAndRenew_ShouldNotResurrectOldEnrollments()
    {
        CaptureAnalysisPolicy policy = CreateGrantedPolicy(watermark: 3);
        CaptureAnalysisEnrollment enrolled = CreateEnrollment(assetSequence: 4);
        CaptureAnalysisEnrollment excluded = new(
            CaptureId.New(),
            CaptureAnalysisEnrollmentState.Excluded,
            CaptureAnalysisExclusionReason.UserExcluded,
            enrollmentGeneration: 1,
            tombstoneGeneration: 0,
            assetFinalizationSequence: 4,
            requestedRecipeId: null,
            requestedRecipeVersion: null);
        CaptureAnalysisEnrollment forgotten = new(
            CaptureId.New(),
            CaptureAnalysisEnrollmentState.Forgotten,
            CaptureAnalysisExclusionReason.None,
            enrollmentGeneration: 1,
            tombstoneGeneration: 1,
            assetFinalizationSequence: 4,
            requestedRecipeId: null,
            requestedRecipeVersion: null);
        var store = new TestControlStore(CreateSnapshot(policy, enrolled, excluded, forgotten));
        var settings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Granted);
        CaptureAnalysisPolicyService service = CreateService(
            store,
            settings,
            new TestFeatureAvailability(true),
            currentAssetSequence: 20);

        CaptureAnalysisPolicyChangeResult revoked =
            await service.RevokeAsync(1);
        CaptureAnalysisPolicyChangeResult renewed =
            await service.ApplyConsentDecisionAsync(
                CreateConsentResponse(CaptureAnalysisConsentDecision.GrantedForFutureCaptures),
                2);
        CaptureAnalysisAuthorizationDecision authorization = await service.AuthorizeAsync(
            CreateAuthorizationRequest());
        CaptureAnalysisAdmissionDecision admission = await service.AuthorizeAdmissionAsync(
            new CaptureAnalysisAdmissionRequest(
                CreateFinalization(4),
                AnalysisTestData.Purpose,
                CaptureAnalysisAdmissionKind.FutureCapture));

        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Succeeded, revoked.Status);
        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Succeeded, renewed.Status);
        Assert.HasCount(2, store.Snapshot.State.Enrollments);
        Assert.IsTrue(store.Snapshot.State.Enrollments.Contains(excluded));
        Assert.IsTrue(store.Snapshot.State.Enrollments.Contains(forgotten));
        Assert.AreEqual(
            CaptureAnalysisPolicyDenialReason.CaptureNotEnrolled,
            authorization.DenialReason);
        Assert.AreEqual(
            CaptureAnalysisPolicyDenialReason.CaptureBeforeFutureWatermark,
            admission.DenialReason);
    }

    [TestMethod]
    public async Task LocalOnlyPolicy_ShouldRejectRemoteBeforeProviderFeatureChecks()
    {
        CaptureAnalysisPolicy policy = CreateGrantedPolicy(watermark: 1);
        CaptureAnalysisEnrollment enrollment = CreateEnrollment(assetSequence: 2);
        var store = new TestControlStore(CreateSnapshot(policy, enrollment));
        var features = new TestFeatureAvailability(true);
        CaptureAnalysisPolicyService service = CreateService(
            store,
            new ConsentSettingsHarness(CaptureAnalysisConsentState.Granted),
            features,
            2);
        AnalyzerIdentity remote = AnalysisTestData.CreateAnalyzer(
            analyzerId: "azure.vision",
            providerId: "microsoft.azure");
        var request = new CaptureAnalysisAuthorizationRequest(
            AnalysisTestData.CaptureId,
            AnalysisTestData.Purpose,
            AnalysisCapabilities.OcrDocumentV1,
            ProcessingBoundary.Remote,
            remote,
            CaptureAnalysisAuthorizationStage.AnalyzerInvocation);

        CaptureAnalysisAuthorizationDecision decision = await service.AuthorizeAsync(request);

        Assert.AreEqual(
            CaptureAnalysisPolicyDenialReason.BoundaryNotAuthorized,
            decision.DenialReason);
        Assert.AreEqual(0, features.ProviderChecks);
        Assert.AreEqual(0, features.AnalyzerChecks);
    }

    [TestMethod]
    [DataRow(CaptureAnalysisAuthorizationStage.SourceVerification)]
    [DataRow(CaptureAnalysisAuthorizationStage.AnalyzerAvailability)]
    [DataRow(CaptureAnalysisAuthorizationStage.AnalyzerInvocation)]
    [DataRow(CaptureAnalysisAuthorizationStage.CapabilityCommit)]
    public async Task UndisclosedCapability_ShouldBeDeniedAtEveryStageBeforeProviderChecks(
        CaptureAnalysisAuthorizationStage stage)
    {
        CaptureAnalysisPolicy policy = CreateGrantedPolicy(watermark: 1);
        CaptureAnalysisEnrollment enrollment = CreateEnrollment(assetSequence: 2);
        var store = new TestControlStore(CreateSnapshot(policy, enrollment));
        var features = new TestFeatureAvailability(true);
        CaptureAnalysisPolicyService service = CreateService(
            store,
            new ConsentSettingsHarness(CaptureAnalysisConsentState.Granted),
            features,
            2);
        CapabilityDefinition changedOcrSchema = new(
            AnalysisCapabilities.OcrDocumentV1.Id,
            new CapabilitySchemaVersion(2),
            AnalysisCapabilities.OcrDocumentV1.Classification);
        var request = new CaptureAnalysisAuthorizationRequest(
            AnalysisTestData.CaptureId,
            AnalysisTestData.Purpose,
            changedOcrSchema,
            ProcessingBoundary.OnDevice,
            AnalysisTestData.CreateAnalyzer(),
            stage);

        CaptureAnalysisAuthorizationDecision decision = await service.AuthorizeAsync(request);

        Assert.AreEqual(
            CaptureAnalysisPolicyDenialReason.CapabilityNotAuthorized,
            decision.DenialReason);
        Assert.AreEqual(0, features.ProviderChecks);
        Assert.AreEqual(0, features.AnalyzerChecks);
    }

    [TestMethod]
    public async Task ClearedSettingsMirror_ShouldDenyAndRenewAtFreshWatermark()
    {
        CaptureAnalysisPolicy policy = CreateGrantedPolicy(watermark: 4);
        var store = new TestControlStore(CreateSnapshot(policy, CreateEnrollment(5)));
        var settings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Unknown);
        CaptureAnalysisPolicyService service = CreateService(
            store,
            settings,
            new TestFeatureAvailability(true),
            50);

        CaptureAnalysisAuthorizationDecision denied = await service.AuthorizeAsync(
            CreateAuthorizationRequest());
        CaptureAnalysisPolicyChangeResult resumeRejected =
            await service.ResumeFutureCaptureAdmissionAsync(1);
        CaptureAnalysisPolicyChangeResult renewed = await service.ApplyConsentDecisionAsync(
            CreateConsentResponse(CaptureAnalysisConsentDecision.GrantedForFutureCaptures),
            1);

        Assert.AreEqual(CaptureAnalysisPolicyDenialReason.ConsentMismatch, denied.DenialReason);
        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Rejected, resumeRejected.Status);
        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Succeeded, renewed.Status);
        Assert.AreEqual(50, store.Snapshot.State.FutureCaptureSequenceWatermark);
        Assert.AreEqual(policy.PolicyRevision + 1, store.Snapshot.State.PolicyRevision);
    }

    [TestMethod]
    public async Task StaleDocumentRevision_ShouldConflictWithoutSettingsOrWatermarkMutation()
    {
        CaptureAnalysisPolicy policy = CreateGrantedPolicy(watermark: 4);
        var store = new TestControlStore(CreateSnapshot(policy));
        var settings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Granted);
        var catalog = new Mock<ICaptureAssetCatalog>(MockBehavior.Strict);
        var service = new CaptureAnalysisPolicyService(
            catalog.Object,
            store,
            new TestFeatureAvailability(true),
            settings.Service);

        CaptureAnalysisPolicyChangeResult result = await service.StopFutureCapturesAsync(99);

        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Conflict, result.Status);
        Assert.AreEqual(0, store.WriteCalls);
        Assert.AreEqual(0, settings.SetCalls);
        catalog.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task WriteConflict_ShouldReturnTheWinningFailClosedSnapshot()
    {
        CaptureAnalysisPolicy policy = CreateGrantedPolicy(watermark: 4);
        CaptureAnalysisPolicy winner = policy.Revoke();
        var store = new TestControlStore(CreateSnapshot(policy))
        {
            WriteStatus = CaptureAnalysisControlWriteStatus.Conflict,
            ConflictSnapshot = new CaptureAnalysisControlSnapshot(
                2,
                new CaptureAnalysisControlState(winner, [])),
        };
        var settings = new ConsentSettingsHarness(CaptureAnalysisConsentState.Granted);
        CaptureAnalysisPolicyService service = CreateService(
            store,
            settings,
            new TestFeatureAvailability(true),
            currentAssetSequence: 10);

        CaptureAnalysisPolicyChangeResult result = await service.StopFutureCapturesAsync(1);

        Assert.AreEqual(CaptureAnalysisPolicyChangeStatus.Conflict, result.Status);
        Assert.AreEqual(2, result.Policy.ControlDocumentRevision);
        Assert.AreEqual(CaptureAnalysisPolicySnapshotStatus.ConsentMismatch, result.Policy.Status);
        Assert.IsFalse(result.Policy.IsProcessingAuthorized);
    }

    [TestMethod]
    public void ConsentSettingValues_ShouldFailClosedForUnknownOrMalformedValues()
    {
        Assert.AreEqual(
            CaptureAnalysisConsentState.Unknown,
            CaptureAnalysisConsentSettingValues.Parse(null));
        Assert.AreEqual(
            CaptureAnalysisConsentState.Unknown,
            CaptureAnalysisConsentSettingValues.Parse("GRANTED"));
        Assert.AreEqual(
            CaptureAnalysisConsentState.Unknown,
            CaptureAnalysisConsentSettingValues.Parse("unexpected"));
        Assert.AreEqual(
            CaptureAnalysisConsentSettingValues.Denied,
            CaptureAnalysisConsentSettingValues.Serialize(CaptureAnalysisConsentState.Denied));
    }

    private static CaptureAnalysisPolicyService CreateService(
        TestControlStore store,
        ConsentSettingsHarness settings,
        TestFeatureAvailability features,
        long currentAssetSequence)
    {
        var catalog = new Mock<ICaptureAssetCatalog>();
        catalog
            .Setup(value => value.GetLatestChangeSequence())
            .Returns(currentAssetSequence);
        return new(catalog.Object, store, features, settings.Service);
    }

    private static CaptureAnalysisControlSnapshot CreateSnapshot(
        CaptureAnalysisPolicy policy,
        params CaptureAnalysisEnrollment[] enrollments)
    {
        return new(1, new CaptureAnalysisControlState(policy, enrollments));
    }

    private static CaptureAnalysisPolicy CreateGrantedPolicy(long watermark)
    {
        return CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            CaptureAnalysisPolicyDefaults.CreateAuthorizationScope(),
            watermark);
    }

    private static CaptureAnalysisEnrollment CreateEnrollment(long assetSequence)
    {
        return new(
            AnalysisTestData.CaptureId,
            CaptureAnalysisEnrollmentState.Enrolled,
            CaptureAnalysisExclusionReason.None,
            enrollmentGeneration: 1,
            tombstoneGeneration: 0,
            assetSequence,
            AnalysisTestData.RecipeId,
            new AnalysisRecipeVersion(1));
    }

    private static CaptureAnalysisAuthorizationRequest CreateAuthorizationRequest()
    {
        return new(
            AnalysisTestData.CaptureId,
            AnalysisTestData.Purpose,
            AnalysisCapabilities.OcrDocumentV1,
            ProcessingBoundary.OnDevice,
            AnalysisTestData.CreateAnalyzer(),
            CaptureAnalysisAuthorizationStage.AnalyzerInvocation);
    }

    private static CaptureAssetChange CreateFinalization(long sequence)
    {
        return new(
            sequence,
            AnalysisTestData.CaptureId,
            lifecycleRevision: 1,
            CaptureAssetChangeType.Finalized,
            AnalysisTestData.CapturedAtUtc);
    }

    private static CaptureAnalysisConsentResponse CreateConsentResponse(
        CaptureAnalysisConsentDecision decision)
    {
        return new(CaptureAnalysisPolicyDefaults.CreateConsentDisclosure(), decision);
    }

    private sealed class TestControlStore(
        CaptureAnalysisControlSnapshot snapshot,
        List<string>? ordering = null) : ICaptureAnalysisControlStore
    {
        public CaptureAnalysisControlSnapshot Snapshot { get; private set; } = snapshot;

        public CaptureAnalysisControlWriteStatus WriteStatus { get; set; } =
            CaptureAnalysisControlWriteStatus.Succeeded;

        public CaptureAnalysisControlSnapshot? ConflictSnapshot { get; set; }

        public int ReadCalls { get; private set; }

        public int WriteCalls { get; private set; }

        public ValueTask<CaptureAnalysisControlSnapshot> GetAsync(
            CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            return ValueTask.FromResult(Snapshot);
        }

        public ValueTask<CaptureAnalysisControlWriteResult> TryWriteAsync(
            CaptureAnalysisControlState state,
            long expectedDocumentRevision,
            CancellationToken cancellationToken = default)
        {
            WriteCalls++;
            ordering?.Add("control:write");
            if (WriteStatus != CaptureAnalysisControlWriteStatus.Succeeded)
            {
                return ValueTask.FromResult(new CaptureAnalysisControlWriteResult(
                    WriteStatus,
                    WriteStatus == CaptureAnalysisControlWriteStatus.Conflict
                        ? ConflictSnapshot ?? Snapshot
                        : null));
            }

            Assert.AreEqual(expectedDocumentRevision, Snapshot.DocumentRevision);
            Snapshot = new(Snapshot.DocumentRevision + 1, state);
            return ValueTask.FromResult(new CaptureAnalysisControlWriteResult(
                CaptureAnalysisControlWriteStatus.Succeeded,
                Snapshot));
        }
    }

    private sealed class ConsentSettingsHarness
    {
        public ConsentSettingsHarness(
            CaptureAnalysisConsentState state,
            List<string>? ordering = null)
        {
            State = state;
            Mock = new Mock<ISettingsService>();
            Mock
                .Setup(service => service.IsSet(CaptureToolSettings.Settings_CaptureAnalysisConsent))
                .Returns(() => State != CaptureAnalysisConsentState.Unknown);
            Mock
                .Setup(service => service.Get(CaptureToolSettings.Settings_CaptureAnalysisConsent))
                .Returns(() => CaptureAnalysisConsentSettingValues.Serialize(State));
            Mock
                .Setup(service => service.TrySetAndSaveAsync(
                    CaptureToolSettings.Settings_CaptureAnalysisConsent,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IStringSettingDefinition _, string value, CancellationToken _) =>
                {
                    SetCalls++;
                    ordering?.Add($"settings:{value}");
                    if (SaveResult.Succeeded)
                    {
                        State = CaptureAnalysisConsentSettingValues.Parse(value);
                    }

                    return SaveResult;
                });
        }

        public Mock<ISettingsService> Mock { get; }

        public ISettingsService Service => Mock.Object;

        public CaptureAnalysisConsentState State { get; private set; }

        public SettingsMutationResult SaveResult { get; set; } = SettingsMutationResult.Saved;

        public int SetCalls { get; private set; }
    }

    private sealed class TestFeatureAvailability(bool isEnabled) : ICaptureAnalysisFeatureAvailability
    {
        public bool IsCaptureAnalysisEnabled { get; } = isEnabled;

        public long ResolutionPolicyRevision => 1;

        public int ProviderChecks { get; private set; }

        public int AnalyzerChecks { get; private set; }

        public bool IsProviderEnabled(string providerId)
        {
            ProviderChecks++;
            return IsCaptureAnalysisEnabled;
        }

        public bool IsAnalyzerEnabled(AnalyzerIdentity analyzer)
        {
            AnalyzerChecks++;
            return IsCaptureAnalysisEnabled;
        }
    }
}
