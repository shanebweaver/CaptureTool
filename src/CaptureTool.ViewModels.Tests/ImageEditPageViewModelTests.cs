using CaptureTool.Common.Storage;
using CaptureTool.Edit;
using CaptureTool.Edit.Drawable;
using CaptureTool.ViewModels.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels.Tests;

[TestClass]
public sealed class ImageEditPageViewModelTests
{
    private ImageEditPageViewModel _vm = null!;

    [TestInitialize]
    public void Setup()
    {
        _vm = new(
            new MockStoreService(),
            new MockAppController(),
            new MockCancellationService(),
            new MockTelemetryService(),
            new MockImageCanvasPrinter(),
            new MockImageCanvasExporter(),
            new MockFilePickerService(),
            new MockChromaKeyService(),
            new MockFeatureManager(),
            new MockShareService()
            );
    }

    [TestMethod]
    public async Task LoadTest()
    {
        Assert.IsFalse(_vm.IsLoaded);
        Assert.IsTrue(_vm.IsLoading);

        // Load
        string testImageFilePath = Guid.NewGuid().ToString();
        object parameter = new ImageFile(testImageFilePath);
        await _vm.LoadAsync(parameter, CancellationToken.None);
        Assert.IsTrue(_vm.IsLoaded);
        Assert.IsFalse(_vm.IsLoading);
        Assert.IsNotNull(_vm.ImageFile);
        Assert.AreEqual(testImageFilePath, _vm.ImageFile.Path);
        Assert.AreEqual(MockFilePickerService.DefaultImageSize, _vm.ImageSize);
        Assert.AreEqual(new Rectangle(Point.Empty, MockFilePickerService.DefaultImageSize), _vm.CropRect);
        Assert.AreEqual(ImageOrientation.RotateNoneFlipNone, _vm.Orientation);

        // Get first (only) drawable
        Assert.AreEqual(1, _vm.Drawables.Count);
        IDrawable drawable = _vm.Drawables.First();

        // Expect type to be an image drawable
        Assert.IsInstanceOfType<ImageDrawable>(drawable);
        ImageDrawable imageDrawable = (ImageDrawable)drawable;

        // Check values
        Assert.AreEqual(Vector2.Zero, imageDrawable.Offset);
        Assert.AreEqual(testImageFilePath, imageDrawable.FileName.Path);
        Assert.AreEqual(MockFilePickerService.DefaultImageSize, imageDrawable.ImageSize);

        // Dispose
        _vm.Dispose();
        Assert.IsFalse(_vm.IsLoaded);
        Assert.IsFalse(_vm.IsLoading);
        Assert.AreEqual(Rectangle.Empty, _vm.CropRect);
        Assert.AreEqual(Size.Empty, _vm.ImageSize);
        Assert.AreEqual(ImageOrientation.RotateNoneFlipNone, _vm.Orientation);
        Assert.AreEqual(0, _vm.Drawables.Count);
    }

    [TestMethod]
    public void ToggleCropModeTest()
    {
        Assert.IsFalse(_vm.IsInCropMode);

        Assert.IsTrue(_vm.ToggleCropModeCommand.CanExecute());
        _vm.ToggleCropModeCommand.Execute();

        Assert.IsTrue(_vm.IsInCropMode);

        Assert.IsTrue(_vm.ToggleCropModeCommand.CanExecute());
        _vm.ToggleCropModeCommand.Execute();

        Assert.IsFalse(_vm.IsInCropMode);
    }

    [TestMethod]
    public void CopyTest()
    {
        Assert.IsTrue(_vm.CopyCommand.CanExecute());
        _vm.CopyCommand.Execute();
    }

    [TestMethod]
    public void SaveTest()
    {
        Assert.IsTrue(_vm.SaveCommand.CanExecute());
        _vm.SaveCommand.Execute();
    }

    //[TestMethod]
    //public void UndoRedoCommands()
    //{
    //    Assert.IsTrue(_vm.UndoCommand.CanExecute());
    //    _vm.UndoCommand.Execute();

    //    Assert.IsTrue(_vm.RedoCommand.CanExecute());
    //    _vm.RedoCommand.Execute();
    //}

    [TestMethod]
    public void RotateTest()
    {
        // Rotate 360
        Assert.IsTrue(_vm.RotateCommand.CanExecute());
        Assert.AreEqual(ImageOrientation.RotateNoneFlipNone, _vm.Orientation);
        _vm.RotateCommand.Execute();
        Assert.AreEqual(ImageOrientation.Rotate90FlipNone, _vm.Orientation);
        _vm.RotateCommand.Execute();
        Assert.AreEqual(ImageOrientation.Rotate180FlipNone, _vm.Orientation);
        _vm.RotateCommand.Execute();
        Assert.AreEqual(ImageOrientation.Rotate270FlipNone, _vm.Orientation);
        _vm.RotateCommand.Execute();
        Assert.AreEqual(ImageOrientation.RotateNoneFlipNone, _vm.Orientation);
    }

    [TestMethod]
    public void FlipHorizontalTest()
    {
        Assert.IsTrue(_vm.FlipHorizontalCommand.CanExecute());
        Assert.AreEqual(ImageOrientation.RotateNoneFlipNone, _vm.Orientation);
        _vm.FlipHorizontalCommand.Execute();
        Assert.AreEqual(ImageOrientation.RotateNoneFlipX, _vm.Orientation);
        _vm.FlipHorizontalCommand.Execute();
        Assert.AreEqual(ImageOrientation.RotateNoneFlipNone, _vm.Orientation);
    }

    [TestMethod]
    public void FlipVerticalTest()
    {
        Assert.IsTrue(_vm.FlipVerticalCommand.CanExecute());
        Assert.AreEqual(ImageOrientation.RotateNoneFlipNone, _vm.Orientation);
        _vm.FlipVerticalCommand.Execute();
        Assert.AreEqual(ImageOrientation.Rotate180FlipX, _vm.Orientation);
        _vm.FlipVerticalCommand.Execute();
        Assert.AreEqual(ImageOrientation.RotateNoneFlipNone, _vm.Orientation);
    }

    [TestMethod]
    public void PrintTest()
    {
        Assert.IsTrue(_vm.PrintCommand.CanExecute());
        _vm.PrintCommand.Execute();
    }
}
