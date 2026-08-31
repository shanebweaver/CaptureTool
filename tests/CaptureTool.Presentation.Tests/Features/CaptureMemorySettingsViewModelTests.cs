using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Presentation.Features.Settings;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class CaptureMemorySettingsViewModelTests
{
    [TestMethod]
    public async Task LoadAsync_WhenFeatureIsDisabled_ShouldRemainHiddenWithoutReadingPolicy()
    {
        var policy = new Mock<ICaptureAnalysisPolicyService>(MockBehavior.Strict);
        var viewModel = new CaptureMemorySettingsViewModel(
            new TestFeatureAvailability(false),
            policy.Object);

        await viewModel.LoadAsync(CancellationToken.None);

        Assert.IsFalse(viewModel.IsVisible);
        policy.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task StopAndResume_ShouldKeepExistingMemoryWhileChangingFutureAdmission()
    {
        CaptureAnalysisPolicySnapshot current = CreateSnapshot(futureAdmission: true);
        var policy = new Mock<ICaptureAnalysisPolicyService>();
        policy.Setup(value => value.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => current);
        var commands = new Mock<ICaptureAnalysisPolicyCommandService>(MockBehavior.Strict);
        commands.Setup(value => value.StopFutureCapturesAsync(
                current.ControlDocumentRevision,
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                current = CreateSnapshot(futureAdmission: false, documentRevision: 2);
                return ValueTask.FromResult(new CaptureAnalysisPolicyChangeResult(
                    CaptureAnalysisPolicyChangeStatus.Succeeded,
                    current));
            });
        commands.Setup(value => value.ResumeFutureCaptureAdmissionAsync(
                2,
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                current = CreateSnapshot(futureAdmission: true, documentRevision: 3);
                return ValueTask.FromResult(new CaptureAnalysisPolicyChangeResult(
                    CaptureAnalysisPolicyChangeStatus.Succeeded,
                    current));
            });
        var confirmation = CreateConfirmation(CaptureAnalysisSettingsAction.StopAnalyzingNewCaptures);
        var viewModel = CreateViewModel(
            policy.Object,
            commands.Object,
            confirmationService: confirmation.Object);
        await viewModel.LoadAsync(CancellationToken.None);

        Assert.IsTrue(viewModel.CanChangeAnalysisState);
        await viewModel.SetAnalyzingNewCapturesAsync(false);

        Assert.IsTrue(viewModel.IsAuthorized);
        Assert.IsFalse(viewModel.IsAnalyzingNewCaptures);
        Assert.AreEqual(1, viewModel.ActiveCaptureCount);

        await viewModel.SetAnalyzingNewCapturesAsync(true);

        Assert.IsTrue(viewModel.IsAnalyzingNewCaptures);
        Assert.AreEqual(1, viewModel.ActiveCaptureCount);
        commands.VerifyAll();
        confirmation.VerifyAll();
    }

    [TestMethod]
    public async Task RebuildSearchIndex_ShouldInvokeOnlyTheProjectionMaintenanceCommand()
    {
        var policy = CreatePolicyService();
        var maintenance = new Mock<ICaptureAnalysisMaintenanceService>(MockBehavior.Strict);
        maintenance.Setup(value => value.RebuildSearchIndexAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureAnalysisMaintenanceResult(
                CaptureAnalysisMaintenanceStatus.Succeeded,
                1));
        var confirmation = CreateConfirmation(CaptureAnalysisSettingsAction.RebuildSearchIndex);
        var viewModel = CreateViewModel(
            policy.Object,
            maintenanceService: maintenance.Object,
            confirmationService: confirmation.Object);
        await viewModel.LoadAsync(CancellationToken.None);

        await viewModel.RebuildSearchIndexCommand.ExecuteAsync(null);

        StringAssert.Contains(viewModel.OperationStatusText, "without running AI models");
        maintenance.VerifyAll();
        confirmation.VerifyAll();
    }

    [TestMethod]
    public async Task ReanalyzeCaptures_ShouldExposeModelAndSchedulingProgress()
    {
        var policy = CreatePolicyService();
        var maintenance = new Mock<ICaptureAnalysisMaintenanceService>(MockBehavior.Strict);
        maintenance.Setup(value => value.ReanalyzeCapturesAsync(
                It.Is<CaptureAnalysisReanalysisRequest>(request =>
                    request.Scope == CaptureAnalysisReanalysisScope.AllEnrolledCaptures),
                It.IsAny<IProgress<CaptureAnalysisMaintenanceProgress>>(),
                It.IsAny<CancellationToken>()))
            .Returns<CaptureAnalysisReanalysisRequest,
                IProgress<CaptureAnalysisMaintenanceProgress>,
                CancellationToken>((_, progress, _) =>
                {
                    progress.Report(new CaptureAnalysisMaintenanceProgress(
                        CaptureAnalysisMaintenancePhase.PreparingModels,
                        0.25));
                    progress.Report(new CaptureAnalysisMaintenanceProgress(
                        CaptureAnalysisMaintenancePhase.SchedulingCaptures,
                        1));
                    return ValueTask.FromResult(new CaptureAnalysisMaintenanceResult(
                        CaptureAnalysisMaintenanceStatus.Succeeded,
                        1));
                });
        var confirmation = CreateConfirmation(CaptureAnalysisSettingsAction.ReanalyzeCaptures);
        var viewModel = CreateViewModel(
            policy.Object,
            maintenanceService: maintenance.Object,
            confirmationService: confirmation.Object);
        await viewModel.LoadAsync(CancellationToken.None);

        await viewModel.ReanalyzeCapturesCommand.ExecuteAsync(null);

        Assert.AreEqual(1, viewModel.OperationProgress);
        Assert.IsFalse(viewModel.IsBusy);
        Assert.IsFalse(viewModel.HasOperationFailure);
        StringAssert.Contains(viewModel.OperationStatusText, "queued");
        maintenance.VerifyAll();
    }

    [TestMethod]
    public async Task ReanalyzeCaptures_AfterClear_ShouldRemainAvailableForClearedEnrollments()
    {
        CaptureAnalysisPolicySnapshot cleared = CreateClearedSnapshot();
        var policy = new Mock<ICaptureAnalysisPolicyService>();
        policy.Setup(value => value.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(cleared);
        var maintenance = new Mock<ICaptureAnalysisMaintenanceService>(MockBehavior.Strict);
        maintenance.Setup(value => value.ReanalyzeCapturesAsync(
                It.Is<CaptureAnalysisReanalysisRequest>(request =>
                    request.Scope == CaptureAnalysisReanalysisScope.AllEnrolledCaptures),
                It.IsAny<IProgress<CaptureAnalysisMaintenanceProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureAnalysisMaintenanceResult(
                CaptureAnalysisMaintenanceStatus.Succeeded,
                1));
        var confirmation = CreateConfirmation(CaptureAnalysisSettingsAction.ReanalyzeCaptures);
        var viewModel = CreateViewModel(
            policy.Object,
            maintenanceService: maintenance.Object,
            confirmationService: confirmation.Object);
        await viewModel.LoadAsync(CancellationToken.None);

        Assert.AreEqual(0, viewModel.ActiveCaptureCount);
        Assert.AreEqual(1, viewModel.ReanalyzableCaptureCount);
        Assert.IsTrue(viewModel.ReanalyzeCapturesCommand.CanExecute(null));

        await viewModel.ReanalyzeCapturesCommand.ExecuteAsync(null);

        Assert.IsFalse(viewModel.HasOperationFailure);
        StringAssert.Contains(viewModel.OperationStatusText, "queued");
        maintenance.VerifyAll();
        confirmation.VerifyAll();
    }

    [TestMethod]
    public async Task ReanalyzeCaptures_Cancel_ShouldCancelApplicationOperationAndRefreshState()
    {
        var policy = CreatePolicyService();
        var operationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var maintenance = new Mock<ICaptureAnalysisMaintenanceService>(MockBehavior.Strict);
        maintenance.Setup(value => value.ReanalyzeCapturesAsync(
                It.IsAny<CaptureAnalysisReanalysisRequest>(),
                It.IsAny<IProgress<CaptureAnalysisMaintenanceProgress>>(),
                It.IsAny<CancellationToken>()))
            .Returns<CaptureAnalysisReanalysisRequest,
                IProgress<CaptureAnalysisMaintenanceProgress>,
                CancellationToken>(async (_, progress, token) =>
                {
                    progress.Report(new CaptureAnalysisMaintenanceProgress(
                        CaptureAnalysisMaintenancePhase.PreparingModels,
                        0.2));
                    operationStarted.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return new CaptureAnalysisMaintenanceResult(
                        CaptureAnalysisMaintenanceStatus.Succeeded);
                });
        var confirmation = CreateConfirmation(CaptureAnalysisSettingsAction.ReanalyzeCaptures);
        var viewModel = CreateViewModel(
            policy.Object,
            maintenanceService: maintenance.Object,
            confirmationService: confirmation.Object);
        await viewModel.LoadAsync(CancellationToken.None);

        Task operation = viewModel.ReanalyzeCapturesCommand.ExecuteAsync(null);
        await operationStarted.Task;
        viewModel.CancelOperationCommand.Execute(null);
        await operation;

        Assert.IsFalse(viewModel.IsBusy);
        StringAssert.Contains(viewModel.OperationStatusText, "cancelled");
        Assert.IsTrue(viewModel.IsAuthorized);
    }

    [TestMethod]
    public async Task ReanalyzeCaptures_WhenModelIsUnavailable_ShouldExposeRecoverableFailure()
    {
        var policy = CreatePolicyService();
        var maintenance = new Mock<ICaptureAnalysisMaintenanceService>();
        maintenance.Setup(value => value.ReanalyzeCapturesAsync(
                It.IsAny<CaptureAnalysisReanalysisRequest>(),
                It.IsAny<IProgress<CaptureAnalysisMaintenanceProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureAnalysisMaintenanceResult(
                CaptureAnalysisMaintenanceStatus.Incomplete));
        var viewModel = CreateViewModel(
            policy.Object,
            maintenanceService: maintenance.Object,
            confirmationService: CreateConfirmation(
                CaptureAnalysisSettingsAction.ReanalyzeCaptures).Object);
        await viewModel.LoadAsync(CancellationToken.None);

        await viewModel.ReanalyzeCapturesCommand.ExecuteAsync(null);

        Assert.IsTrue(viewModel.HasOperationFailure);
        Assert.IsTrue(viewModel.IsModelUnavailable);
        StringAssert.Contains(viewModel.OperationStatusText, "required AI model");
    }

    [TestMethod]
    public async Task TurnOffAndErase_WhenReconciliationIsIncomplete_ShouldRemainOffAndShowRecovery()
    {
        CaptureAnalysisPolicySnapshot current = CreateSnapshot(futureAdmission: true);
        var policy = new Mock<ICaptureAnalysisPolicyService>();
        policy.Setup(value => value.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => current);
        var commands = new Mock<ICaptureAnalysisPolicyCommandService>();
        commands.Setup(value => value.RevokeAsync(
                current.ControlDocumentRevision,
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                current = CreateOffSnapshot(documentRevision: 2);
                return ValueTask.FromResult(new CaptureAnalysisPolicyChangeResult(
                    CaptureAnalysisPolicyChangeStatus.ReconciliationRequired,
                    current));
            });
        var viewModel = CreateViewModel(
            policy.Object,
            commands.Object,
            confirmationService: CreateConfirmation(
                CaptureAnalysisSettingsAction.TurnOffAndErase).Object);
        await viewModel.LoadAsync(CancellationToken.None);

        await viewModel.TurnOffAndEraseCommand.ExecuteAsync(null);

        Assert.IsFalse(viewModel.IsAuthorized);
        Assert.IsTrue(viewModel.NeedsRecovery);
        Assert.AreEqual(0, viewModel.ActiveCaptureCount);
    }

    [TestMethod]
    public async Task EnableCaptureMemory_WhenOff_ShouldGrantFutureCapturesAndPrepareModels()
    {
        CaptureAnalysisPolicySnapshot current = CreateOffSnapshot(documentRevision: 2);
        CaptureAnalysisPolicySnapshot enabled = CreateSnapshot(
            futureAdmission: true,
            documentRevision: 3);
        var policy = new Mock<ICaptureAnalysisPolicyService>();
        policy.Setup(value => value.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => current);
        var commands = new Mock<ICaptureAnalysisPolicyCommandService>();
        commands.Setup(value => value.ApplyConsentDecisionAsync(
                It.Is<CaptureAnalysisConsentResponse>(response =>
                    response.Decision ==
                        CaptureAnalysisConsentDecision.GrantedForFutureCaptures),
                2,
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                current = enabled;
                return ValueTask.FromResult(new CaptureAnalysisPolicyChangeResult(
                    CaptureAnalysisPolicyChangeStatus.Succeeded,
                    enabled));
            });
        var preparation = new Mock<IUserInitiatedAnalysisCapabilityPreparationService>();
        preparation.Setup(value => value.PrepareAsync(
                It.IsAny<AnalysisCapabilityPreparationRequest>(),
                It.IsAny<IProgress<AnalysisCapabilityPreparationProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnalysisCapabilityPreparationRequest request,
                IProgress<AnalysisCapabilityPreparationProgress> progress,
                CancellationToken _) =>
            {
                progress.Report(new AnalysisCapabilityPreparationProgress(1));
                return AnalysisCapabilityPreparationState.Ready(
                    CreateAnalyzer(request.Capability.Id.Value),
                    ProcessingBoundary.OnDevice);
            });
        var viewModel = CreateViewModel(
            policy.Object,
            commands.Object,
            preparationService: preparation.Object);
        await viewModel.LoadAsync(CancellationToken.None);

        Assert.IsTrue(viewModel.ShowEnableAction);
        Assert.IsTrue(viewModel.EnableCaptureMemoryCommand.CanExecute(null));
        Assert.IsFalse(viewModel.IncludeExistingCaptures);

        await viewModel.EnableCaptureMemoryCommand.ExecuteAsync(null);

        Assert.IsTrue(viewModel.IsAuthorized);
        Assert.IsTrue(viewModel.IsAnalyzingNewCaptures);
        Assert.IsFalse(viewModel.ShowEnableAction);
        Assert.AreEqual(1, viewModel.OperationProgress);
        StringAssert.Contains(viewModel.OperationStatusText, "models are ready");
        commands.Verify(value => value.AuthorizeExistingCaptureBackfillAsync(
            It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        preparation.Verify(value => value.PrepareAsync(
            It.IsAny<AnalysisCapabilityPreparationRequest>(),
            It.IsAny<IProgress<AnalysisCapabilityPreparationProgress>>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task EnableWithExistingCaptures_ShouldAuthorizeAndQueueAfterPreparation(
        bool limitedCoverage)
    {
        using var fixture = new EnableFixture(limitedCoverage);
        await fixture.ViewModel.LoadAsync(CancellationToken.None);
        fixture.ViewModel.IncludeExistingCaptures = true;

        await fixture.ViewModel.EnableCaptureMemoryCommand.ExecuteAsync(null);

        Assert.IsTrue(fixture.ViewModel.IsAuthorized);
        Assert.IsTrue(fixture.ViewModel.IsAnalyzingNewCaptures);
        Assert.IsFalse(fixture.ViewModel.IsBusy);
        Assert.IsTrue(fixture.ViewModel.CanChangeSetupOptions);
        Assert.IsFalse(fixture.ViewModel.HasOperationFailure);
        Assert.IsFalse(fixture.ViewModel.IncludeExistingCaptures);
        Assert.AreEqual(1, fixture.ViewModel.OperationProgress);
        StringAssert.Contains(fixture.ViewModel.OperationStatusText, "Existing captures were queued");
        if (limitedCoverage)
        {
            StringAssert.Contains(fixture.ViewModel.OperationStatusText, "available on-device capabilities");
        }

        // Preparation advanced the control revision from 3 to 4. Backfill must use
        // the current revision, not the stale revision returned by initial consent.
        fixture.Commands.Verify(value => value.AuthorizeExistingCaptureBackfillAsync(
            4, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Backfill.Verify(value => value.RunAsync(
            It.IsAny<IProgress<CaptureAnalysisBackfillProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task EnableWithExistingCaptures_WhenAuthorizationConflicts_ShouldNotRunBackfill()
    {
        using var fixture = new EnableFixture();
        fixture.Commands.Setup(value => value.AuthorizeExistingCaptureBackfillAsync(
                It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new CaptureAnalysisPolicyChangeResult(
                CaptureAnalysisPolicyChangeStatus.Conflict, fixture.Current));
        await fixture.ViewModel.LoadAsync(CancellationToken.None);
        fixture.ViewModel.IncludeExistingCaptures = true;

        await fixture.ViewModel.EnableCaptureMemoryCommand.ExecuteAsync(null);

        Assert.IsTrue(fixture.ViewModel.HasOperationFailure);
        Assert.IsTrue(fixture.ViewModel.IsAuthorized);
        Assert.IsFalse(fixture.ViewModel.IsBusy);
        fixture.Backfill.Verify(value => value.RunAsync(
            It.IsAny<IProgress<CaptureAnalysisBackfillProgress>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    [DataRow(CaptureAnalysisBackfillRunStatus.Unavailable)]
    [DataRow(CaptureAnalysisBackfillRunStatus.Cancelled)]
    public async Task EnableWithExistingCaptures_WhenBackfillDoesNotFinish_ShouldNotReportSuccess(
        CaptureAnalysisBackfillRunStatus status)
    {
        using var fixture = new EnableFixture();
        fixture.Backfill.Setup(value => value.RunAsync(
                It.IsAny<IProgress<CaptureAnalysisBackfillProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureAnalysisBackfillRunResult(
                status, new CaptureAnalysisBackfillProgress(0, 12, 0)));
        await fixture.ViewModel.LoadAsync(CancellationToken.None);
        fixture.ViewModel.IncludeExistingCaptures = true;

        await fixture.ViewModel.EnableCaptureMemoryCommand.ExecuteAsync(null);

        Assert.IsTrue(fixture.ViewModel.IsAuthorized);
        Assert.IsFalse(fixture.ViewModel.IsBusy);
        StringAssert.Contains(fixture.ViewModel.OperationStatusText,
            status == CaptureAnalysisBackfillRunStatus.Cancelled ? "cancelled" : "not all existing captures");
        Assert.AreEqual(status != CaptureAnalysisBackfillRunStatus.Cancelled,
            fixture.ViewModel.HasOperationFailure);
    }

    [TestMethod]
    public async Task EnableWithExistingCaptures_Cancel_ShouldCancelBackfillAndUnlockControls()
    {
        using var fixture = new EnableFixture();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var progressReported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CaptureMemorySettingsViewModel.OperationProgress) &&
                fixture.ViewModel.OperationProgress == 0.5)
            {
                progressReported.TrySetResult();
            }
        };
        fixture.Backfill.Setup(value => value.RunAsync(
                It.IsAny<IProgress<CaptureAnalysisBackfillProgress>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (IProgress<CaptureAnalysisBackfillProgress> progress, CancellationToken token) =>
            {
                progress.Report(new CaptureAnalysisBackfillProgress(6, 12, 1));
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new AssertFailedException("The backfill should have been cancelled.");
            });
        await fixture.ViewModel.LoadAsync(CancellationToken.None);
        fixture.ViewModel.IncludeExistingCaptures = true;

        Task operation = fixture.ViewModel.EnableCaptureMemoryCommand.ExecuteAsync(null);
        await started.Task;
        await progressReported.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(fixture.ViewModel.IsBusy);
        Assert.IsTrue(fixture.ViewModel.IsSchedulingCaptures);
        Assert.IsFalse(fixture.ViewModel.IsPreparingModels);
        Assert.IsFalse(fixture.ViewModel.CanChangeSetupOptions);
        Assert.AreEqual(0.5, fixture.ViewModel.OperationProgress);
        fixture.ViewModel.CancelOperationCommand.Execute(null);
        await operation;

        Assert.IsFalse(fixture.ViewModel.IsBusy);
        Assert.IsTrue(fixture.ViewModel.CanChangeSetupOptions);
        Assert.IsTrue(fixture.ViewModel.IsAuthorized);
        StringAssert.Contains(fixture.ViewModel.OperationStatusText, "cancelled");
    }

    private sealed class EnableFixture : IDisposable
    {
        public EnableFixture(bool limitedCoverage = false)
        {
            var policy = new Mock<ICaptureAnalysisPolicyService>();
            policy.Setup(value => value.GetCurrentAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Current);
            Commands.Setup(value => value.ApplyConsentDecisionAsync(
                    It.IsAny<CaptureAnalysisConsentResponse>(), 2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    Current = CreateSnapshot(futureAdmission: true, documentRevision: 3);
                    return new CaptureAnalysisPolicyChangeResult(
                        CaptureAnalysisPolicyChangeStatus.Succeeded, Current);
                });
            var preparation = new Mock<IUserInitiatedAnalysisCapabilityPreparationService>();
            ViewModel = CreateViewModel(policy.Object, Commands.Object,
                preparationService: preparation.Object, backfillService: Backfill.Object);
            preparation.Setup(value => value.PrepareAsync(
                    It.IsAny<AnalysisCapabilityPreparationRequest>(),
                    It.IsAny<IProgress<AnalysisCapabilityPreparationProgress>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    // Simulate another capture being enrolled while models are preparing.
                    Current = CreateSnapshot(futureAdmission: true, documentRevision: 4);
                    Assert.IsFalse(ViewModel.CanChangeSetupOptions);
                    return limitedCoverage
                        ? AnalysisCapabilityPreparationState.Unsupported(new AnalysisFailure(
                            AnalysisFailureCode.CapabilityUnavailable, AnalysisFailureDisposition.Terminal))
                        : AnalysisCapabilityPreparationState.Ready(
                            CreateAnalyzer("test"), ProcessingBoundary.OnDevice);
                });
            Commands.Setup(value => value.AuthorizeExistingCaptureBackfillAsync(
                    4, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    SetPolicy(Current.Policy!.AuthorizeExistingCaptureBackfill(currentSequence: 12), 5);
                    return new CaptureAnalysisPolicyChangeResult(
                        CaptureAnalysisPolicyChangeStatus.Succeeded, Current);
                });
            Backfill.Setup(value => value.RunAsync(
                    It.IsAny<IProgress<CaptureAnalysisBackfillProgress>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    SetPolicy(Current.Policy!.StartExistingCaptureBackfill()
                        .AdvanceExistingCaptureBackfill(checkpoint: 12), 6);
                    return new CaptureAnalysisBackfillRunResult(
                        CaptureAnalysisBackfillRunStatus.Completed,
                        new CaptureAnalysisBackfillProgress(12, 12, 1));
                });
        }

        public CaptureAnalysisPolicySnapshot Current { get; private set; } = CreateOffSnapshot(2);
        public Mock<ICaptureAnalysisPolicyCommandService> Commands { get; } = new(MockBehavior.Strict);
        public Mock<ICaptureAnalysisBackfillService> Backfill { get; } = new(MockBehavior.Strict);
        public CaptureMemorySettingsViewModel ViewModel { get; }

        public void Dispose() => ViewModel.Dispose();

        private void SetPolicy(CaptureAnalysisPolicy policy, long revision)
        {
            Current = new CaptureAnalysisPolicySnapshot(
                CaptureAnalysisPolicySnapshotStatus.Available,
                CaptureAnalysisConsentState.Granted,
                new CaptureAnalysisControlSnapshot(revision,
                    new CaptureAnalysisControlState(policy, Current.ControlSnapshot!.State.Enrollments)));
        }
    }

    private static CaptureMemorySettingsViewModel CreateViewModel(
        ICaptureAnalysisPolicyService policyService,
        ICaptureAnalysisPolicyCommandService? policyCommandService = null,
        ICaptureAnalysisMaintenanceService? maintenanceService = null,
        ICaptureAnalysisSettingsConfirmationDialogService? confirmationService = null,
        IUserInitiatedAnalysisCapabilityPreparationService? preparationService = null,
        ICaptureAnalysisBackfillService? backfillService = null)
    {
        return new CaptureMemorySettingsViewModel(
            new TestFeatureAvailability(true),
            policyService,
            policyCommandService,
            maintenanceService,
            confirmationService,
            localizationService: null,
            preparationService,
            backfillService);
    }

    private static Mock<ICaptureAnalysisPolicyService> CreatePolicyService()
    {
        var policy = new Mock<ICaptureAnalysisPolicyService>();
        policy.Setup(value => value.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(futureAdmission: true));
        return policy;
    }

    private static Mock<ICaptureAnalysisSettingsConfirmationDialogService> CreateConfirmation(
        CaptureAnalysisSettingsAction expectedAction)
    {
        var confirmation = new Mock<ICaptureAnalysisSettingsConfirmationDialogService>(
            MockBehavior.Strict);
        confirmation.Setup(value => value.ConfirmAsync(
                It.Is<CaptureAnalysisSettingsConfirmationRequest>(request =>
                    request.Action == expectedAction),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CaptureAnalysisConfirmationDecision.Confirmed);
        return confirmation;
    }

    private static CaptureAnalysisPolicySnapshot CreateSnapshot(
        bool futureAdmission,
        long documentRevision = 1)
    {
        CaptureAnalysisPolicy policy = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            CaptureAnalysisPolicyDefaults.CreateAuthorizationScope(),
            currentSequence: 1);
        if (!futureAdmission)
        {
            policy = policy.StopFutureCaptures(currentSequence: 1);
        }

        CaptureAnalysisRecipe recipe = CaptureAnalysisRecipeDefaults
            .CreateCaptureMemoryImageRecipe();
        var enrollment = new CaptureAnalysisEnrollment(
            CaptureId.New(),
            CaptureAnalysisEnrollmentState.Enrolled,
            CaptureAnalysisExclusionReason.None,
            enrollmentGeneration: 1,
            tombstoneGeneration: 0,
            assetFinalizationSequence: 1,
            recipe.Id,
            recipe.Version);
        var control = new CaptureAnalysisControlSnapshot(
            documentRevision,
            new CaptureAnalysisControlState(policy, [enrollment]));
        return new CaptureAnalysisPolicySnapshot(
            CaptureAnalysisPolicySnapshotStatus.Available,
            CaptureAnalysisConsentState.Granted,
            control);
    }

    private static CaptureAnalysisPolicySnapshot CreateOffSnapshot(long documentRevision)
    {
        var control = new CaptureAnalysisControlSnapshot(
            documentRevision,
            new CaptureAnalysisControlState(CaptureAnalysisPolicy.Unknown, []));
        return new CaptureAnalysisPolicySnapshot(
            CaptureAnalysisPolicySnapshotStatus.Available,
            CaptureAnalysisConsentState.Denied,
            control);
    }

    private static CaptureAnalysisPolicySnapshot CreateClearedSnapshot()
    {
        CaptureAnalysisPolicy policy = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            CaptureAnalysisPolicyDefaults.CreateAuthorizationScope(),
            currentSequence: 1);
        var enrollment = new CaptureAnalysisEnrollment(
            CaptureId.New(),
            CaptureAnalysisEnrollmentState.Excluded,
            CaptureAnalysisExclusionReason.MemoryCleared,
            enrollmentGeneration: 2,
            tombstoneGeneration: 1,
            assetFinalizationSequence: 1,
            requestedRecipeId: null,
            requestedRecipeVersion: null);
        var control = new CaptureAnalysisControlSnapshot(
            2,
            new CaptureAnalysisControlState(policy.ClearMemory(currentSequence: 1), [enrollment]));
        return new CaptureAnalysisPolicySnapshot(
            CaptureAnalysisPolicySnapshotStatus.Available,
            CaptureAnalysisConsentState.Granted,
            control);
    }

    private static AnalyzerIdentity CreateAnalyzer(string analyzerId)
    {
        return new AnalyzerIdentity(
            analyzerId,
            "test-provider",
            "test-model",
            "1",
            "1",
            "test-runtime",
            "1",
            null,
            null);
    }

    private sealed class TestFeatureAvailability(bool enabled) :
        ICaptureMemoryFeatureAvailability
    {
        public bool IsCaptureMemorySearchEnabled => enabled;
    }
}
