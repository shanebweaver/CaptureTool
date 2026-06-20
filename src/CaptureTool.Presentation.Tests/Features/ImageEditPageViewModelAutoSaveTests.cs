using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Features.ImageEdit.ChromaKey;
using CaptureTool.Application.Abstractions.Features.ImageEdit.Rendering;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Share;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Features.Settings;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Presentation.Features.ImageEdit;
using Moq;
using System.Drawing;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class ImageEditPageViewModelAutoSaveTests
{
    [TestMethod]
    public async Task AutoSaveAsync_ShouldSaveLoadedImageAndAutoSavedOutputCopy_WhenOutputCopyIsKnown()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var settings = new Mock<ISettingsService>();
        var imageFile = new ImageFile("temp.png");
        imageFile.MarkAutoSavedAs("output.png");

        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_Edit_AutoSave))
            .Returns(true);

        ImageEditPageViewModel viewModel = await CreateLoadedViewModelAsync(
            imageFile,
            exporter: exporter.Object,
            settings: settings.Object);

        await viewModel.AutoSaveAsync(CancellationToken.None);

        exporter.Verify(service => service.SaveImageAsync(
                "temp.png",
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()),
            Times.Once);
        exporter.Verify(service => service.SaveImageAsync(
                "output.png",
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()),
            Times.Once);
    }

    [TestMethod]
    public async Task AutoSaveAsync_ShouldOnlySaveLoadedImage_WhenOutputCopyIsUnknown()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var settings = new Mock<ISettingsService>();
        var imageFile = new ImageFile("temp.png");

        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_Edit_AutoSave))
            .Returns(true);

        ImageEditPageViewModel viewModel = await CreateLoadedViewModelAsync(
            imageFile,
            exporter: exporter.Object,
            settings: settings.Object);

        await viewModel.AutoSaveAsync(CancellationToken.None);

        exporter.Verify(service => service.SaveImageAsync(
                "temp.png",
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()),
            Times.Once);
        exporter.Verify(service => service.SaveImageAsync(
                It.Is<string>(path => path != "temp.png"),
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()),
            Times.Never);
    }

    [TestMethod]
    public async Task SaveAsync_ShouldSavePickedFileAndAutoSavedOutputCopy_WhenOutputCopyIsKnown()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var filePicker = new Mock<IFilePickerService>();
        var pickedFile = new Mock<IFile>();
        var imageFile = new ImageFile("temp.png");
        imageFile.MarkAutoSavedAs("output.png");

        pickedFile
            .Setup(file => file.FilePath)
            .Returns("picked.png");
        filePicker
            .Setup(service => service.PickSaveFileAsync(FilePickerType.Image, UserFolder.Pictures))
            .ReturnsAsync(pickedFile.Object);

        ImageEditPageViewModel viewModel = await CreateLoadedViewModelAsync(
            imageFile,
            exporter: exporter.Object,
            filePicker: filePicker.Object);

        await viewModel.SaveAsync(CancellationToken.None);

        exporter.Verify(service => service.SaveImageAsync(
                "picked.png",
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()),
            Times.Once);
        exporter.Verify(service => service.SaveImageAsync(
                "output.png",
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SaveAsync_ShouldSucceed_WhenAutoSavedOutputCopyCannotBeUpdated()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var filePicker = new Mock<IFilePickerService>();
        var pickedFile = new Mock<IFile>();
        var imageFile = new ImageFile("temp.png");
        imageFile.MarkAutoSavedAs("output.png");

        pickedFile
            .Setup(file => file.FilePath)
            .Returns("picked.png");
        filePicker
            .Setup(service => service.PickSaveFileAsync(FilePickerType.Image, UserFolder.Pictures))
            .ReturnsAsync(pickedFile.Object);
        exporter
            .Setup(service => service.SaveImageAsync(
                "output.png",
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .ThrowsAsync(new IOException("Output copy is unavailable."));

        ImageEditPageViewModel viewModel = await CreateLoadedViewModelAsync(
            imageFile,
            exporter: exporter.Object,
            filePicker: filePicker.Object);

        bool saved = await viewModel.SaveAsync(CancellationToken.None);

        Assert.IsTrue(saved);
        exporter.Verify(service => service.SaveImageAsync(
                "picked.png",
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()),
            Times.Once);
    }

    private static async Task<ImageEditPageViewModel> CreateLoadedViewModelAsync(
        ImageFile imageFile,
        IImageCanvasExporter? exporter = null,
        IFilePickerService? filePicker = null,
        ISettingsService? settings = null)
    {
        var cancellationService = new Mock<ICancellationService>();
        var featureAvailability = new Mock<IChromaKeyFeatureAvailability>();
        var filePickerMock = Mock.Get(filePicker ?? Mock.Of<IFilePickerService>());
        using var linkedCts = new CancellationTokenSource();

        cancellationService
            .Setup(service => service.GetLinkedCancellationTokenSource(It.IsAny<CancellationToken>()))
            .Returns(linkedCts);
        featureAvailability
            .Setup(service => service.IsChromaKeyEnabled)
            .Returns(false);
        filePickerMock
            .Setup(service => service.GetImageFileSize(imageFile))
            .Returns(new Size(100, 100));

        var viewModel = new ImageEditPageViewModel(
            Mock.Of<ILocalizationService>(),
            cancellationService.Object,
            Mock.Of<IImageCanvasPrinter>(),
            exporter ?? Mock.Of<IImageCanvasExporter>(),
            filePickerMock.Object,
            Mock.Of<IShareService>(),
            settings ?? Mock.Of<ISettingsService>(),
            Mock.Of<ILogService>(),
            new ChromaKeyToolViewModel(
                Mock.Of<IStoreService>(),
                Mock.Of<IChromaKeyService>(),
                featureAvailability.Object),
            new ShapeToolViewModel(),
            new TextToolViewModel());

        await viewModel.LoadAsync(imageFile, CancellationToken.None);
        return viewModel;
    }
}
