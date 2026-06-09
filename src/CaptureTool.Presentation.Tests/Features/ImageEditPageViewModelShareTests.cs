using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Features;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Features.ImageEdit.ChromaKey;
using CaptureTool.Application.Abstractions.Features.ImageEdit.Rendering;
using CaptureTool.Application.Abstractions.Share;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Windowing;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Edit;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Presentation.Features.ImageEdit;
using Moq;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class ImageEditPageViewModelShareTests
{
    [TestMethod]
    public async Task ShareCommand_ShouldRenderDrawables_AndInvokeShareService()
    {
        var localization = Mock.Of<ILocalizationService>();
        var storeService = Mock.Of<IStoreService>();
        var windowingService = new Mock<IWindowHandleProvider>();
        var cancellationService = new Mock<ICancellationService>();
        var telemetry = Mock.Of<ITelemetryService>();
        var printer = Mock.Of<IImageCanvasPrinter>();
        var exporter = new Mock<IImageCanvasExporter>();
        var filePicker = new Mock<IFilePickerService>();
        var chromaKeyService = Mock.Of<IChromaKeyService>();
        var featureAvailability = new Mock<IFeatureAvailabilityService>();
        var shareService = new Mock<IShareService>();

        const nint hwnd = 123;
        using var renderedStream = new MemoryStream([1, 2, 3]);
        using var linkedCts = new CancellationTokenSource();
        var imageFile = new ImageFile("test.png");
        var shape = new RectangleDrawable(
            new Vector2(10, 10),
            new Size(50, 50),
            Color.Red,
            Color.Transparent,
            2);

        cancellationService
            .Setup(service => service.GetLinkedCancellationTokenSource(It.IsAny<CancellationToken>()))
            .Returns(linkedCts);

        featureAvailability
            .Setup(service => service.IsImageEditChromaKeyEnabled)
            .Returns(false);

        filePicker
            .Setup(picker => picker.GetImageFileSize(imageFile))
            .Returns(new Size(100, 200));

        windowingService
            .Setup(service => service.GetMainWindowHandle())
            .Returns(hwnd);

        exporter
            .Setup(service => service.RenderToStreamAsync(
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .ReturnsAsync(renderedStream);

        var viewModel = new ImageEditPageViewModel(
            localization,
            storeService,
            windowingService.Object,
            cancellationService.Object,
            telemetry,
            printer,
            exporter.Object,
            filePicker.Object,
            chromaKeyService,
            featureAvailability.Object,
            shareService.Object);

        await viewModel.LoadAsync(imageFile, CancellationToken.None);
        viewModel.AddDrawable(shape);

        await viewModel.ShareCommand.ExecuteAsync(null);

        exporter.Verify(service =>
            service.RenderToStreamAsync(
                It.Is<IDrawable[]>(drawables => drawables.Contains(shape)),
                It.Is<ImageCanvasRenderOptions>(options =>
                    options.CanvasSize == new Size(100, 200) &&
                    options.CropRect == new Rectangle(0, 0, 100, 200))),
            Times.Once);

        shareService.Verify(service => service.ShareStreamAsync(renderedStream, hwnd), Times.Once);
        shareService.Verify(service => service.ShareAsync(It.IsAny<string>(), It.IsAny<nint>()), Times.Never);
    }
}
