using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
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

        await viewModel.StopAnalyzingNewCapturesCommand.ExecuteAsync(null);

        Assert.IsTrue(viewModel.IsAuthorized);
        Assert.IsFalse(viewModel.IsAnalyzingNewCaptures);
        Assert.AreEqual(1, viewModel.ActiveCaptureCount);

        await viewModel.ResumeAnalyzingNewCapturesCommand.ExecuteAsync(null);

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

    private static CaptureMemorySettingsViewModel CreateViewModel(
        ICaptureAnalysisPolicyService policyService,
        ICaptureAnalysisPolicyCommandService? policyCommandService = null,
        ICaptureAnalysisMaintenanceService? maintenanceService = null,
        ICaptureAnalysisSettingsConfirmationDialogService? confirmationService = null)
    {
        return new CaptureMemorySettingsViewModel(
            new TestFeatureAvailability(true),
            policyService,
            policyCommandService,
            maintenanceService,
            confirmationService);
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

    private sealed class TestFeatureAvailability(bool enabled) :
        ICaptureMemoryFeatureAvailability
    {
        public bool IsCaptureMemorySearchEnabled => enabled;
    }
}
