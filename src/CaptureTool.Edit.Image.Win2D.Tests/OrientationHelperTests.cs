using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;

namespace CaptureTool.Edit.Image.Win2D.Tests;

[TestClass]
public sealed class OrientationHelperTests
{
    [TestMethod]
    public void GetFlippedOrientationTest()
    {
        (RotateFlipType original, FlipDirection flip, RotateFlipType expected)[] testValues = [
            (RotateFlipType.RotateNoneFlipNone, FlipDirection.Horizontal, RotateFlipType.RotateNoneFlipX),
            (RotateFlipType.RotateNoneFlipNone, FlipDirection.Vertical, RotateFlipType.RotateNoneFlipY),

            (RotateFlipType.Rotate90FlipNone, FlipDirection.Horizontal, RotateFlipType.Rotate90FlipX),
            (RotateFlipType.Rotate90FlipNone, FlipDirection.Vertical, RotateFlipType.Rotate90FlipY),

            (RotateFlipType.Rotate180FlipNone, FlipDirection.Horizontal, RotateFlipType.Rotate180FlipX),
            (RotateFlipType.Rotate180FlipNone, FlipDirection.Vertical, RotateFlipType.Rotate180FlipY),

            (RotateFlipType.Rotate270FlipNone, FlipDirection.Horizontal, RotateFlipType.Rotate270FlipX),
            (RotateFlipType.Rotate270FlipNone, FlipDirection.Vertical, RotateFlipType.Rotate270FlipY),

            (RotateFlipType.RotateNoneFlipX, FlipDirection.Horizontal, RotateFlipType.RotateNoneFlipNone),
            (RotateFlipType.RotateNoneFlipX, FlipDirection.Vertical, RotateFlipType.RotateNoneFlipXY),

            (RotateFlipType.Rotate90FlipX, FlipDirection.Horizontal, RotateFlipType.Rotate90FlipNone),
            (RotateFlipType.Rotate90FlipX, FlipDirection.Vertical, RotateFlipType.Rotate90FlipXY),

            (RotateFlipType.Rotate180FlipX, FlipDirection.Horizontal, RotateFlipType.Rotate180FlipNone),
            (RotateFlipType.Rotate180FlipX, FlipDirection.Vertical, RotateFlipType.Rotate180FlipXY),

            (RotateFlipType.Rotate270FlipX, FlipDirection.Horizontal, RotateFlipType.Rotate270FlipNone),
            (RotateFlipType.Rotate270FlipX, FlipDirection.Vertical, RotateFlipType.Rotate270FlipXY),
        ];

        foreach (var (original, flip, expected) in testValues)
        {
            Assert.AreEqual(expected, OrientationHelper.GetFlippedOrientation(original, flip));
        }
    }

    [TestMethod]
    public void GetFlippedCropRectTest()
    {
        Rectangle rect = new(0, 0, 100, 100);
        Size imageSize = new(200, 200);

        Rectangle xFlippedRect = OrientationHelper.GetFlippedCropRect(rect, imageSize, FlipDirection.Horizontal);
        Assert.AreEqual(new Rectangle(100, 0, 100, 100), xFlippedRect);

        Rectangle yFlippedRect = OrientationHelper.GetFlippedCropRect(rect, imageSize, FlipDirection.Vertical);
        Assert.AreEqual(new Rectangle(0, 100, 100, 100), yFlippedRect);
    }

