using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Presentation.Features.Settings;
using CaptureTool.Presentation.Notifications;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class TextExtractionConsentViewModelTests
{
    [TestMethod]
    public async Task SettingsUsesStandaloneConsentAndTracksExternalChangesAndFailedRevocation()
    {
        var state = AiFeatureConsentState.Unknown;
        var consent = new Mock<ITextExtractionConsentService>();
        consent.SetupGet(x => x.State).Returns(() => state);
        var availability = new Mock<ITextExtractionFeatureAvailability>();
        availability.SetupGet(x => x.IsTextExtractionEnabled).Returns(true);
        var ui = new Mock<ITaskEnvironment>();
        ui.Setup(x => x.TryExecute(It.IsAny<Action>())).Callback<Action>(action => action()).Returns(true);
        var localization = new Mock<ILocalizationService>();
        localization.Setup(x => x.GetString(It.IsAny<string>())).Returns<string>(key => key);
        var notifications = new Mock<IAppNotificationService>();
        using var vm = new TextExtractionConsentViewModel(consent.Object, availability.Object, ui.Object, localization.Object, notifications.Object);
        Assert.IsTrue(vm.IsVisible);
        Assert.IsFalse(vm.ConsentGranted);
        await vm.SetConsentCommand.ExecuteAsync(true);
        consent.Verify(x => x.EnsureConsentAsync(default), Times.Once);
        Assert.IsFalse(vm.ConsentGranted, "Declining the prompt leaves the checkbox off.");
        state = AiFeatureConsentState.Granted;
        consent.Raise(x => x.StateChanged += null);
        Assert.IsTrue(vm.ConsentGranted);
        availability.SetupGet(x => x.IsTextExtractionEnabled).Returns(false);
        vm.Refresh();
        Assert.IsTrue(vm.IsVisible, "An existing grant can be revoked even when the tool is unavailable.");
        consent.Setup(x => x.RevokeConsentAsync(default)).ReturnsAsync(() => { state = AiFeatureConsentState.Denied; return false; });
        await vm.SetConsentCommand.ExecuteAsync(false);
        Assert.IsFalse(vm.ConsentGranted);
        Assert.IsFalse(vm.IsVisible);
        notifications.Verify(x => x.ShowError("TextExtractionSettings_SaveFailed"), Times.Once);
    }
}
