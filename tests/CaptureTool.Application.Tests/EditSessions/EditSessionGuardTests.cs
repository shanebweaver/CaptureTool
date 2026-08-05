using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.EditSessions;
using Moq;

namespace CaptureTool.Application.Tests.EditSessions;

[TestClass]
public sealed class EditSessionGuardTests
{
    [TestMethod]
    public async Task CanLeaveCurrentSessionAsync_ReturnsTrue_WhenNoSessionIsActive()
    {
        var guard = CreateGuard();

        Assert.IsTrue(await guard.CanLeaveCurrentSessionAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CanLeaveCurrentSessionAsync_ReturnsFalse_WhenUserCancels()
    {
        var session = new Mock<IEditableSession>();
        session.SetupGet(s => s.HasUnsavedChanges).Returns(true);

        var confirmation = new Mock<IEditSessionConfirmationService>();
        confirmation
            .Setup(service => service.ConfirmLeaveAsync(session.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EditSessionLeaveDecision.Cancel);

        var active = new ActiveEditSessionService();
        active.SetCurrentSession(session.Object);
        var guard = CreateGuard(active, confirmation.Object);

        Assert.IsFalse(await guard.CanLeaveCurrentSessionAsync(TestContext.CancellationToken));
        session.Verify(s => s.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task CanLeaveCurrentSessionAsync_SavesAsBeforeLeaving_WhenUserChoosesSaveAs()
    {
        var session = new Mock<IEditableSession>();
        session.SetupGet(s => s.HasUnsavedChanges).Returns(true);
        session
            .Setup(s => s.SaveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var confirmation = new Mock<IEditSessionConfirmationService>();
        confirmation
            .Setup(service => service.ConfirmLeaveAsync(session.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EditSessionLeaveDecision.SaveAs);

        var active = new ActiveEditSessionService();
        active.SetCurrentSession(session.Object);
        var guard = CreateGuard(active, confirmation.Object);

        Assert.IsTrue(await guard.CanLeaveCurrentSessionAsync(TestContext.CancellationToken));
        session.Verify(s => s.SaveAsync(TestContext.CancellationToken), Times.Once);
    }

    [TestMethod]
    public async Task CanLeaveCurrentSessionAsync_ReturnsFalse_WhenSaveAsFails()
    {
        var session = new Mock<IEditableSession>();
        session.SetupGet(s => s.HasUnsavedChanges).Returns(true);
        session
            .Setup(s => s.SaveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var confirmation = new Mock<IEditSessionConfirmationService>();
        confirmation
            .Setup(service => service.ConfirmLeaveAsync(session.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EditSessionLeaveDecision.SaveAs);

        var active = new ActiveEditSessionService();
        active.SetCurrentSession(session.Object);
        var guard = CreateGuard(active, confirmation.Object);

        Assert.IsFalse(await guard.CanLeaveCurrentSessionAsync(TestContext.CancellationToken));
        session.Verify(s => s.SaveAsync(TestContext.CancellationToken), Times.Once);
    }

    [TestMethod]
    public async Task CanLeaveCurrentSessionAsync_SavesToSource_WhenSupportedAndUserChoosesSave()
    {
        var session = new Mock<ISourceSaveableSession>();
        session.SetupGet(s => s.HasUnsavedChanges).Returns(true);
        session
            .Setup(s => s.SaveToSourceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var confirmation = new Mock<IEditSessionConfirmationService>();
        confirmation
            .Setup(service => service.ConfirmLeaveAsync(session.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EditSessionLeaveDecision.SaveToSource);

        var active = new ActiveEditSessionService();
        active.SetCurrentSession(session.Object);
        var guard = CreateGuard(active, confirmation.Object);

        Assert.IsTrue(await guard.CanLeaveCurrentSessionAsync(TestContext.CancellationToken));
        session.Verify(
            value => value.SaveToSourceAsync(TestContext.CancellationToken),
            Times.Once);
        session.Verify(
            value => value.SaveAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task CanLeaveCurrentSessionAsync_SkipsPrompt_WhenWarningsAreDisabled()
    {
        var session = new Mock<IEditableSession>();
        session.SetupGet(s => s.HasUnsavedChanges).Returns(true);

        var active = new ActiveEditSessionService();
        active.SetCurrentSession(session.Object);

        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_Edit_WarnBeforeDiscard))
            .Returns(false);

        var confirmation = new Mock<IEditSessionConfirmationService>();
        var guard = CreateGuard(active, confirmation.Object, settings.Object);

        Assert.IsTrue(await guard.CanLeaveCurrentSessionAsync(TestContext.CancellationToken));
        confirmation.Verify(service => service.ConfirmLeaveAsync(It.IsAny<IEditableSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static EditSessionGuard CreateGuard(
        IActiveEditSessionService? activeEditSessionService = null,
        IEditSessionConfirmationService? confirmationService = null,
        ISettingsService? settingsService = null)
    {
        var settings = settingsService ?? Mock.Of<ISettingsService>(
            service => service.Get(CaptureToolSettings.Settings_Edit_WarnBeforeDiscard) == true);

        return new(
            activeEditSessionService ?? new ActiveEditSessionService(),
            confirmationService ?? Mock.Of<IEditSessionConfirmationService>(),
            settings);
    }

    public TestContext TestContext { get; set; } = null!;
}