    [TestMethod]
    public void GetRotatedOrientationTest()
    {
        Assert.AreEqual(RotateFlipType.Rotate270FlipNone, OrientationHelper.GetRotatedOrientation(RotateFlipType.RotateNoneFlipNone, RotationDirection.CounterClockwise));
        Assert.AreEqual(RotateFlipType.RotateNoneFlipNone, OrientationHelper.GetRotatedOrientation(RotateFlipType.Rotate90FlipNone, RotationDirection.CounterClockwise));
        Assert.AreEqual(RotateFlipType.Rotate90FlipNone, OrientationHelper.GetRotatedOrientation(RotateFlipType.Rotate180FlipNone, RotationDirection.CounterClockwise));
        Assert.AreEqual(RotateFlipType.Rotate180FlipNone, OrientationHelper.GetRotatedOrientation(RotateFlipType.Rotate270FlipNone, RotationDirection.CounterClockwise));

        Assert.AreEqual(RotateFlipType.Rotate90FlipNone, OrientationHelper.GetRotatedOrientation(RotateFlipType.RotateNoneFlipNone, RotationDirection.Clockwise));
        Assert.AreEqual(RotateFlipType.Rotate180FlipNone, OrientationHelper.GetRotatedOrientation(RotateFlipType.Rotate90FlipNone, RotationDirection.Clockwise));
        Assert.AreEqual(RotateFlipType.Rotate270FlipNone, OrientationHelper.GetRotatedOrientation(RotateFlipType.Rotate180FlipNone, RotationDirection.Clockwise));
        Assert.AreEqual(RotateFlipType.RotateNoneFlipNone, OrientationHelper.GetRotatedOrientation(RotateFlipType.Rotate270FlipNone, RotationDirection.Clockwise));

        Assert.AreEqual(RotateFlipType.Rotate270FlipX, OrientationHelper.GetRotatedOrientation(RotateFlipType.RotateNoneFlipX, RotationDirection.CounterClockwise));
        Assert.AreEqual(RotateFlipType.RotateNoneFlipX, OrientationHelper.GetRotatedOrientation(RotateFlipType.Rotate90FlipX, RotationDirection.CounterClockwise));
        Assert.AreEqual(RotateFlipType.Rotate90FlipX, OrientationHelper.GetRotatedOrientation(RotateFlipType.Rotate180FlipX, RotationDirection.CounterClockwise));
        Assert.AreEqual(RotateFlipType.Rotate180FlipX, OrientationHelper.GetRotatedOrientation(RotateFlipType.Rotate270FlipX, RotationDirection.CounterClockwise));

        Assert.AreEqual(RotateFlipType.Rotate90FlipX, OrientationHelper.GetRotatedOrientation(RotateFlipType.RotateNoneFlipX, RotationDirection.Clockwise));
        Assert.AreEqual(RotateFlipType.Rotate180FlipX, OrientationHelper.GetRotatedOrientation(RotateFlipType.Rotate90FlipX, RotationDirection.Clockwise));
        Assert.AreEqual(RotateFlipType.Rotate270FlipX, OrientationHelper.GetRotatedOrientation(RotateFlipType.Rotate180FlipX, RotationDirection.Clockwise));
        Assert.AreEqual(RotateFlipType.RotateNoneFlipX, OrientationHelper.GetRotatedOrientation(RotateFlipType.Rotate270FlipX, RotationDirection.Clockwise));
    }

    [TestMethod]
    public void IsTurnedTest()
    {
        Assert.IsTrue(OrientationHelper.IsTurned(RotateFlipType.Rotate90FlipNone));
        Assert.IsTrue(OrientationHelper.IsTurned(RotateFlipType.Rotate270FlipNone));
        Assert.IsTrue(OrientationHelper.IsTurned(RotateFlipType.Rotate90FlipX));
        Assert.IsTrue(OrientationHelper.IsTurned(RotateFlipType.Rotate270FlipX));
        Assert.IsFalse(OrientationHelper.IsTurned(RotateFlipType.RotateNoneFlipNone));
        Assert.IsFalse(OrientationHelper.IsTurned(RotateFlipType.Rotate180FlipNone));
        Assert.IsFalse(OrientationHelper.IsTurned(RotateFlipType.RotateNoneFlipX));
        Assert.IsFalse(OrientationHelper.IsTurned(RotateFlipType.Rotate180FlipX));
    }

