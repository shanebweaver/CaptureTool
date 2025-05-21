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
    public void GetRotationStepsTest()
    {
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate90FlipNone, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate90FlipNone, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate180FlipNone, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate180FlipNone, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate270FlipNone, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate270FlipNone, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate270FlipNone, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate270FlipNone, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate270FlipNone, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate270FlipNone, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate270FlipNone, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate270FlipNone, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate90FlipX, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate90FlipX, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(RotateFlipType.RotateNoneFlipX, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(RotateFlipType.RotateNoneFlipX, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate270FlipX, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate270FlipX, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate180FlipX, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate180FlipX, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate270FlipX));

    }

    [TestMethod]
    public void ToCanonicalCropRectTest()
    {
        // Portrait dimensions, width < height
        int imageWidth = 200;
        int imageHeight = 300;

        Size imageSize = new(imageWidth, imageHeight);
        Size cropRectSizePortrait = new(imageWidth / 2, imageHeight / 2);
        Size cropRectSizeLandscape = new(imageHeight / 2, imageWidth / 2);

        int imageWidthCenterPortrait = imageWidth - cropRectSizePortrait.Width;
        int imageHeightCenterPortrait = imageHeight - cropRectSizePortrait.Height;
        int imageWidthCenterLandscape = imageHeight - cropRectSizeLandscape.Width;
        int imageHeightCenterLandscape = imageWidth - cropRectSizeLandscape.Height;

        // Corners of the image
        Point topLeft = new(0, 0);
        Point topRightPortrait = new(imageWidthCenterPortrait, 0);
        Point bottomLeftPortrait = new(0, imageHeightCenterPortrait);
        Point bottomRightPortrait = new(imageWidthCenterPortrait, imageHeightCenterPortrait);
        Point topRightLandscape = new(imageWidthCenterLandscape, 0);
        Point bottomLeftLandscape = new(0, imageHeightCenterLandscape);
        Point bottomRightLandscape = new(imageWidthCenterLandscape, imageHeightCenterLandscape);

        // Traditional graph quadrants
        Rectangle quad1Portrait = new(topRightPortrait, cropRectSizePortrait);
        Rectangle quad2Portrait = new(topLeft, cropRectSizePortrait);
        Rectangle quad3Portrait = new(bottomLeftPortrait, cropRectSizePortrait);
        Rectangle quad4Portrait = new(bottomRightPortrait, cropRectSizePortrait);
        Rectangle quad1Landscape = new(topRightLandscape, cropRectSizeLandscape);
        Rectangle quad2Landscape = new(topLeft, cropRectSizeLandscape);
        Rectangle quad3Landscape = new(bottomLeftLandscape, cropRectSizeLandscape);
        Rectangle quad4Landscape = new(bottomRightLandscape, cropRectSizeLandscape);

        Assert.AreEqual(quad2Portrait, OrientationHelper.ToCanonicalCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(quad2Portrait, OrientationHelper.ToCanonicalCropRect(quad1Landscape, imageSize, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(quad2Portrait, OrientationHelper.ToCanonicalCropRect(quad4Portrait, imageSize, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(quad2Portrait, OrientationHelper.ToCanonicalCropRect(quad3Landscape, imageSize, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(quad2Portrait, OrientationHelper.ToCanonicalCropRect(quad1Portrait, imageSize, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(quad2Portrait, OrientationHelper.ToCanonicalCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(quad2Portrait, OrientationHelper.ToCanonicalCropRect(quad3Portrait, imageSize, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(quad2Portrait, OrientationHelper.ToCanonicalCropRect(quad4Landscape, imageSize, RotateFlipType.Rotate270FlipX));
    }

    [TestMethod]
    public void FromCanonicalCropRect()
    {
        // Portrait dimensions, width < height
        int imageWidth = 200;
        int imageHeight = 300;

        Size imageSize = new(imageWidth, imageHeight);
        Size cropRectSizePortrait = new(imageWidth / 2, imageHeight / 2);
        Size cropRectSizeLandscape = new(imageHeight / 2, imageWidth / 2);

        int imageWidthCenterPortrait = imageWidth - cropRectSizePortrait.Width;
        int imageHeightCenterPortrait = imageHeight - cropRectSizePortrait.Height;
        int imageWidthCenterLandscape = imageHeight - cropRectSizeLandscape.Width;
        int imageHeightCenterLandscape = imageWidth - cropRectSizeLandscape.Height;

        // Corners of the image
        Point topLeft = new(0, 0);
        Point topRightPortrait = new(imageWidthCenterPortrait, 0);
        Point bottomLeftPortrait = new(0, imageHeightCenterPortrait);
        Point bottomRightPortrait = new(imageWidthCenterPortrait, imageHeightCenterPortrait);
        Point topRightLandscape = new(imageWidthCenterLandscape, 0);
        Point bottomLeftLandscape = new(0, imageHeightCenterLandscape);
        Point bottomRightLandscape = new(imageWidthCenterLandscape, imageHeightCenterLandscape);

        // Traditional graph quadrants
        Rectangle quad1Portrait = new(topRightPortrait, cropRectSizePortrait);
        Rectangle quad2Portrait = new(topLeft, cropRectSizePortrait);
        Rectangle quad3Portrait = new(bottomLeftPortrait, cropRectSizePortrait);
        Rectangle quad4Portrait = new(bottomRightPortrait, cropRectSizePortrait);
        Rectangle quad1Landscape = new(topRightLandscape, cropRectSizeLandscape);
        Rectangle quad2Landscape = new(topLeft, cropRectSizeLandscape);
        Rectangle quad3Landscape = new(bottomLeftLandscape, cropRectSizeLandscape);
        Rectangle quad4Landscape = new(bottomRightLandscape, cropRectSizeLandscape);

        Assert.AreEqual(quad2Portrait, OrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(quad1Landscape, OrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(quad4Portrait, OrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(quad3Landscape, OrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(quad1Portrait, OrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(quad2Landscape, OrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(quad3Portrait, OrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(quad4Landscape, OrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate270FlipX));
    }

    [TestMethod]
    public void GetOrientedCropRectTest()
    {
        // Portrait dimensions, width < height
        int imageWidth = 200;
        int imageHeight = 300;

        Size imageSize = new(imageWidth, imageHeight);
        Size cropRectSizePortrait = new(imageWidth / 2, imageHeight / 2);
        Size cropRectSizeLandscape = new(imageHeight / 2, imageWidth / 2);

        int imageWidthCenterPortrait = imageWidth - cropRectSizePortrait.Width;
        int imageHeightCenterPortrait = imageHeight - cropRectSizePortrait.Height;
        int imageWidthCenterLandscape = imageHeight - cropRectSizeLandscape.Width;
        int imageHeightCenterLandscape = imageWidth - cropRectSizeLandscape.Height;

        // Corners of the image
        Point topLeft = new(0, 0);
        Point topRightPortrait = new(imageWidthCenterPortrait, 0);
        Point bottomLeftPortrait = new(0, imageHeightCenterPortrait);
        Point bottomRightPortrait = new(imageWidthCenterPortrait, imageHeightCenterPortrait);
        Point topRightLandscape = new(imageWidthCenterLandscape, 0);
        Point bottomLeftLandscape = new(0, imageHeightCenterLandscape);
        Point bottomRightLandscape = new(imageWidthCenterLandscape, imageHeightCenterLandscape);

        // Traditional graph quadrants
        Rectangle quad1Portrait = new(topRightPortrait, cropRectSizePortrait);
        Rectangle quad2Portrait = new(topLeft, cropRectSizePortrait);
        Rectangle quad3Portrait = new(bottomLeftPortrait, cropRectSizePortrait);
        Rectangle quad4Portrait = new(bottomRightPortrait, cropRectSizePortrait);
        Rectangle quad1Landscape = new(topRightLandscape, cropRectSizeLandscape);
        Rectangle quad2Landscape = new(topLeft, cropRectSizeLandscape);
        Rectangle quad3Landscape = new(bottomLeftLandscape, cropRectSizeLandscape);
        Rectangle quad4Landscape = new(bottomRightLandscape, cropRectSizeLandscape);

        // Tips:
        // GetOrientedCropRect helps determine the new size and placement of the crop rectangle when transitioning from one orientation to another.
        // The method expects that the oldOrientation value matches the orientation of the provided cropRect.
        // If oldOrientation is turned 90 degrees, the cropRect should oriented to match, swapping height and width.
        // The imageSize should always be the correctly oriented size of the image, unaffected by rotation.
        // This test assumes that the image is portrait, taller than it is wide.
        // The resulting cropRect should be in the correct placement and size for the new orientation.

        Assert.AreEqual(quad2Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(quad1Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(quad4Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(quad3Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(quad1Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(quad2Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(quad3Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(quad4Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(quad3Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate90FlipNone, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(quad2Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(quad1Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(quad4Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(quad4Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate90FlipNone, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(quad1Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(quad2Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(quad3Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(quad4Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipNone, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(quad3Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(quad2Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(quad1Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(quad3Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipNone, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(quad4Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(quad1Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(quad2Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(quad1Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate270FlipNone, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(quad4Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate270FlipNone, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(quad3Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate270FlipNone, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(quad2Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate270FlipNone, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(quad2Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate270FlipNone, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(quad3Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate270FlipNone, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(quad4Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate270FlipNone, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(quad1Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate270FlipNone, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(quad1Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipX, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(quad4Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(quad3Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(quad2Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(quad2Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipX, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(quad3Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(quad4Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(quad1Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(quad2Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate90FlipX, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(quad1Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(quad4Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(quad3Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(quad1Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate90FlipX, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(quad2Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(quad3Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(quad4Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(quad3Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipX, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(quad2Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(quad1Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(quad4Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(quad4Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipX, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(quad1Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(quad2Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(quad3Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate270FlipX));

        Assert.AreEqual(quad4Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate270FlipX, RotateFlipType.RotateNoneFlipNone));
        Assert.AreEqual(quad3Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate90FlipNone));
        Assert.AreEqual(quad2Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate180FlipNone));
        Assert.AreEqual(quad1Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate270FlipNone));
        Assert.AreEqual(quad3Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate270FlipX, RotateFlipType.RotateNoneFlipX));
        Assert.AreEqual(quad4Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate90FlipX));
        Assert.AreEqual(quad1Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate180FlipX));
        Assert.AreEqual(quad2Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate270FlipX));
    }
}
