using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.ImageEdit.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Features.Windowing.ShowMainWindow;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Features.SelectionOverlay;
using CaptureTool.Presentation.Features.SelectionOverlay.Factories;
using Moq;
using System.Drawing;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class SelectionOverlayWindowViewModelTests
{
    [TestMethod]
    public void Load_WithImageCapture_ShouldDefaultToRectangle()
    {
        SelectionOverlayWindowViewModel viewModel = CreateViewModel();

        viewModel.Load(CreateOptions(CaptureOptions.ImageDefault));

        Assert.AreEqual(CaptureMode.Image, viewModel.GetSelectedCaptureMode());
        Assert.AreEqual(CaptureType.Rectangle, viewModel.GetSelectedCaptureType());
        Assert.IsTrue(viewModel.UsesCrosshairCursor);
    }

    [TestMethod]
    public void Load_WithVideoCapture_ShouldDefaultToFullScreen()
    {
        SelectionOverlayWindowViewModel viewModel = CreateViewModel();

        viewModel.Load(CreateOptions(CaptureOptions.VideoDefault));

        Assert.AreEqual(CaptureMode.Video, viewModel.GetSelectedCaptureMode());
        Assert.AreEqual(CaptureType.FullScreen, viewModel.GetSelectedCaptureType());
        Assert.IsFalse(viewModel.UsesCrosshairCursor);
    }

    [TestMethod]
    public void UpdateSelectedCaptureModeCommand_WhenSwitchingCaptureModes_ShouldUseModeDefaults()
    {
        SelectionOverlayWindowViewModel viewModel = CreateViewModel();
        viewModel.Load(CreateOptions(CaptureOptions.ImageDefault));

        viewModel.UpdateSelectedCaptureModeCommand.Execute((1, SelectionUpdateSource.UserInteraction));

        Assert.AreEqual(CaptureMode.Video, viewModel.GetSelectedCaptureMode());
        Assert.AreEqual(CaptureType.FullScreen, viewModel.GetSelectedCaptureType());
        Assert.IsFalse(viewModel.UsesCrosshairCursor);

        viewModel.UpdateSelectedCaptureModeCommand.Execute((0, SelectionUpdateSource.UserInteraction));

        Assert.AreEqual(CaptureMode.Image, viewModel.GetSelectedCaptureMode());
        Assert.AreEqual(CaptureType.Rectangle, viewModel.GetSelectedCaptureType());
        Assert.IsTrue(viewModel.UsesCrosshairCursor);
    }

    private static SelectionOverlayWindowViewModel CreateViewModel()
    {
        Mock<ILocalizationService> localizationService = new();
        localizationService
            .Setup(service => service.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        Mock<IThemeService> themeService = new();
        themeService.Setup(service => service.DefaultTheme).Returns(AppTheme.Light);
        themeService.Setup(service => service.CurrentTheme).Returns(AppTheme.Light);

        return new SelectionOverlayWindowViewModel(
            Mock.Of<IOpenImageEditPageUseCase>(),
            Mock.Of<IOpenCaptureOverlayUseCase>(),
            Mock.Of<IShowMainWindowUseCase>(),
            Mock.Of<ITelemetryService>(),
            themeService.Object,
            Mock.Of<IShutdownHandler>(),
            Mock.Of<IImageCaptureHandler>(),
            new CaptureModeViewModelFactory(localizationService.Object),
            new CaptureTypeViewModelFactory(localizationService.Object));
    }

    private static SelectionOverlayWindowOptions CreateOptions(CaptureOptions captureOptions)
    {
        MonitorCaptureResult monitor = new(
            IntPtr.Zero,
            [],
            96,
            new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080),
            true);

        return new SelectionOverlayWindowOptions(monitor, [], captureOptions);
    }
}
