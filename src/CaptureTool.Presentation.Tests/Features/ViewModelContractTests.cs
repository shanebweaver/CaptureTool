using CaptureTool.Application.Abstractions.Features.About.LeaveAboutPage;
using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Features.About;
using CaptureTool.Presentation.Features.Home;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class ViewModelContractTests
{
    [TestMethod]
    public void AboutPageViewModel_ShouldRaiseDialogRequest_WithLocalizedContent()
    {
        var goBack = Mock.Of<ILeaveAboutPageUseCase>();
        var localization = new Mock<ILocalizationService>();
        localization.Setup(service => service.GetString("About_ThirdParty_DialogTitle")).Returns("Third-party");
        localization.Setup(service => service.GetString("About_ThirdParty_DialogContent")).Returns("Notices");

        var viewModel = new AboutPageViewModel(goBack, localization.Object, Mock.Of<ITelemetryService>());

        (string title, string content)? dialog = null;
        viewModel.ShowDialogRequested += (_, args) => dialog = args;

        viewModel.ShowThirdPartyCommand.Execute(null);

        Assert.IsNotNull(dialog);
        Assert.AreEqual("Third-party", dialog.Value.title);
        Assert.AreEqual("Notices", dialog.Value.content);
    }

    [TestMethod]
    public async Task HomePageViewModel_NewImageCaptureCommand_ShouldExecuteSelectionOverlayUseCase()
    {
        var openSelectionOverlay = new Mock<IOpenSelectionOverlayUseCase>();
        var openAudioCapturePage = Mock.Of<IOpenAudioCapturePageUseCase>();
        var featureAvailability = new Mock<IAudioCaptureFeatureAvailability>();

        var viewModel = new HomePageViewModel(openSelectionOverlay.Object, openAudioCapturePage, featureAvailability.Object, Mock.Of<ITelemetryService>());

        viewModel.NewImageCaptureCommand.Execute(null);

        await Task.Yield();

        openSelectionOverlay.Verify(
            useCase => useCase.ExecuteAsync(
                It.Is<OpenSelectionOverlayRequest>(request => request.CaptureOptions.CaptureMode == CaptureMode.Image),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
