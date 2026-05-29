using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.About.LeaveAboutPage;
using CaptureTool.Application.Features.AudioCapture.OpenAudioCapturePage;
using CaptureTool.Application.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.Abstractions.Localization;
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
        var goBack = Mock.Of<IUseCase<LeaveAboutPageRequest, LeaveAboutPageResponse>>();
        var localization = new Mock<ILocalizationService>();
        localization.Setup(service => service.GetString("About_ThirdParty_DialogTitle")).Returns("Third-party");
        localization.Setup(service => service.GetString("About_ThirdParty_DialogContent")).Returns("Notices");

        var viewModel = new AboutPageViewModel(goBack, localization.Object);

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
        var openSelectionOverlay = new Mock<IUseCase<OpenSelectionOverlayRequest, OpenSelectionOverlayResponse>>();
        openSelectionOverlay
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<OpenSelectionOverlayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenSelectionOverlayResponse());

        var openAudioCapturePage = Mock.Of<IUseCase<OpenAudioCapturePageRequest, OpenAudioCapturePageResponse>>();
        var featureManager = new Mock<IFeatureManager>();

        var viewModel = new HomePageViewModel(openSelectionOverlay.Object, openAudioCapturePage, featureManager.Object);

        viewModel.NewImageCaptureCommand.Execute(null);

        await Task.Yield();

        openSelectionOverlay.Verify(
            useCase => useCase.ExecuteAsync(
                It.Is<OpenSelectionOverlayRequest>(request => request.CaptureOptions.CaptureMode == CaptureMode.Image),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
