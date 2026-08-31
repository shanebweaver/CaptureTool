using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Presentation.Features.Settings;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class CaptureMemorySettingsViewModelTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Enable_ShouldDelegateCheckboxToAppWorkflow(bool includeExisting)
    {
        var workflow = new TestCaptureMemoryWorkflow { Current = new(TestCaptureMemoryWorkflow.Policy(false), null) };
        workflow.Execute = request =>
        {
            var result = TestCaptureMemoryWorkflow.Operation(request.Kind, CaptureMemoryOperationStatus.Succeeded, scheduled: includeExisting);
            workflow.Current = new(TestCaptureMemoryWorkflow.Policy(true), result);
            workflow.Publish();
            return Task.FromResult(result);
        };
        using var vm = Create(workflow);
        await vm.LoadAsync(CancellationToken.None);
        Assert.IsTrue(vm.ShowEnableAction);
        vm.IncludeExistingCaptures = includeExisting;
        await vm.EnableCaptureMemoryCommand.ExecuteAsync(null);
        Assert.AreEqual(includeExisting, workflow.Requests.Single().IncludeExistingCaptures);
        Assert.IsFalse(vm.IsBusy);
        Assert.IsTrue(vm.IsAuthorized);
        Assert.IsFalse(vm.ShowEnableAction);
        Assert.IsFalse(vm.IncludeExistingCaptures);
    }

    [TestMethod]
    [DataRow(CaptureAnalysisSettingsAction.ClearMemory, CaptureMemoryOperationKind.ClearMemory)]
    [DataRow(CaptureAnalysisSettingsAction.TurnOffAndErase, CaptureMemoryOperationKind.TurnOffAndErase)]
    [DataRow(CaptureAnalysisSettingsAction.ReanalyzeCaptures, CaptureMemoryOperationKind.Reanalyze)]
    [DataRow(CaptureAnalysisSettingsAction.RebuildSearchIndex, CaptureMemoryOperationKind.RebuildSearch)]
    [DataRow(CaptureAnalysisSettingsAction.StopAnalyzingNewCaptures, CaptureMemoryOperationKind.StopNewCaptures)]
    public async Task Maintenance_ShouldConfirmAndDelegate(CaptureAnalysisSettingsAction action, CaptureMemoryOperationKind kind)
    {
        var workflow = new TestCaptureMemoryWorkflow();
        var confirmation = new Mock<ICaptureAnalysisSettingsConfirmationDialogService>(MockBehavior.Strict);
        confirmation.Setup(s => s.ConfirmAsync(It.Is<CaptureAnalysisSettingsConfirmationRequest>(r => r.Action == action),
            It.IsAny<CancellationToken>())).ReturnsAsync(CaptureAnalysisConfirmationDecision.Confirmed);
        using var vm = Create(workflow, confirmation.Object);
        await vm.LoadAsync(CancellationToken.None);
        await Command(vm, kind).ExecuteAsync(null);
        Assert.AreEqual(kind, workflow.Requests.Single().Kind);
        Assert.IsFalse(vm.IsBusy);
    }

    [TestMethod]
    public async Task CancelledConfirmation_ShouldNotMutateAndShouldRestoreToggle()
    {
        var workflow = new TestCaptureMemoryWorkflow();
        var confirmation = new Mock<ICaptureAnalysisSettingsConfirmationDialogService>();
        using var vm = Create(workflow, confirmation.Object);
        await vm.LoadAsync(CancellationToken.None);
        await vm.SetAnalyzingNewCapturesAsync(false);
        Assert.IsEmpty(workflow.Requests);
        Assert.IsTrue(vm.IsAnalyzingNewCaptures);
    }

    [TestMethod]
    public async Task Resume_ShouldWorkFromSettingsWithoutHome()
    {
        var workflow = new TestCaptureMemoryWorkflow { Current = new(TestCaptureMemoryWorkflow.Policy(true, future: false), null) };
        using var vm = Create(workflow);
        await vm.LoadAsync(CancellationToken.None);
        Assert.IsTrue(vm.ShowResumeAction);
        await vm.SetAnalyzingNewCapturesAsync(true);
        Assert.AreEqual(CaptureMemoryOperationKind.ResumeNewCaptures, workflow.Requests.Single().Kind);
    }

    [TestMethod]
    [DataRow(CaptureAnalysisExclusionReason.None, true)]
    [DataRow(CaptureAnalysisExclusionReason.MemoryCleared, true)]
    [DataRow(CaptureAnalysisExclusionReason.UserExcluded, false)]
    [DataRow(CaptureAnalysisExclusionReason.PrivateCapture, false)]
    public async Task Reanalyze_ShouldUseCurrentEligibleCount(CaptureAnalysisExclusionReason reason, bool expected)
    {
        var workflow = new TestCaptureMemoryWorkflow { Current = new(TestCaptureMemoryWorkflow.Policy(true, reason: reason), null) };
        using var vm = Create(workflow);
        await vm.LoadAsync(CancellationToken.None);
        Assert.AreEqual(expected, vm.ReanalyzeCapturesCommand.CanExecute(null));
        Assert.AreEqual(!expected, vm.ShowReanalyzeAvailability);
    }

    [TestMethod]
    public async Task SharedOperation_ShouldDisableOverlapButAllowEraseAndIdentityCancellation()
    {
        var operation = TestCaptureMemoryWorkflow.Operation(CaptureMemoryOperationKind.Reanalyze);
        var workflow = new TestCaptureMemoryWorkflow { Current = new(TestCaptureMemoryWorkflow.Policy(true), operation, .4) };
        using var vm = Create(workflow);
        await vm.LoadAsync(CancellationToken.None);
        Assert.IsTrue(vm.IsBusy);
        Assert.IsTrue(vm.IsPreparingModels);
        Assert.AreEqual(.4, vm.OperationProgress);
        Assert.IsFalse(vm.ReanalyzeCapturesCommand.CanExecute(null));
        Assert.IsTrue(vm.TurnOffAndEraseCommand.CanExecute(null));
        vm.CancelOperationCommand.Execute(null);
        Assert.AreEqual(operation.Id, workflow.Cancellations.Single());
        workflow.Current = workflow.Current with { Operation = operation.Advance(CaptureMemoryOperationPhase.Finished, CaptureMemoryOperationStatus.Cancelled) };
        workflow.Publish();
        Assert.IsFalse(vm.IsBusy);
        Assert.IsTrue(vm.ReanalyzeCapturesCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task Navigation_ShouldDetachWithoutCancellingAndReattachToSameWork()
    {
        var operation = TestCaptureMemoryWorkflow.Operation(CaptureMemoryOperationKind.Reanalyze);
        var workflow = new TestCaptureMemoryWorkflow { Current = new(TestCaptureMemoryWorkflow.Policy(true), operation) };
        var first = Create(workflow);
        await first.LoadAsync(CancellationToken.None);
        first.Dispose();
        Assert.IsEmpty(workflow.Cancellations);
        using var second = Create(workflow);
        await second.LoadAsync(CancellationToken.None);
        Assert.IsTrue(second.IsBusy);
        second.CancelOperationCommand.Execute(null);
        Assert.AreEqual(operation.Id, workflow.Cancellations.Single());
    }

    [TestMethod]
    public async Task LatestRead_ShouldWinWhenAnOlderReadCompletesLate()
    {
        var workflow = new TestCaptureMemoryWorkflow();
        using var vm = Create(workflow);
        await vm.LoadAsync(CancellationToken.None);
        var stale = new TaskCompletionSource<CaptureMemoryWorkflowSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        workflow.Read = _ => new(stale.Task);
        Task old = vm.RefreshCommand.ExecuteAsync(null);
        workflow.Read = null;
        workflow.Current = new(TestCaptureMemoryWorkflow.Policy(false), null);
        workflow.Publish();
        stale.SetResult(new(TestCaptureMemoryWorkflow.Policy(true), null));
        await old;
        Assert.IsFalse(vm.IsAuthorized);
        Assert.IsTrue(vm.ShowEnableAction);
    }

    [TestMethod]
    [DataRow(CaptureMemoryOperationStatus.Partial, false, false)]
    [DataRow(CaptureMemoryOperationStatus.Failed, true, false)]
    [DataRow(CaptureMemoryOperationStatus.Conflict, true, false)]
    [DataRow(CaptureMemoryOperationStatus.RecoveryRequired, false, true)]
    public async Task Outcome_ShouldExplainFailureWithoutKeepingCommandsBusy(CaptureMemoryOperationStatus status, bool failure, bool recovery)
    {
        var workflow = new TestCaptureMemoryWorkflow { Current = new(TestCaptureMemoryWorkflow.Policy(true),
            TestCaptureMemoryWorkflow.Operation(CaptureMemoryOperationKind.Reanalyze, status)) };
        using var vm = Create(workflow);
        await vm.LoadAsync(CancellationToken.None);
        Assert.IsFalse(vm.IsBusy);
        Assert.AreEqual(failure, vm.HasOperationFailure);
        Assert.AreEqual(recovery, vm.NeedsRecovery);
        Assert.IsTrue(vm.HasOperationStatus);
        Assert.IsTrue(vm.ReanalyzeCapturesCommand.CanExecute(null));
    }

    private static CommunityToolkit.Mvvm.Input.IAsyncRelayCommand Command(CaptureMemorySettingsViewModel vm, CaptureMemoryOperationKind kind) => kind switch
    {
        CaptureMemoryOperationKind.ClearMemory => vm.ClearMemoryCommand,
        CaptureMemoryOperationKind.TurnOffAndErase => vm.TurnOffAndEraseCommand,
        CaptureMemoryOperationKind.RebuildSearch => vm.RebuildSearchIndexCommand,
        CaptureMemoryOperationKind.Reanalyze => vm.ReanalyzeCapturesCommand,
        _ => vm.StopAnalyzingNewCapturesCommand,
    };
    private static CaptureMemorySettingsViewModel Create(TestCaptureMemoryWorkflow workflow,
        ICaptureAnalysisSettingsConfirmationDialogService? confirmation = null) => new(new Enabled(), workflow, confirmation);
    private sealed class Enabled : ICaptureMemoryFeatureAvailability { public bool IsCaptureMemorySearchEnabled => true; }
}
