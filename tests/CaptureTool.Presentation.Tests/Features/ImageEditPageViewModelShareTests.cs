using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Edit.Image;
using CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;
using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Share;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Features.ImageEdit;
using CaptureTool.Presentation.Notifications;
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
        var cancellationService = new Mock<ICancellationService>();
        var printer = Mock.Of<IImageCanvasPrinter>();
        var exporter = new Mock<IImageCanvasExporter>();
        var filePicker = new Mock<IFilePickerService>();
        var imageMetadata = new Mock<IImageMetadataService>();
        var chromaKeyService = Mock.Of<IChromaKeyService>();
        var chromaKeyAccess = new Mock<IChromaKeyAccessService>();
        var shareService = new Mock<IShareService>();
        var externalEditor = Mock.Of<IOpenExternalEditorUseCase>();
        var storage = Mock.Of<IStorageService>();

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

        chromaKeyAccess
            .Setup(service => service.IsChromaKeyEnabled)
            .Returns(false);

        imageMetadata
            .Setup(service => service.GetImageFileSize(imageFile))
            .Returns(new Size(100, 200));

        exporter
            .Setup(service => service.RenderToStreamAsync(
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .ReturnsAsync(renderedStream);

        var viewModel = new ImageEditPageViewModel(
            localization,
            cancellationService.Object,
            printer,
            exporter.Object,
            filePicker.Object,
            imageMetadata.Object,
            Mock.Of<IImageSuperResolutionService>(),
            Mock.Of<IImageSuperResolutionFeatureAvailability>(x => x.IsImageSuperResolutionEnabled == true),
            Mock.Of<IImageSuperResolutionPreparationConsentService>(),
            shareService.Object,
            externalEditor,
            storage,
            Mock.Of<ISettingsService>(),
            Mock.Of<ILogService>(),
            Mock.Of<IAppNotificationService>(),
            new ChromaKeyToolViewModel(chromaKeyAccess.Object, chromaKeyService),
            new ShapeToolViewModel(),
            new TextToolViewModel());

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

        shareService.Verify(service => service.ShareStreamAsync(renderedStream), Times.Once);
        shareService.Verify(service => service.ShareAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task EditInPaintCommand_ShouldRenderTempImage_AndOpenPaint()
    {
        var localization = Mock.Of<ILocalizationService>();
        var cancellationService = new Mock<ICancellationService>();
        var printer = Mock.Of<IImageCanvasPrinter>();
        var exporter = new Mock<IImageCanvasExporter>();
        var filePicker = new Mock<IFilePickerService>();
        var imageMetadata = new Mock<IImageMetadataService>();
        var chromaKeyService = Mock.Of<IChromaKeyService>();
        var chromaKeyAccess = new Mock<IChromaKeyAccessService>();
        var shareService = Mock.Of<IShareService>();
        var externalEditor = new Mock<IOpenExternalEditorUseCase>();
        var storage = new Mock<IStorageService>();

        using var linkedCts = new CancellationTokenSource();
        var imageFile = new ImageFile("test.png");
        string tempFolder = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString());

        cancellationService
            .Setup(service => service.GetLinkedCancellationTokenSource(It.IsAny<CancellationToken>()))
            .Returns(linkedCts);

        chromaKeyAccess
            .Setup(service => service.IsChromaKeyEnabled)
            .Returns(false);

        imageMetadata
            .Setup(service => service.GetImageFileSize(imageFile))
            .Returns(new Size(100, 200));

        storage
            .Setup(service => service.GetApplicationTemporaryFolderPath())
            .Returns(tempFolder);
        storage
            .Setup(service => service.GetTemporaryFileName())
            .Returns("paint-export.tmp");

        var viewModel = new ImageEditPageViewModel(
            localization,
            cancellationService.Object,
            printer,
            exporter.Object,
            filePicker.Object,
            imageMetadata.Object,
            Mock.Of<IImageSuperResolutionService>(),
            Mock.Of<IImageSuperResolutionFeatureAvailability>(x => x.IsImageSuperResolutionEnabled == true),
            Mock.Of<IImageSuperResolutionPreparationConsentService>(),
            shareService,
            externalEditor.Object,
            storage.Object,
            Mock.Of<ISettingsService>(),
            Mock.Of<ILogService>(),
            Mock.Of<IAppNotificationService>(),
            new ChromaKeyToolViewModel(chromaKeyAccess.Object, chromaKeyService),
            new ShapeToolViewModel(),
            new TextToolViewModel());

        await viewModel.LoadAsync(imageFile, CancellationToken.None);
        await viewModel.EditInPaintCommand.ExecuteAsync(null);

        string expectedPath = Path.Combine(tempFolder, "paint-export.png");
        exporter.Verify(service =>
            service.SaveImageAsync(
                expectedPath,
                It.IsAny<IDrawable[]>(),
                It.Is<ImageCanvasRenderOptions>(options =>
                    options.CanvasSize == new Size(100, 200) &&
                    options.CropRect == new Rectangle(0, 0, 100, 200))),
            Times.Once);

        externalEditor.Verify(service =>
            service.ExecuteAsync(
                It.Is<OpenExternalEditorRequest>(request =>
                    request.MediaPath == expectedPath &&
                    request.Editor == ExternalMediaEditor.Paint),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
