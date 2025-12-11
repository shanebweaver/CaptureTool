using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Interfaces.FeatureManagement;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Domains.Edit.Interfaces;
using CaptureTool.Domains.Edit.Interfaces.ChromaKey;
using CaptureTool.Domains.Edit.Interfaces.Drawable;
using CaptureTool.Services.Interfaces.Cancellation;
using CaptureTool.Services.Interfaces.FeatureManagement;
using CaptureTool.Services.Interfaces.Share;
using CaptureTool.Services.Interfaces.Shutdown;
using CaptureTool.Services.Interfaces.Storage;
using CaptureTool.Services.Interfaces.Store;
using CaptureTool.Services.Interfaces.Telemetry;
using FluentAssertions;
using Moq;
using System.Drawing;

namespace CaptureTool.ViewModels.Tests;

[TestClass]
public sealed class ImageEditPageViewModelTests
{
    public required IFixture Fixture { get; set; }

    private ImageEditPageViewModel Create() => Fixture.Create<ImageEditPageViewModel>();

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture()
         .Customize(new AutoMoqCustomization { ConfigureMembers = true });

        Fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => Fixture.Behaviors.Remove(b));

        Fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        Fixture.Customize<ImageEditPageViewModel>(c => c.OmitAutoProperties());

        Fixture.Freeze<Mock<IStoreService>>();
        Fixture.Freeze<Mock<IShutdownHandler>>();
        Fixture.Freeze<Mock<ICancellationService>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
        Fixture.Freeze<Mock<IImageCanvasPrinter>>();
        Fixture.Freeze<Mock<IImageCanvasExporter>>();
        Fixture.Freeze<Mock<IFilePickerService>>();
        Fixture.Freeze<Mock<IChromaKeyService>>();
        Fixture.Freeze<Mock<IFeatureManager>>();
        Fixture.Freeze<Mock<IShareService>>();
    }

    // ------------------------------------------------------------------
    // TEST: LoadAsync — happy path
    // ------------------------------------------------------------------
    [TestMethod]
    public async Task LoadAsync_ShouldInitializeProperties_AndLogTelemetry()
    {
        // Arrange
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var filePicker = Fixture.Freeze<Mock<IFilePickerService>>();
        var cancel = Fixture.Freeze<Mock<ICancellationService>>();
        var chromaFeature = Fixture.Freeze<Mock<IFeatureManager>>();
        var storeService = Fixture.Freeze<Mock<IStoreService>>();
        var chromaService = Fixture.Freeze<Mock<IChromaKeyService>>();

        chromaFeature.Setup(f => f.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_ChromaKey))
                     .Returns(false);

        storeService.Setup(s => s.IsAddonPurchasedAsync(It.IsAny<string>()))
                    .ReturnsAsync(false);

        chromaService.Setup(s => s.GetTopColorsAsync(It.IsAny<ImageFile>(), It.IsAny<uint>(), It.IsAny<byte>()))
                     .ReturnsAsync([]);

        var cts = new CancellationTokenSource();
        cancel.Setup(c => c.GetLinkedCancellationTokenSource(cts.Token))
              .Returns(cts);

        var testFile = new ImageFile("test.png");
        filePicker.Setup(f => f.GetImageFileSize(testFile))
                  .Returns(new Size(100, 200));

        var vm = Create();

        // Act
        await vm.LoadAsync(testFile, cts.Token);

        // Assert
        vm.ImageFile.Should().Be(testFile);
        vm.ImageSize.Should().Be(new Size(100, 200));
        vm.CropRect.Should().Be(new Rectangle(0, 0, 100, 200));
        vm.Drawables.Should()
            .ContainSingle("only the image drawable should be present after load");

        telemetry.Verify(t => t.ActivityInitiated(ImageEditPageViewModel.ActivityIds.Load), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(ImageEditPageViewModel.ActivityIds.Load), Times.Once);
        telemetry.Verify(t => t.ActivityError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // TEST: ToggleCropModeCommand
    // ------------------------------------------------------------------
    [TestMethod]
    public void ToggleCropModeCommand_ShouldToggle_AndLogTelemetry()
    {
        // Arrange
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var vm = Create();

        // Act
        vm.ToggleCropModeCommand.Execute(null);

        // Assert
        vm.IsInCropMode.Should().BeTrue("Crop mode should be enabled after first toggle");

        telemetry.Verify(t => t.ActivityInitiated(ImageEditPageViewModel.ActivityIds.ToggleCropMode), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(ImageEditPageViewModel.ActivityIds.ToggleCropMode), Times.Once);
        telemetry.Verify(t => t.ActivityError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // TEST: CopyCommand
    // ------------------------------------------------------------------
    [TestMethod]
    public async Task CopyCommand_ShouldInvokeExporter_AndLogTelemetry()
    {
        // Arrange
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var exporter = Fixture.Freeze<Mock<IImageCanvasExporter>>();

        var vm = Create();
        vm.Drawables.Add(Fixture.Create<IDrawable>());

        // Act
        await vm.CopyCommand.ExecuteAsync(null);

        // Assert: exporter called
        exporter.Verify(e =>
            e.CopyImageToClipboardAsync(
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()),
            Times.Once);

        // Assert telemetry
        telemetry.Verify(t => t.ActivityInitiated(ImageEditPageViewModel.ActivityIds.Copy), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(ImageEditPageViewModel.ActivityIds.Copy), Times.Once);
        telemetry.Verify(t => t.ActivityError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // TEST: ShareCommand error path
    // ------------------------------------------------------------------
    [TestMethod]
    public async Task ShareCommand_ShouldLogError_WhenNoImageLoaded()
    {
        // Arrange
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var vm = Create();

        // Act
        await vm.ShareCommand.ExecuteAsync(null);

        // Assert telemetry
        telemetry.Verify(t => t.ActivityInitiated(ImageEditPageViewModel.ActivityIds.Share), Times.Once);

        telemetry.Verify(
            t => t.ActivityError(
                ImageEditPageViewModel.ActivityIds.Share,
                It.IsAny<InvalidOperationException>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<string?>()),
            Times.Once);

        telemetry.Verify(t => t.ActivityCompleted(ImageEditPageViewModel.ActivityIds.Share), Times.Never);
    }

    // ------------------------------------------------------------------
    // TEST: Undo — empty stack should raise error
    // ------------------------------------------------------------------
    [TestMethod]
    public void UndoCommand_ShouldLogError_WhenStackIsEmpty()
    {
        // Arrange
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var vm = Create();

        // Act
        vm.UndoCommand.Execute(null);

        // Assert telemetry
        telemetry.Verify(
            t => t.ActivityError(
                ImageEditPageViewModel.ActivityIds.Undo, 
                It.IsAny<InvalidOperationException>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    // ------------------------------------------------------------------
    // TEST: Rotate pushes undo + logs telemetry
    // ------------------------------------------------------------------
    [TestMethod]
    public void RotateCommand_ShouldPushUndo_AndLogTelemetry()
    {
        // Arrange
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var vm = Create();

        var oldOrientation = vm.Orientation;

        // Act
        vm.RotateCommand.Execute(null);

        // Assert
        vm.Orientation.Should().NotBe(oldOrientation, "Oriention should change after rotation");
        vm.HasUndoStack.Should().BeTrue("Undo stack should have an entry after rotation");
        vm.HasRedoStack.Should().BeFalse("Redo stack should be empty after new action");

        telemetry.Verify(t => t.ActivityInitiated(ImageEditPageViewModel.ActivityIds.Rotate), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(ImageEditPageViewModel.ActivityIds.Rotate), Times.Once);
        telemetry.Verify(t => t.ActivityError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }
}
