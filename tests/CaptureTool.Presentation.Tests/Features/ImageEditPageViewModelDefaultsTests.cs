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
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.Edit.Operations;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Features.ImageEdit;
using CaptureTool.Presentation.Notifications;
using FluentAssertions;
using Moq;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class ImageEditPageViewModelDefaultsTests
{
    [TestMethod]
    public async Task LoadAsync_ShouldScaleInitialShapeAndTextDefaults_ForLargeImages()
    {
        var imageMetadata = new Mock<IImageMetadataService>();
        var cancellationService = new Mock<ICancellationService>();
        var chromaKeyAccess = new Mock<IChromaKeyAccessService>();
        using var linkedCts = new CancellationTokenSource();
        var imageFile = new ImageFile("large.png");

        chromaKeyAccess
            .Setup(service => service.IsChromaKeyEnabled)
            .Returns(false);

        imageMetadata
            .Setup(service => service.GetImageFileSize(imageFile))
            .Returns(new Size(8000, 4000));

        cancellationService
            .Setup(service => service.GetLinkedCancellationTokenSource(It.IsAny<CancellationToken>()))
            .Returns(linkedCts);

        var viewModel = CreateViewModel(
            imageMetadata: imageMetadata.Object,
            cancellationService: cancellationService.Object,
            chromaKeyAccess: chromaKeyAccess.Object);

        await viewModel.LoadAsync(imageFile, CancellationToken.None);

        viewModel.ShapeTool.ShapeStrokeWidth.Should().Be(9);
        viewModel.TextTool.TextFontSize.Should().Be(100);
    }

    [TestMethod]
    public void EditModeCommands_ShouldKeepModesMutuallyExclusive()
    {
        var viewModel = CreateViewModel();

        viewModel.ToggleTextModeCommand.Execute(null);
        viewModel.IsTextModeActive.Should().BeTrue();
        viewModel.IsCropModeActive.Should().BeFalse();
        viewModel.IsShapesModeActive.Should().BeFalse();
        viewModel.IsChromaKeyModeActive.Should().BeFalse();
        viewModel.IsColorPickerModeActive.Should().BeFalse();

        viewModel.ToggleCropModeCommand.Execute(null);
        viewModel.IsCropModeActive.Should().BeTrue();
        viewModel.IsTextModeActive.Should().BeFalse();
        viewModel.IsShapesModeActive.Should().BeFalse();
        viewModel.IsChromaKeyModeActive.Should().BeFalse();
        viewModel.IsColorPickerModeActive.Should().BeFalse();

        viewModel.ToggleShapesModeCommand.Execute(null);
        viewModel.IsShapesModeActive.Should().BeTrue();
        viewModel.IsCropModeActive.Should().BeFalse();
        viewModel.IsTextModeActive.Should().BeFalse();
        viewModel.IsChromaKeyModeActive.Should().BeFalse();
        viewModel.IsColorPickerModeActive.Should().BeFalse();

        viewModel.SetChromaKeyModeActiveCommand.Execute(true);
        viewModel.IsChromaKeyModeActive.Should().BeTrue();
        viewModel.IsCropModeActive.Should().BeFalse();
        viewModel.IsShapesModeActive.Should().BeFalse();
        viewModel.IsTextModeActive.Should().BeFalse();
        viewModel.IsColorPickerModeActive.Should().BeFalse();

        viewModel.ToggleColorPickerModeCommand.Execute(null);
        viewModel.IsColorPickerModeActive.Should().BeTrue();
        viewModel.IsChromaKeyModeActive.Should().BeFalse();
        viewModel.IsCropModeActive.Should().BeFalse();
        viewModel.IsShapesModeActive.Should().BeFalse();
        viewModel.IsTextModeActive.Should().BeFalse();

        viewModel.ToggleTextModeCommand.Execute(null);
        viewModel.IsTextModeActive.Should().BeTrue();
        viewModel.IsColorPickerModeActive.Should().BeFalse();
    }

    [TestMethod]
    public void EditModeCommands_ShouldIgnoreInactiveModeDeactivation()
    {
        var viewModel = CreateViewModel();

        viewModel.SetChromaKeyModeActiveCommand.Execute(true);
        viewModel.ToggleShapesModeCommand.Execute(null);
        viewModel.SetChromaKeyModeActiveCommand.Execute(false);

        viewModel.IsShapesModeActive.Should().BeTrue();
        viewModel.IsChromaKeyModeActive.Should().BeFalse();
    }

    [TestMethod]
    public void OnShapeDrawn_ShouldRequestRedrawWithoutInvalidatingCanvasViewport()
    {
        var viewModel = CreateViewModel();
        int redrawRequests = 0;
        int invalidationRequests = 0;
        viewModel.RedrawCanvasRequested += (_, _) => redrawRequests++;
        viewModel.InvalidateCanvasRequested += (_, _) => invalidationRequests++;
        viewModel.ToggleShapesModeCommand.Execute(null);

        viewModel.OnShapeDrawn(new Vector2(10, 20), new Vector2(110, 120));

        viewModel.Drawables.Should().ContainSingle()
            .Which.Should().BeOfType<RectangleDrawable>();
        redrawRequests.Should().Be(1);
        invalidationRequests.Should().Be(0);
    }

    [TestMethod]
    public void OnTextBoxDrawn_ShouldRequestRedrawWithoutInvalidatingCanvasViewport()
    {
        var viewModel = CreateViewModel();
        int redrawRequests = 0;
        int invalidationRequests = 0;
        viewModel.RedrawCanvasRequested += (_, _) => redrawRequests++;
        viewModel.InvalidateCanvasRequested += (_, _) => invalidationRequests++;
        viewModel.ToggleTextModeCommand.Execute(null);

        viewModel.OnTextBoxDrawn(new Vector2(10, 20), new Vector2(110, 120));

        viewModel.Drawables.Should().ContainSingle()
            .Which.Should().BeOfType<TextDrawable>();
        redrawRequests.Should().Be(1);
        invalidationRequests.Should().Be(0);
    }

    [TestMethod]
    public void ShapeModificationDeletionAndHistory_ShouldPreserveCanvasViewport()
    {
        var viewModel = CreateViewModel();
        viewModel.ToggleShapesModeCommand.Execute(null);
        viewModel.OnShapeDrawn(new Vector2(10, 20), new Vector2(110, 120));
        var shape = viewModel.Drawables.Should().ContainSingle()
            .Which.Should().BeOfType<RectangleDrawable>().Subject;
        var originalState = new ShapeState(shape);
        shape.Offset = new Vector2(30, 40);
        var modifiedState = new ShapeState(shape);
        int redrawRequests = 0;
        int invalidationRequests = 0;
        viewModel.RedrawCanvasRequested += (_, _) => redrawRequests++;
        viewModel.InvalidateCanvasRequested += (_, _) => invalidationRequests++;

        viewModel.OnShapeModified(0, originalState, modifiedState);
        viewModel.OnShapeDeleted(0);
        viewModel.UndoCommand.Execute(null);
        viewModel.UndoCommand.Execute(null);
        shape.Offset.Should().Be(originalState.Offset);
        viewModel.RedoCommand.Execute(null);
        shape.Offset.Should().Be(modifiedState.Offset);
        viewModel.RedoCommand.Execute(null);

        viewModel.Drawables.Should().BeEmpty();
        redrawRequests.Should().Be(6);
        invalidationRequests.Should().Be(0);
    }

    [TestMethod]
    public void CanvasGeometryEditsAndHistory_ShouldInvalidateCanvasLayout()
    {
        var viewModel = CreateViewModel();
        int redrawRequests = 0;
        int invalidationRequests = 0;
        viewModel.RedrawCanvasRequested += (_, _) => redrawRequests++;
        viewModel.InvalidateCanvasRequested += (_, _) => invalidationRequests++;

        viewModel.RotateCommand.Execute(null);
        viewModel.UndoCommand.Execute(null);
        viewModel.RedoCommand.Execute(null);

        redrawRequests.Should().Be(0);
        invalidationRequests.Should().Be(3);
    }

    [TestMethod]
    public async Task OpenScreenshotsFolderCommand_ShouldOpenScreenshotsFolder()
    {
        var openScreenshotsFolder = new Mock<IOpenScreenshotsFolderUseCase>();
        openScreenshotsFolder
            .Setup(service => service.ExecuteAsync(It.IsAny<OpenScreenshotsFolderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<OpenScreenshotsFolderResponse>.Success(new OpenScreenshotsFolderResponse()));
        var viewModel = CreateViewModel(openScreenshotsFolderAction: openScreenshotsFolder.Object);

        await viewModel.OpenScreenshotsFolderCommand.ExecuteAsync(null);

        openScreenshotsFolder.Verify(
            service => service.ExecuteAsync(It.IsAny<OpenScreenshotsFolderRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ChromaKeyInteraction_ShouldUndoAndRedoAsSingleInteraction()
    {
        var imageMetadata = new Mock<IImageMetadataService>();
        var cancellationService = new Mock<ICancellationService>();
        var chromaKeyAccess = new Mock<IChromaKeyAccessService>();
        var chromaKeyService = new Mock<IChromaKeyService>();
        using var linkedCts = new CancellationTokenSource();
        var imageFile = new ImageFile("green-screen.png");

        chromaKeyAccess
            .Setup(service => service.IsChromaKeyEnabled)
            .Returns(true);
        chromaKeyAccess
            .Setup(service => service.IsChromaKeyAddOnOwnedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        imageMetadata
            .Setup(service => service.GetImageFileSize(imageFile))
            .Returns(new Size(100, 100));

        cancellationService
            .Setup(service => service.GetLinkedCancellationTokenSource(It.IsAny<CancellationToken>()))
            .Returns(linkedCts);

        chromaKeyService
            .Setup(service => service.GetTopColorsAsync(imageFile, It.IsAny<uint>(), It.IsAny<byte>()))
            .ReturnsAsync([Color.Green]);

        var viewModel = CreateViewModel(
            imageMetadata: imageMetadata.Object,
            cancellationService: cancellationService.Object,
            chromaKeyAccess: chromaKeyAccess.Object,
            chromaKeyService: chromaKeyService.Object);

        await viewModel.LoadAsync(imageFile, CancellationToken.None);

        viewModel.ChromaKeyTool.BeginInteraction();
        viewModel.ChromaKeyTool.UpdateSelectedColorOptionIndexCommand.Execute(1);
        viewModel.ChromaKeyTool.UpdateToleranceCommand.Execute(45);
        viewModel.ChromaKeyTool.UpdateToleranceCommand.Execute(65);
        viewModel.ChromaKeyTool.UpdateDesaturationCommand.Execute(20);
        viewModel.ChromaKeyTool.CompleteInteraction();

        viewModel.HasUndoStack.Should().BeTrue();
        viewModel.HasRedoStack.Should().BeFalse();
        viewModel.ChromaKeyTool.SelectedChromaKeyColorOption.Should().Be(1);
        viewModel.ChromaKeyTool.ChromaKeyColor.Should().Be(Color.Green);
        viewModel.ChromaKeyTool.ChromaKeyTolerance.Should().Be(65);
        viewModel.ChromaKeyTool.ChromaKeyDesaturation.Should().Be(20);
        GetChromaKeyEffect(viewModel).IsEnabled.Should().BeTrue();

        viewModel.UndoCommand.Execute(null);

        viewModel.ChromaKeyTool.SelectedChromaKeyColorOption.Should().Be(0);
        viewModel.ChromaKeyTool.ChromaKeyColor.Should().Be(Color.Empty);
        viewModel.ChromaKeyTool.ChromaKeyTolerance.Should().Be(30);
        viewModel.ChromaKeyTool.ChromaKeyDesaturation.Should().Be(0);
        GetChromaKeyEffect(viewModel).IsEnabled.Should().BeFalse();

        viewModel.RedoCommand.Execute(null);

        viewModel.ChromaKeyTool.SelectedChromaKeyColorOption.Should().Be(1);
        viewModel.ChromaKeyTool.ChromaKeyColor.Should().Be(Color.Green);
        viewModel.ChromaKeyTool.ChromaKeyTolerance.Should().Be(65);
        viewModel.ChromaKeyTool.ChromaKeyDesaturation.Should().Be(20);
        GetChromaKeyEffect(viewModel).IsEnabled.Should().BeTrue();
    }

    private static ImageEditPageViewModel CreateViewModel(
        IFilePickerService? filePicker = null,
        IImageMetadataService? imageMetadata = null,
        ICancellationService? cancellationService = null,
        IChromaKeyAccessService? chromaKeyAccess = null,
        IChromaKeyService? chromaKeyService = null,
        IOpenScreenshotsFolderUseCase? openScreenshotsFolderAction = null)
    {
        IAppNotificationService notifications = Mock.Of<IAppNotificationService>();

        return new ImageEditPageViewModel(
            Mock.Of<ILocalizationService>(),
            cancellationService ?? Mock.Of<ICancellationService>(),
            Mock.Of<IImageCanvasPrinter>(),
            Mock.Of<IImageCanvasExporter>(),
            filePicker ?? Mock.Of<IFilePickerService>(),
            imageMetadata ?? Mock.Of<IImageMetadataService>(),
            Mock.Of<IImageSuperResolutionService>(),
            Mock.Of<IImageSuperResolutionFeatureAvailability>(x => x.IsImageSuperResolutionEnabled == true),
            Mock.Of<IImageSuperResolutionPreparationConsentService>(),
            Mock.Of<IShareService>(),
            Mock.Of<IOpenExternalEditorUseCase>(),
            Mock.Of<IStorageService>(),
            Mock.Of<ISettingsService>(),
            openScreenshotsFolderAction ?? Mock.Of<IOpenScreenshotsFolderUseCase>(),
            Mock.Of<ILogService>(),
            notifications,
            Mock.Of<IClipboardService>(),
            new ColorPickerToolViewModel(
                Mock.Of<IClipboardService>(),
                Mock.Of<ILocalizationService>(),
                notifications),
            new ChromaKeyToolViewModel(
                chromaKeyAccess ?? Mock.Of<IChromaKeyAccessService>(),
                chromaKeyService ?? Mock.Of<IChromaKeyService>()),
            new ShapeToolViewModel(),
            new TextToolViewModel(),
            new TextExtractionToolViewModel(
                Mock.Of<IClipboardService>(),
                Mock.Of<ILocalizationService>(),
                notifications));
    }

    private static ImageChromaKeyEffect GetChromaKeyEffect(ImageEditPageViewModel viewModel)
    {
        var image = viewModel.Drawables.OfType<ImageDrawable>().Single();
        return image.ImageEffect.Should().BeOfType<ImageChromaKeyEffect>().Subject;
    }
}
