using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.EditSessions;
using CaptureTool.Application.Features.Settings;
using Moq;

namespace CaptureTool.Application.Tests.Features;

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
    public async Task CanLeaveCurrentSessionAsync_SavesBeforeLeaving_WhenUserChoosesSave()
    {
        var session = new Mock<IEditableSession>();
        session.SetupGet(s => s.HasUnsavedChanges).Returns(true);
        session
            .Setup(s => s.SaveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var confirmation = new Mock<IEditSessionConfirmationService>();
        confirmation
            .Setup(service => service.ConfirmLeaveAsync(session.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EditSessionLeaveDecision.Save);

        var active = new ActiveEditSessionService();
        active.SetCurrentSession(session.Object);
        var guard = CreateGuard(active, confirmation.Object);

        Assert.IsTrue(await guard.CanLeaveCurrentSessionAsync(TestContext.CancellationToken));
        session.Verify(s => s.SaveAsync(TestContext.CancellationToken), Times.Once);
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
