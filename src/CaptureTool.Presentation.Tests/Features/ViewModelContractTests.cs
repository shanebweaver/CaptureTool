using CaptureTool.Application.Features.About.LeaveAboutPage;
using CaptureTool.Application.Features.AudioCapture.OpenAudioCapturePage;
using CaptureTool.Application.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Navigation;
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
        var goBack = new LeaveAboutPageUseCase(Mock.Of<INavigationService>());
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
        var navigation = new Mock<INavigationService>();
        var openSelectionOverlay = new OpenSelectionOverlayUseCase(navigation.Object);
        var openAudioCapturePage = new OpenAudioCapturePageUseCase(Mock.Of<INavigationService>());
        var featureManager = new Mock<IFeatureManager>();

        var viewModel = new HomePageViewModel(openSelectionOverlay, openAudioCapturePage, featureManager.Object);

        viewModel.NewImageCaptureCommand.Execute(null);

        await Task.Yield();

        navigation.Verify(
            service => service.Navigate(
                NavigationRoute.SelectionOverlay,
                It.Is<CaptureOptions>(options => options.CaptureMode == CaptureMode.Image),
                false),
            Times.Once);
    }
}
