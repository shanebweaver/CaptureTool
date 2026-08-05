using CaptureTool.Application.Abstractions.Capture.Image.CaptureImage;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenCaptureOverlay;
using CaptureTool.Application.Abstractions.Edit.Image.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Abstractions.Windowing.ShowMainWindow;
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

    [TestMethod]
    public async Task CloseOverlayCommand_WhenMainWindowExists_ShouldReturnToIt()
    {
        var showMainWindow = new Mock<IShowMainWindowUseCase>();
        showMainWindow
            .Setup(useCase => useCase.ExecuteAsync(
                It.Is<ShowMainWindowRequest>(request => !request.CreateIfUnavailable),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<ShowMainWindowResponse>.Success(new ShowMainWindowResponse()));
        var shutdownHandler = new Mock<IShutdownHandler>();
        SelectionOverlayWindowViewModel viewModel = CreateViewModel(showMainWindow, shutdownHandler);

        await viewModel.CloseOverlayCommand.ExecuteAsync(null);

        showMainWindow.VerifyAll();
        shutdownHandler.Verify(handler => handler.Shutdown(), Times.Never);
    }

    [TestMethod]
    public async Task CloseOverlayCommand_WhenNoMainWindowExists_ShouldShutDown()
    {
        var showMainWindow = new Mock<IShowMainWindowUseCase>();
        showMainWindow
            .Setup(useCase => useCase.ExecuteAsync(
                It.Is<ShowMainWindowRequest>(request => !request.CreateIfUnavailable),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<ShowMainWindowResponse>.Success(new ShowMainWindowResponse(false)));
        var shutdownHandler = new Mock<IShutdownHandler>();
        SelectionOverlayWindowViewModel viewModel = CreateViewModel(showMainWindow, shutdownHandler);

        await viewModel.CloseOverlayCommand.ExecuteAsync(null);

        showMainWindow.VerifyAll();
        shutdownHandler.Verify(handler => handler.Shutdown(), Times.Once);
    }

    [TestMethod]
    public async Task RequestCaptureCommand_WindowSelection_UsesExplicitWindowHandle()
    {
        var openCaptureOverlay = new Mock<IOpenCaptureOverlayUseCase>();
        OpenCaptureOverlayRequest? capturedRequest = null;
        openCaptureOverlay
            .Setup(useCase => useCase.ExecuteAsync(
                It.IsAny<OpenCaptureOverlayRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<OpenCaptureOverlayRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(UseCaseResponse<OpenCaptureOverlayResponse>.Success(new OpenCaptureOverlayResponse()));
        Rectangle duplicateArea = new(20, 30, 400, 300);
        WindowInfo[] windows =
        [
            new WindowInfo(111, "first", duplicateArea),
            new WindowInfo(222, "second", duplicateArea),
        ];
        SelectionOverlayWindowViewModel viewModel = CreateViewModel(openCaptureOverlay: openCaptureOverlay);
        viewModel.Load(CreateOptions(CaptureOptions.VideoDefault, windows));
        viewModel.UpdateSelectedCaptureTypeCommand.Execute((1, SelectionUpdateSource.UserInteraction));
        viewModel.UpdateSelectionCommand.Execute(new SelectionOverlaySelection(duplicateArea, 222));

        await viewModel.RequestCaptureCommand.ExecuteAsync(null);

        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(CaptureType.Window, capturedRequest.CaptureArgs.CaptureType);
        Assert.AreEqual(duplicateArea, capturedRequest.CaptureArgs.Area);
        Assert.AreEqual((nint)222, capturedRequest.CaptureArgs.WindowHandle);
    }

    [TestMethod]
    public async Task RequestCaptureCommand_NonWindowSelection_DoesNotReuseWindowHandle()
    {
        var openCaptureOverlay = new Mock<IOpenCaptureOverlayUseCase>();
        OpenCaptureOverlayRequest? capturedRequest = null;
        openCaptureOverlay
            .Setup(useCase => useCase.ExecuteAsync(
                It.IsAny<OpenCaptureOverlayRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<OpenCaptureOverlayRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(UseCaseResponse<OpenCaptureOverlayResponse>.Success(new OpenCaptureOverlayResponse()));
        SelectionOverlayWindowViewModel viewModel = CreateViewModel(openCaptureOverlay: openCaptureOverlay);
        viewModel.Load(CreateOptions(CaptureOptions.VideoDefault));
        viewModel.UpdateSelectionCommand.Execute(new SelectionOverlaySelection(new Rectangle(20, 30, 400, 300), 222));

        await viewModel.RequestCaptureCommand.ExecuteAsync(null);

        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(CaptureType.FullScreen, capturedRequest.CaptureArgs.CaptureType);
        Assert.AreEqual(nint.Zero, capturedRequest.CaptureArgs.WindowHandle);
    }

    [TestMethod]
    public void UpdateSelectionCommand_ClearingOrReplacingSelection_ClearsWindowHandle()
    {
        SelectionOverlayWindowViewModel viewModel = CreateViewModel();
        viewModel.Load(CreateOptions(CaptureOptions.VideoDefault));
        viewModel.UpdateSelectionCommand.Execute(new SelectionOverlaySelection(new Rectangle(20, 30, 400, 300), 222));

        viewModel.UpdateSelectionCommand.Execute(SelectionOverlaySelection.Empty);

        Assert.AreEqual(Rectangle.Empty, viewModel.CaptureArea);
        Assert.AreEqual(nint.Zero, viewModel.SelectedWindowHandle);

        viewModel.UpdateSelectionCommand.Execute(new SelectionOverlaySelection(new Rectangle(40, 50, 500, 400)));

        Assert.AreEqual(new Rectangle(40, 50, 500, 400), viewModel.CaptureArea);
        Assert.AreEqual(nint.Zero, viewModel.SelectedWindowHandle);
    }

    private static SelectionOverlayWindowViewModel CreateViewModel(
        Mock<IShowMainWindowUseCase>? showMainWindow = null,
        Mock<IShutdownHandler>? shutdownHandler = null,
        Mock<IOpenCaptureOverlayUseCase>? openCaptureOverlay = null)
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
            openCaptureOverlay?.Object ?? Mock.Of<IOpenCaptureOverlayUseCase>(),
            showMainWindow?.Object ?? Mock.Of<IShowMainWindowUseCase>(),
            Mock.Of<ICaptureImageUseCase>(),
            themeService.Object,
            shutdownHandler?.Object ?? Mock.Of<IShutdownHandler>(),
            new CaptureModeViewModelFactory(localizationService.Object),
            new CaptureTypeViewModelFactory(localizationService.Object));
    }

    private static SelectionOverlayWindowOptions CreateOptions(
        CaptureOptions captureOptions,
        IEnumerable<WindowInfo>? windows = null)
    {
        MonitorCaptureResult monitor = new(
            IntPtr.Zero,
            [],
            96,
            new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080),
            true);

        return new SelectionOverlayWindowOptions(monitor, windows ?? [], captureOptions);
    }
}
