using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Edit.Image;
using CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;
using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Abstractions.Share;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Features.ImageEdit;
using CaptureTool.Presentation.Notifications;
using FluentAssertions;
using Moq;
using System.Drawing;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class ImageEditPageViewModelSaveTests
{
    [TestMethod]
    public async Task SaveToSourceAsync_SavesWorkingAndPersistentFiles()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var picker = new Mock<IFilePickerService>();
        List<string> savedPaths = [];
        exporter
            .Setup(service => service.SaveImageAsync(
                It.IsAny<string>(),
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .Callback<string, IDrawable[], ImageCanvasRenderOptions>((path, _, _) => savedPaths.Add(path))
            .Returns(Task.CompletedTask);
        var imageFile = new ImageFile("working.png", "original.png");
        ImageEditPageViewModel viewModel = await CreateLoadedViewModelAsync(imageFile, exporter.Object, picker.Object);
        viewModel.RotateCommand.Execute(null);

        bool saved = await viewModel.SaveToSourceAsync();

        saved.Should().BeTrue();
        savedPaths.Should().Equal("working.png", "original.png");
        viewModel.HasUnsavedChanges.Should().BeFalse();
        picker.Verify(
            service => service.PickSaveFileAsync(It.IsAny<FilePickerType>(), It.IsAny<UserFolder>()),
            Times.Never);
    }

    [TestMethod]
    public async Task SaveAsync_SaveAsOnlyWritesPickedFile()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var picker = new Mock<IFilePickerService>();
        List<string> savedPaths = [];
        exporter
            .Setup(service => service.SaveImageAsync(
                It.IsAny<string>(),
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .Callback<string, IDrawable[], ImageCanvasRenderOptions>((path, _, _) => savedPaths.Add(path))
            .Returns(Task.CompletedTask);
        picker
            .Setup(service => service.PickSaveFileAsync(FilePickerType.Image, UserFolder.Pictures))
            .ReturnsAsync(new FileReference("save-as.png"));
        var imageFile = new ImageFile("working.png", "original.png");
        ImageEditPageViewModel viewModel = await CreateLoadedViewModelAsync(imageFile, exporter.Object, picker.Object);
        viewModel.RotateCommand.Execute(null);

        bool saved = await viewModel.SaveAsync();

        saved.Should().BeTrue();
        savedPaths.Should().Equal("save-as.png");
        viewModel.HasUnsavedChanges.Should().BeFalse();
    }

    [TestMethod]
    public async Task SaveToSourceAsync_WhenPersistentSaveFails_KeepsUnsavedChanges()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        exporter
            .Setup(service => service.SaveImageAsync(
                "working.png",
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .Returns(Task.CompletedTask);
        exporter
            .Setup(service => service.SaveImageAsync(
                "original.png",
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .ThrowsAsync(new IOException("Persistent file is unavailable."));
        var imageFile = new ImageFile("working.png", "original.png");
        ImageEditPageViewModel viewModel = await CreateLoadedViewModelAsync(imageFile, exporter.Object);
        viewModel.RotateCommand.Execute(null);

        bool saved = await viewModel.SaveToSourceAsync();

        saved.Should().BeFalse();
        viewModel.HasUnsavedChanges.Should().BeTrue();
    }

    private static async Task<ImageEditPageViewModel> CreateLoadedViewModelAsync(
        ImageFile imageFile,
        IImageCanvasExporter exporter,
        IFilePickerService? filePicker = null)
    {
        var cancellationService = new Mock<ICancellationService>();
        cancellationService
            .Setup(service => service.GetLinkedCancellationTokenSource(It.IsAny<CancellationToken>()))
            .Returns(() => new CancellationTokenSource());
        var imageMetadata = new Mock<IImageMetadataService>();
        imageMetadata
            .Setup(service => service.GetImageFileSize(imageFile))
            .Returns(new Size(100, 50));
        IAppNotificationService notifications = Mock.Of<IAppNotificationService>();

        var viewModel = new ImageEditPageViewModel(
            Mock.Of<ILocalizationService>(),
            cancellationService.Object,
            Mock.Of<IImageCanvasPrinter>(),
            exporter,
            filePicker ?? Mock.Of<IFilePickerService>(),
            imageMetadata.Object,
            Mock.Of<IImageSuperResolutionService>(),
            Mock.Of<IImageSuperResolutionFeatureAvailability>(),
            Mock.Of<IImageSuperResolutionPreparationConsentService>(),
            Mock.Of<IShareService>(),
            Mock.Of<IOpenExternalEditorUseCase>(),
            Mock.Of<IStorageService>(),
            Mock.Of<ISettingsService>(),
            Mock.Of<IOpenScreenshotsFolderUseCase>(),
            Mock.Of<ILogService>(),
            notifications,
            Mock.Of<IClipboardService>(),
            new ColorPickerToolViewModel(
                Mock.Of<IClipboardService>(),
                Mock.Of<ILocalizationService>(),
                notifications),
            new ChromaKeyToolViewModel(
                Mock.Of<IChromaKeyAccessService>(),
                Mock.Of<IChromaKeyService>()),
            new ShapeToolViewModel(),
            new TextToolViewModel(),
            new TextExtractionToolViewModel(
                Mock.Of<IClipboardService>(),
                Mock.Of<ILocalizationService>(),
                notifications));

        await viewModel.LoadAsync(imageFile, CancellationToken.None);
        return viewModel;
    }
}
