using CaptureTool.Application.Implementations.ViewModels;
using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Interfaces.Actions.About;
using CaptureTool.Infrastructure.Interfaces.Localization;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using Moq;
using System.Windows.Input;

namespace CaptureTool.Application.Tests.ViewModels;

[TestClass]
public class AboutPageViewModelTests
{
    public required IFixture Fixture { get; set; }

    private AboutPageViewModel Create() => Fixture.Create<AboutPageViewModel>();

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture()
            .Customize(new AutoMoqCustomization { ConfigureMembers = true });

        Fixture.Freeze<Mock<IAboutGoBackAction>>();
        Fixture.Freeze<Mock<ILocalizationService>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
    }

    // ---------------------------------------------------------
    // Generic dialog test method
    // ---------------------------------------------------------
    private async Task TestDialogCommandAsync(
        Func<AboutPageViewModel, ICommand> getCommand,
        string expectedActivityId,
        string titleResourceKey,
        string contentResourceKey)
    {
        // Arrange
        const string title = "TITLE!";
        const string content = "CONTENT!";

        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var _ = telemetryService.Object;

        var localizationService = Fixture.Freeze<Mock<ILocalizationService>>();
        localizationService.Setup(l => l.GetString(titleResourceKey)).Returns(title);
        localizationService.Setup(l => l.GetString(contentResourceKey)).Returns(content);

        var vm = Create();

        TaskCompletionSource<(string title, string content)> tcs = new();
        vm.ShowDialogRequested += (object? s, (string t, string c) e) =>
        {
            tcs.SetResult((e.t, e.c));
        };

        // Act
        var command = getCommand(vm);
        command.Execute(null);

        // Assert telemetry
        var result = await tcs.Task;
        string capturedTitle = result.title;
        string capturedContent = result.content;

        telemetryService.Verify(t => t.ActivityInitiated(expectedActivityId, It.IsAny<string>()), Times.Exactly(1));
        telemetryService.Verify(t => t.ActivityCompleted(expectedActivityId, It.IsAny<string>()), Times.Exactly(1));
        telemetryService.Verify(t => t.ActivityError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);

        // Assert localization calls
        localizationService.Verify(l => l.GetString(titleResourceKey), Times.Once);
        localizationService.Verify(l => l.GetString(contentResourceKey), Times.Once);

        // Assert dialog raised
        Assert.AreEqual(title, capturedTitle);
        Assert.AreEqual(content, capturedContent);
    }

    // -------------------------------------------------------------------
    // Individual command tests
    // -------------------------------------------------------------------

    [TestMethod]
    public async Task ShowThirdPartyCommand_ShouldShowDialog_AndTrackTelemetry()
    {
        await TestDialogCommandAsync(
            (vm) => vm.ShowThirdPartyCommand,
            AboutPageViewModel.ActivityIds.ShowThirdParty,
            "About_ThirdParty_DialogTitle",
            "About_ThirdParty_DialogContent");
    }

    [TestMethod]
    public async Task ShowPrivacyPolicyCommand_ShouldShowDialog_AndTrackTelemetry()
    {
        await TestDialogCommandAsync(
            (vm) => vm.ShowPrivacyPolicyCommand,
            AboutPageViewModel.ActivityIds.ShowPrivacyPolicy,
            "About_PrivacyPolicy_DialogTitle",
            "About_PrivacyPolicy_DialogContent");
    }

    [TestMethod]
    public async Task ShowTermsOfUseCommand_ShouldShowDialog_AndTrackTelemetry()
    {
        await TestDialogCommandAsync(
            (vm) => vm.ShowTermsOfUseCommand,
            AboutPageViewModel.ActivityIds.ShowTermsOfUse,
            "About_TermsOfUse_DialogTitle",
            "About_TermsOfUse_DialogContent");
    }

    [TestMethod]
    public async Task ShowDisclaimerOfLiabilityCommand_ShouldShowDialog_AndTrackTelemetry()
    {
        await TestDialogCommandAsync(
            (vm) => vm.ShowDisclaimerOfLiabilityCommand,
            AboutPageViewModel.ActivityIds.ShowDisclaimerOfLiability,
            "About_DisclaimerOfLiability_DialogTitle",
            "About_DisclaimerOfLiability_DialogContent");
    }

    // ---------------------------------------------------------
    // Exception path
    // ---------------------------------------------------------

    [TestMethod]
    public void ShowDialog_ShouldLogError_WhenLocalizationThrows()
    {
        // Arrange
        const string titleKey = "About_ThirdParty_DialogTitle";

        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var _ = telemetryService.Object;

        var localizationService = Fixture.Freeze<Mock<ILocalizationService>>();
        localizationService.Setup(l => l.GetString(titleKey))
            .Throws(new InvalidOperationException("oops"));

        var vm = Fixture.Create<AboutPageViewModel>();

        // Act
        vm.ShowThirdPartyCommand.Execute(null);

        // Assert
        telemetryService.Verify(
            t => t.ActivityError(
                AboutPageViewModel.ActivityIds.ShowThirdParty,
                It.IsAny<Exception>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    // ---------------------------------------------------------
    // GoBack
    // ---------------------------------------------------------

    [TestMethod]
    public void GoBackCommand_ShouldInvokeAction_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var goBackAction = Fixture.Freeze<Mock<IAboutGoBackAction>>();
        var vm = Create();

        // Act
        vm.GoBackCommand.Execute(null);

        // Assert
        goBackAction.Verify(a => a.Execute(), Times.Once);
        telemetryService.Verify(t => t.ActivityInitiated(AboutPageViewModel.ActivityIds.GoBack, It.IsAny<string>()), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(AboutPageViewModel.ActivityIds.GoBack, It.IsAny<string>()), Times.Once);
    }
}