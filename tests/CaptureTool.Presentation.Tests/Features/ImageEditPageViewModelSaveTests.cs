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
using CaptureTool.Domain.Edit;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Features.ImageEdit;
using CaptureTool.Presentation.Notifications;
using FluentAssertions;
using Moq;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class ImageEditPageViewModelSaveTests
{
    [TestMethod]
    public async Task SaveToSourceAsync_WithPersistentSource_DoesNotModifyWorkingFile()
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
        savedPaths.Should().Equal("original.png");
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
        exporter.Verify(
            service => service.SaveImageAsync(
                "working.png",
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()),
            Times.Never);
    }

    [TestMethod]
    public async Task SaveToSourceAsync_WithPersistentSource_PreservesLiveEditSession()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        exporter
            .Setup(service => service.SaveImageAsync(
                It.IsAny<string>(),
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .Returns(Task.CompletedTask);
        var imageFile = new ImageFile("working.png", "original.png");
        ImageEditPageViewModel viewModel = await CreateLoadedViewModelAsync(imageFile, exporter.Object);
        viewModel.RotateCommand.Execute(null);
        var annotation = new RectangleDrawable(
            Vector2.One,
            new Size(10, 10),
            Color.Red,
            Color.Transparent,
            2);
        viewModel.AddDrawable(annotation);

        bool saved = await viewModel.SaveToSourceAsync();

        saved.Should().BeTrue();
        viewModel.Drawables.Should().ContainInOrder(viewModel.Drawables.OfType<ImageDrawable>().Single(), annotation);
        viewModel.Orientation.Should().Be(ImageOrientation.Rotate90FlipNone);
        viewModel.HasUndoStack.Should().BeTrue();
        viewModel.HasUnsavedChanges.Should().BeFalse();
    }

    [TestMethod]
    public async Task SaveToSourceAsync_WithWorkingSource_RebasesFlattenedImageBeforeFurtherEdits()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        List<(string Path, int DrawableCount, ImageCanvasRenderOptions Options)> saves = [];
        exporter
            .Setup(service => service.SaveImageAsync(
                It.IsAny<string>(),
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .Callback<string, IDrawable[], ImageCanvasRenderOptions>(
                (path, drawables, options) => saves.Add((path, drawables.Length, options)))
            .Returns(Task.CompletedTask);
        var imageFile = new ImageFile("working.png");
        ImageEditPageViewModel viewModel = await CreateLoadedViewModelAsync(imageFile, exporter.Object);
        viewModel.RotateCommand.Execute(null);
        viewModel.AddDrawable(new RectangleDrawable(
            Vector2.One,
            new Size(10, 10),
            Color.Red,
            Color.Transparent,
            2));
        int reloadRequests = 0;
        viewModel.ReloadCanvasResourcesRequested += (_, _) => reloadRequests++;

        bool saved = await viewModel.SaveToSourceAsync();

        saved.Should().BeTrue();
        saves.Should().ContainSingle();
        saves[0].Path.Should().Be("working.png");
        saves[0].DrawableCount.Should().Be(2);
        viewModel.Drawables.Should().ContainSingle().Which.Should().BeOfType<ImageDrawable>();
        viewModel.Orientation.Should().Be(ImageOrientation.RotateNoneFlipNone);
        viewModel.ImageSize.Should().Be(new Size(50, 100));
        viewModel.CropRect.Should().Be(new Rectangle(Point.Empty, new Size(50, 100)));
        viewModel.HasUndoStack.Should().BeFalse();
        viewModel.HasRedoStack.Should().BeFalse();
        viewModel.HasUnsavedChanges.Should().BeFalse();
        reloadRequests.Should().Be(1);

        viewModel.AddDrawable(new RectangleDrawable(
            Vector2.One,
            new Size(5, 5),
            Color.Blue,
            Color.Transparent,
            1));

        viewModel.Drawables.Should().HaveCount(2);
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
