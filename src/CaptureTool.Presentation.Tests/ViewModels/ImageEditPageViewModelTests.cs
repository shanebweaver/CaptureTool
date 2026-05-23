using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.ViewModels;
using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Domain.Edit.Interfaces;
using CaptureTool.Domain.Edit.Interfaces.ChromaKey;
using CaptureTool.Domain.Edit.Interfaces.Drawable;
using CaptureTool.Infrastructure.Interfaces.Cancellation;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using CaptureTool.Infrastructure.Interfaces.Share;
using CaptureTool.Infrastructure.Interfaces.Shutdown;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.Store;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using FluentAssertions;
using Moq;
using System.Drawing;

namespace CaptureTool.Application.Tests.ViewModels;

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

        telemetry.Verify(t => t.ActivityInitiated(ImageEditPageViewModel.ActivityIds.Load, It.IsAny<string>()), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(ImageEditPageViewModel.ActivityIds.Load, It.IsAny<string>()), Times.Once);
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
        vm.ToggleCropModeCommand.Execute();

        // Assert
        vm.IsInCropMode.Should().BeTrue("Crop mode should be enabled after first toggle");

        telemetry.Verify(t => t.ActivityInitiated(ImageEditPageViewModel.ActivityIds.ToggleCropMode, It.IsAny<string>()), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(ImageEditPageViewModel.ActivityIds.ToggleCropMode, It.IsAny<string>()), Times.Once);
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

        // Act
        await vm.CopyCommand.ExecuteAsync();

        // Assert: exporter called
        exporter.Verify(e =>
            e.CopyImageToClipboardAsync(
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()),
            Times.Once);

        // Assert telemetry
        telemetry.Verify(t => t.ActivityInitiated(ImageEditPageViewModel.ActivityIds.Copy, It.IsAny<string>()), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(ImageEditPageViewModel.ActivityIds.Copy, It.IsAny<string>()), Times.Once);
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
        await vm.ShareCommand.ExecuteAsync();

        // Assert telemetry
        telemetry.Verify(t => t.ActivityInitiated(ImageEditPageViewModel.ActivityIds.Share, It.IsAny<string>()), Times.Once);

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

        telemetry.Verify(t => t.ActivityCompleted(ImageEditPageViewModel.ActivityIds.Share, It.IsAny<string>()), Times.Never);
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
        vm.UndoCommand.Execute();

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
        vm.RotateCommand.Execute();

        // Assert
        vm.Orientation.Should().NotBe(oldOrientation, "Oriention should change after rotation");
        vm.HasUndoStack.Should().BeTrue("Undo stack should have an entry after rotation");
        vm.HasRedoStack.Should().BeFalse("Redo stack should be empty after new action");

        telemetry.Verify(t => t.ActivityInitiated(ImageEditPageViewModel.ActivityIds.Rotate, It.IsAny<string>()), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(ImageEditPageViewModel.ActivityIds.Rotate, It.IsAny<string>()), Times.Once);
        telemetry.Verify(t => t.ActivityError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // TEST: OnShapeDeleted — should ignore when shapes mode is off
    // ------------------------------------------------------------------
    [TestMethod]
    public void OnShapeDeleted_ShouldIgnore_WhenShapesModeIsOff()
    {
        // Arrange
        var featureManager = Fixture.Freeze<Mock<IFeatureManager>>();
        featureManager.Setup(f => f.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_Shapes))
                     .Returns(true);

        var vm = Create();
        
        // Add a drawable to delete using the AddDrawable method
        var drawable = new RectangleDrawable(
            new System.Numerics.Vector2(10, 10),
            new Size(50, 50),
            Color.Red,
            Color.Transparent,
            2);
        vm.AddDrawable(drawable);
        
        // Ensure shapes mode is off (even though feature is enabled)
        // This simulates when user is not in shapes mode
        
        int initialCount = vm.Drawables.Count;

        // Act
        vm.OnShapeDeleted(0);

        // Assert
        vm.Drawables.Count.Should().Be(initialCount, "Drawable should not be removed when not in shapes mode");
    }

    // ------------------------------------------------------------------
    // TEST: OnShapeDeleted — should ignore when feature is disabled
    // ------------------------------------------------------------------
    [TestMethod]
    public void OnShapeDeleted_ShouldIgnore_WhenFeatureIsDisabled()
    {
        // Arrange
        var featureManager = Fixture.Freeze<Mock<IFeatureManager>>();
        featureManager.Setup(f => f.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_Shapes))
                     .Returns(false);

        var vm = Create();
        
        // Toggle shapes mode on
        vm.ToggleShapesModeCommand.Execute();
        
        // Add a drawable to delete using the AddDrawable method
        var drawable = new RectangleDrawable(
            new System.Numerics.Vector2(10, 10),
            new Size(50, 50),
            Color.Red,
            Color.Transparent,
            2);
        vm.AddDrawable(drawable);
        
        int initialCount = vm.Drawables.Count;

        // Act
        vm.OnShapeDeleted(0);

        // Assert
        vm.Drawables.Count.Should().Be(initialCount, "Drawable should not be removed when feature is disabled");
    }

    // ------------------------------------------------------------------
    // TEST: OnShapeDeleted — should remove drawable and invalidate canvas
    // ------------------------------------------------------------------
    [TestMethod]
    public void OnShapeDeleted_ShouldRemoveDrawable_AndInvalidateCanvas()
    {
        // Arrange
        var featureManager = Fixture.Freeze<Mock<IFeatureManager>>();
        featureManager.Setup(f => f.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_Shapes))
                     .Returns(true);

        var vm = Create();
        
        // Toggle shapes mode on
        vm.ToggleShapesModeCommand.Execute();
        
        // Add drawables using the AddDrawable method
        var drawable1 = new RectangleDrawable(
            new System.Numerics.Vector2(10, 10),
            new Size(50, 50),
            Color.Red,
            Color.Transparent,
            2);
        var drawable2 = new EllipseDrawable(
            new System.Numerics.Vector2(100, 100),
            new Size(30, 30),
            Color.Blue,
            Color.Transparent,
            2);
        vm.AddDrawable(drawable1);
        vm.AddDrawable(drawable2);
        
        bool invalidateRequested = false;
        vm.InvalidateCanvasRequested += (s, e) => invalidateRequested = true;

        // Act
        vm.OnShapeDeleted(0);

        // Assert
        vm.Drawables.Count.Should().Be(1, "One drawable should be removed");
        vm.Drawables[0].Should().Be(drawable2, "The second drawable should remain");
        invalidateRequested.Should().BeTrue("InvalidateCanvasRequested event should be raised");
    }

    // ------------------------------------------------------------------
    // TEST: OnShapeDeleted — should handle invalid index
    // ------------------------------------------------------------------
    [TestMethod]
    public void OnShapeDeleted_ShouldHandleInvalidIndex()
    {
        // Arrange
        var featureManager = Fixture.Freeze<Mock<IFeatureManager>>();
        featureManager.Setup(f => f.IsEnabled(CaptureToolFeatures.Feature_ImageEdit_Shapes))
                     .Returns(true);

        var vm = Create();
        
        // Toggle shapes mode on
        vm.ToggleShapesModeCommand.Execute();
        
        // Add a drawable using the AddDrawable method
        var drawable = new RectangleDrawable(
            new System.Numerics.Vector2(10, 10),
            new Size(50, 50),
            Color.Red,
            Color.Transparent,
            2);
        vm.AddDrawable(drawable);

        // Act - try to delete with invalid indices
        vm.OnShapeDeleted(-1);
        vm.OnShapeDeleted(999);

        // Assert
        vm.Drawables.Count.Should().Be(1, "Drawable should not be removed with invalid index");
    }
}
