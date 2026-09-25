using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Domain.Analysis;
using CaptureTool.Presentation.Features.Settings;
using CaptureTool.Presentation.Notifications;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class CaptureMemoryViewModelTests
{
    [TestMethod]
    public void CoalescesProgressOnDispatcherAndNeverShowsStaleLoadingAfterIdle()
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.State with { Activity = new(AnalysisActivity.Analyzing) });
        fixture.Change(fixture.State with { Activity = new(AnalysisActivity.Idle) });
        Assert.HasCount(1, fixture.Updates);
        fixture.Dispatch();
        Assert.IsFalse(fixture.ViewModel.IsAnalysisActive);
        Assert.AreEqual(string.Empty, fixture.ViewModel.ProgressText);
        fixture.Change(fixture.State with { Activity = new(AnalysisActivity.Preparing) });
        fixture.Dispatch();
        Assert.IsTrue(fixture.ViewModel.IsAnalysisActive);
        Assert.AreEqual("CaptureMemory_Preparing", fixture.ViewModel.ProgressText);
        fixture.Change(fixture.State with { PolicyAvailable = false });
        fixture.Dispatch();
        Assert.IsFalse(fixture.ViewModel.IsAnalysisActive);
    }

    [TestMethod]
    public void DeleteDisablesDuringDeletionAndReenablesAfterFailureWithNoAlternateAction()
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.State with { Storage = new(true, true), IsDeleting = true });
        fixture.Dispatch();
        Assert.IsFalse(fixture.ViewModel.DeleteCommand.CanExecute(null));
        Assert.IsFalse(fixture.ViewModel.ScanCommand.CanExecute(null));
        fixture.Change(fixture.State with { IsDeleting = false, Storage = new(true, true, true), FailureCode = "cleanup-pending" });
        fixture.Dispatch();
        Assert.IsTrue(fixture.ViewModel.DeleteCommand.CanExecute(null));
        fixture.Notifications.Verify(value => value.ShowError("CaptureMemory_Error_Cleanup"), Times.Once);
        fixture.Change(fixture.State);
        fixture.Dispatch();
        fixture.Notifications.Verify(value => value.ShowError(It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public async Task CommandsUseApplicationServiceAndDisabledPolicyPreventsScan()
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.State with { Storage = new(true, true) });
        fixture.Dispatch();
        await fixture.ViewModel.DeleteCommand.ExecuteAsync(null);
        fixture.Memory.Verify(value => value.DeleteMetadataAsync(default), Times.Once);
        await fixture.ViewModel.ScanCommand.ExecuteAsync(null);
        fixture.Memory.Verify(value => value.ScanExistingAsync(default), Times.Once);
        await fixture.ViewModel.SetConsentCommand.ExecuteAsync(false);
        fixture.Memory.Verify(value => value.SetConsentAsync(false, default), Times.Once);
        fixture.Change(fixture.State with { Policy = CaptureMemoryPolicy.Disabled(), Storage = new(false, true) });
        fixture.Dispatch();
        Assert.IsFalse(fixture.ViewModel.ScanCommand.CanExecute(null));
        Assert.IsFalse(fixture.ViewModel.DeleteCommand.CanExecute(null));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RepeatedFailuresAcrossQueuedCapturesDoNotFloodNotifications(bool completed)
    {
        using var fixture = new Fixture();
        for (int i = 0; i < 3; i++)
        {
            fixture.Change(fixture.State with { Activity = new(AnalysisActivity.Analyzing) });
            fixture.Dispatch();
            fixture.Change(fixture.State with { Activity = new(AnalysisActivity.Analyzing,
                LastRunStatus: completed ? AnalysisRunStatus.Completed : AnalysisRunStatus.Failed, LastRunHadFailures: completed) });
            fixture.Dispatch();
        }
        fixture.Notifications.Verify(value => value.ShowError("CaptureMemory_Error_Analysis"), Times.Once);
    }

    [TestMethod]
    [DataRow(AnalysisActivity.Analyzing)]
    [DataRow(AnalysisActivity.Idle)]
    public void CompletedStepFailureSurvivesCoalescingWithTheNextCapture(AnalysisActivity nextActivity)
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.State with { Activity = new(AnalysisActivity.Analyzing,
            LastRunStatus: AnalysisRunStatus.Completed, LastRunHadFailures: true) });
        fixture.Change(fixture.State with { Activity = new(nextActivity,
            LastRunStatus: nextActivity == AnalysisActivity.Idle ? AnalysisRunStatus.Completed : null) });
        fixture.Dispatch();
        fixture.Notifications.Verify(value => value.ShowError("CaptureMemory_Error_Analysis"), Times.Once);
        Assert.AreEqual(nextActivity == AnalysisActivity.Analyzing, fixture.ViewModel.IsAnalysisActive);
    }

    [TestMethod]
    public void SuccessfulCompletionResetsFailureDeduplicationAndUnsupportedStepsRemainQuiet()
    {
        using var fixture = new Fixture();
        var failed = fixture.State with { Activity = new(AnalysisActivity.Idle,
            LastRunStatus: AnalysisRunStatus.Completed, LastRunHadFailures: true) };
        fixture.Change(failed);
        fixture.Dispatch();
        fixture.Change(fixture.State with { Activity = new(AnalysisActivity.Idle,
            FailureCode: "model-unavailable", LastRunStatus: AnalysisRunStatus.Completed) });
        fixture.Dispatch();
        fixture.Notifications.Verify(value => value.ShowError(It.IsAny<string>()), Times.Once);
        fixture.Change(failed);
        fixture.Dispatch();
        fixture.Notifications.Verify(value => value.ShowError("CaptureMemory_Error_Analysis"), Times.Exactly(2));
    }

    [TestMethod]
    public void DisposalIgnoresQueuedUpdatesAndUnsubscribes()
    {
        using var fixture = new Fixture();
        fixture.ViewModel.Dispose();
        fixture.Dispatch();
        fixture.Change(fixture.State with { Activity = new(AnalysisActivity.Analyzing) });
        Assert.IsEmpty(fixture.Updates);
        Assert.IsFalse(fixture.ViewModel.IsAnalysisActive);
    }

    private sealed class Fixture : IDisposable
    {
        public Mock<ICaptureMemoryService> Memory { get; } = new();
        public Mock<IAppNotificationService> Notifications { get; } = new();
        public Queue<Action> Updates { get; } = new();
        public CaptureMemoryState State { get; private set; } = new(new(true, true, Guid.NewGuid(), 0), true, new(false, true), new(AnalysisActivity.Idle));
        public CaptureMemoryViewModel ViewModel { get; }
        public Fixture()
        {
            Memory.SetupGet(value => value.State).Returns(() => State);
            var ui = new Mock<ITaskEnvironment>();
            ui.Setup(value => value.TryExecute(It.IsAny<Action>())).Callback<Action>(Updates.Enqueue).Returns(true);
            var localization = new Mock<ILocalizationService>();
            localization.Setup(value => value.GetString(It.IsAny<string>())).Returns<string>(value => value);
            ViewModel = new(Memory.Object, ui.Object, localization.Object, Notifications.Object);
        }
        public void Change(CaptureMemoryState state) { State = state; Memory.Raise(value => value.StateChanged += null); }
        public void Dispatch() { while (Updates.TryDequeue(out Action? update)) update(); }
        public void Dispose() => ViewModel.Dispose();
    }
}
