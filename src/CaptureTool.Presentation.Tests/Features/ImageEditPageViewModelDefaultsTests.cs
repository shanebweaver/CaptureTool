using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Features.ImageEdit.ChromaKey;
using CaptureTool.Application.Abstractions.Features.ImageEdit.Rendering;
using CaptureTool.Application.Abstractions.Features.Store;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Share;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Presentation.Features.ImageEdit;
using FluentAssertions;
using Moq;
using System.Drawing;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class ImageEditPageViewModelDefaultsTests
{
    [TestMethod]
    public async Task LoadAsync_ShouldScaleInitialShapeAndTextDefaults_ForLargeImages()
    {
        var filePicker = new Mock<IFilePickerService>();
        var cancellationService = new Mock<ICancellationService>();
        var featureAvailability = new Mock<IChromaKeyFeatureAvailability>();
        using var linkedCts = new CancellationTokenSource();
        var imageFile = new ImageFile("large.png");

        featureAvailability
            .Setup(service => service.IsChromaKeyEnabled)
            .Returns(false);

        filePicker
            .Setup(picker => picker.GetImageFileSize(imageFile))
            .Returns(new Size(8000, 4000));

        cancellationService
            .Setup(service => service.GetLinkedCancellationTokenSource(It.IsAny<CancellationToken>()))
            .Returns(linkedCts);

        var viewModel = CreateViewModel(filePicker.Object, cancellationService.Object, featureAvailability.Object);

        await viewModel.LoadAsync(imageFile, CancellationToken.None);

        viewModel.ShapeStrokeWidth.Should().Be(9);
        viewModel.TextFontSize.Should().Be(100);
    }

    [TestMethod]
    public void EditModeCommands_ShouldKeepModesMutuallyExclusive()
    {
        var viewModel = CreateViewModel();

        viewModel.ToggleTextModeCommand.Execute(null);
        viewModel.IsInTextMode.Should().BeTrue();
        viewModel.IsInCropMode.Should().BeFalse();
        viewModel.IsInShapesMode.Should().BeFalse();
        viewModel.ShowChromaKeyOptions.Should().BeFalse();

        viewModel.ToggleCropModeCommand.Execute(null);
        viewModel.IsInCropMode.Should().BeTrue();
        viewModel.IsInTextMode.Should().BeFalse();
        viewModel.IsInShapesMode.Should().BeFalse();
        viewModel.ShowChromaKeyOptions.Should().BeFalse();

        viewModel.ToggleShapesModeCommand.Execute(null);
        viewModel.IsInShapesMode.Should().BeTrue();
        viewModel.IsInCropMode.Should().BeFalse();
        viewModel.IsInTextMode.Should().BeFalse();
        viewModel.ShowChromaKeyOptions.Should().BeFalse();

        viewModel.UpdateShowChromaKeyOptionsCommand.Execute(true);
        viewModel.ShowChromaKeyOptions.Should().BeTrue();
        viewModel.IsInCropMode.Should().BeFalse();
        viewModel.IsInShapesMode.Should().BeFalse();
        viewModel.IsInTextMode.Should().BeFalse();
    }

    [TestMethod]
    public async Task ChromaKeyInteraction_ShouldUndoAndRedoAsSingleInteraction()
    {
        var filePicker = new Mock<IFilePickerService>();
        var cancellationService = new Mock<ICancellationService>();
        var featureAvailability = new Mock<IChromaKeyFeatureAvailability>();
        var chromaKeyService = new Mock<IChromaKeyService>();
        var storeService = new Mock<IStoreService>();
        using var linkedCts = new CancellationTokenSource();
        var imageFile = new ImageFile("green-screen.png");

        featureAvailability
            .Setup(service => service.IsChromaKeyEnabled)
            .Returns(true);

        filePicker
            .Setup(picker => picker.GetImageFileSize(imageFile))
            .Returns(new Size(100, 100));

        cancellationService
            .Setup(service => service.GetLinkedCancellationTokenSource(It.IsAny<CancellationToken>()))
            .Returns(linkedCts);

        chromaKeyService
            .Setup(service => service.GetTopColorsAsync(imageFile, It.IsAny<uint>(), It.IsAny<byte>()))
            .ReturnsAsync([Color.Green]);
        storeService
            .Setup(service => service.IsAddonPurchasedAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var viewModel = CreateViewModel(
            filePicker.Object,
            cancellationService.Object,
            featureAvailability.Object,
            chromaKeyService.Object,
            storeService.Object);

        await viewModel.LoadAsync(imageFile, CancellationToken.None);

        viewModel.BeginChromaKeyInteraction();
        viewModel.UpdateSelectedColorOptionIndexCommand.Execute(1);
        viewModel.UpdateToleranceCommand.Execute(45);
        viewModel.UpdateToleranceCommand.Execute(65);
        viewModel.UpdateDesaturationCommand.Execute(20);
        viewModel.CompleteChromaKeyInteraction();

        viewModel.HasUndoStack.Should().BeTrue();
        viewModel.HasRedoStack.Should().BeFalse();
        viewModel.SelectedChromaKeyColorOption.Should().Be(1);
        viewModel.ChromaKeyColor.Should().Be(Color.Green);
        viewModel.ChromaKeyTolerance.Should().Be(65);
        viewModel.ChromaKeyDesaturation.Should().Be(20);
        GetChromaKeyEffect(viewModel).IsEnabled.Should().BeTrue();

        viewModel.UndoCommand.Execute(null);

        viewModel.SelectedChromaKeyColorOption.Should().Be(0);
        viewModel.ChromaKeyColor.Should().Be(Color.Empty);
        viewModel.ChromaKeyTolerance.Should().Be(30);
        viewModel.ChromaKeyDesaturation.Should().Be(0);
        GetChromaKeyEffect(viewModel).IsEnabled.Should().BeFalse();

        viewModel.RedoCommand.Execute(null);

        viewModel.SelectedChromaKeyColorOption.Should().Be(1);
        viewModel.ChromaKeyColor.Should().Be(Color.Green);
        viewModel.ChromaKeyTolerance.Should().Be(65);
        viewModel.ChromaKeyDesaturation.Should().Be(20);
        GetChromaKeyEffect(viewModel).IsEnabled.Should().BeTrue();
    }

    private static ImageEditPageViewModel CreateViewModel(
        IFilePickerService? filePicker = null,
        ICancellationService? cancellationService = null,
        IChromaKeyFeatureAvailability? featureAvailability = null,
        IChromaKeyService? chromaKeyService = null,
        IStoreService? storeService = null)
    {
        return new ImageEditPageViewModel(
            Mock.Of<ILocalizationService>(),
            storeService ?? Mock.Of<IStoreService>(),
            cancellationService ?? Mock.Of<ICancellationService>(),
            Mock.Of<ITelemetryService>(),
            Mock.Of<IImageCanvasPrinter>(),
            Mock.Of<IImageCanvasExporter>(),
            filePicker ?? Mock.Of<IFilePickerService>(),
            chromaKeyService ?? Mock.Of<IChromaKeyService>(),
            featureAvailability ?? Mock.Of<IChromaKeyFeatureAvailability>(),
            Mock.Of<IShareService>());
    }

    private static ImageChromaKeyEffect GetChromaKeyEffect(ImageEditPageViewModel viewModel)
    {
        var image = viewModel.Drawables.OfType<ImageDrawable>().Single();
        return image.ImageEffect.Should().BeOfType<ImageChromaKeyEffect>().Subject;
    }
}