    [TestMethod]
    public void GetOrientedImageSizeTest()
    {
        int height = 200;
        int width = 300;
        Size imageSize = new(width, height);
        Size imageSizeOriented = new(height, width);
        Assert.AreEqual(imageSize, OrientationHelper.GetOrientedImageSize(imageSize, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(imageSizeOriented, OrientationHelper.GetOrientedImageSize(imageSize, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(imageSize, OrientationHelper.GetOrientedImageSize(imageSize, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(imageSizeOriented, OrientationHelper.GetOrientedImageSize(imageSize, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(imageSize, OrientationHelper.GetOrientedImageSize(imageSize, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(imageSizeOriented, OrientationHelper.GetOrientedImageSize(imageSize, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(imageSize, OrientationHelper.GetOrientedImageSize(imageSize, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(imageSizeOriented, OrientationHelper.GetOrientedImageSize(imageSize, RotateFlipType.Rotate270FlipX));
    }

    [TestMethod]
    public void GetOrientedCropRectTest()
    {
        Rectangle rect = new(0, 0, 100, 100);
        Size imageSize = new(200, 200);

        Assert.AreEqual(new Rectangle(100, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(new Rectangle(100, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(new Rectangle(0, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(new Rectangle(100, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(new Rectangle(0, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(new Rectangle(0, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(new Rectangle(100, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(new Rectangle(0, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate90FlipNone, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(new Rectangle(100, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(new Rectangle(100, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(new Rectangle(100, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate90FlipNone, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(new Rectangle(100, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(new Rectangle(0, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(new Rectangle(0, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(new Rectangle(100, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate180FlipNone, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(new Rectangle(0, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(new Rectangle(100, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(new Rectangle(0, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate180FlipNone, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(new Rectangle(100, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(new Rectangle(100, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(new Rectangle(0, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(new Rectangle(100, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate270FlipNone, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(new Rectangle(100, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate270FlipNone, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(new Rectangle(0, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate270FlipNone, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(new Rectangle(0, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate270FlipNone, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(new Rectangle(0, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate270FlipNone, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(new Rectangle(100, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate270FlipNone, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(new Rectangle(100, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate270FlipNone, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(new Rectangle(100, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.RotateNoneFlipX, RotateFlipType.RotateNoneFlipNone));
        //Assert.AreEqual(new Rectangle(100, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(new Rectangle(0, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate180FlipNone));
        //Assert.AreEqual(new Rectangle(0, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate270FlipNone));
        //Assert.AreEqual(new Rectangle(0, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(new Rectangle(100, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate180FlipX));
        //Assert.AreEqual(new Rectangle(100, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate270FlipX));

        //Assert.AreEqual(new Rectangle(0, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate90FlipX, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(new Rectangle(100, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate90FlipNone));
        //Assert.AreEqual(new Rectangle(100, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate180FlipNone));
        //Assert.AreEqual(new Rectangle(0, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate270FlipNone));
        //Assert.AreEqual(new Rectangle(100, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate90FlipX, RotateFlipType.RotateNoneFlipX));
        //Assert.AreEqual(new Rectangle(0, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(new Rectangle(100, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(new Rectangle(0, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate180FlipX, RotateFlipType.RotateNoneFlipNone));
        //Assert.AreEqual(new Rectangle(0, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate90FlipNone));
        //Assert.AreEqual(new Rectangle(100, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate180FlipNone));
        //Assert.AreEqual(new Rectangle(100, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(new Rectangle(100, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate180FlipX, RotateFlipType.RotateNoneFlipX));
        //Assert.AreEqual(new Rectangle(100, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate90FlipX));
        //Assert.AreEqual(new Rectangle(0, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate270FlipX));

        //Assert.AreEqual(new Rectangle(100, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate270FlipX, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(new Rectangle(0, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate90FlipNone));
        //Assert.AreEqual(new Rectangle(0, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(new Rectangle(100, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate270FlipNone));
        //Assert.AreEqual(new Rectangle(0, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate270FlipX, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(new Rectangle(100, 100, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate90FlipX));
        //Assert.AreEqual(new Rectangle(100, 0, 100, 100), OrientationHelper.GetOrientedCropRect(rect, imageSize, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate180FlipX));
    }
}
