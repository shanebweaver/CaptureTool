using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;

namespace CaptureTool.Edit.Windows.Tests;

[TestClass]
public sealed class OrientationHelperTests
{
    [TestMethod]
    public void GetFlippedOrientationTest()
    {
        (Orientation original, FlipDirection flip, Orientation expected)[] testValues = [
            (Orientation.RotateNoneFlipNone, FlipDirection.Horizontal, Orientation.RotateNoneFlipX),
            (Orientation.RotateNoneFlipNone, FlipDirection.Vertical, Orientation.RotateNoneFlipY),

            (Orientation.Rotate90FlipNone, FlipDirection.Horizontal, Orientation.Rotate90FlipX),
            (Orientation.Rotate90FlipNone, FlipDirection.Vertical, Orientation.Rotate90FlipY),

            (Orientation.Rotate180FlipNone, FlipDirection.Horizontal, Orientation.Rotate180FlipX),
            (Orientation.Rotate180FlipNone, FlipDirection.Vertical, Orientation.Rotate180FlipY),

            (Orientation.Rotate270FlipNone, FlipDirection.Horizontal, Orientation.Rotate270FlipX),
            (Orientation.Rotate270FlipNone, FlipDirection.Vertical, Orientation.Rotate270FlipY),

            (Orientation.RotateNoneFlipX, FlipDirection.Horizontal, Orientation.RotateNoneFlipNone),
            (Orientation.RotateNoneFlipX, FlipDirection.Vertical, Orientation.RotateNoneFlipXY),

            (Orientation.Rotate90FlipX, FlipDirection.Horizontal, Orientation.Rotate90FlipNone),
            (Orientation.Rotate90FlipX, FlipDirection.Vertical, Orientation.Rotate90FlipXY),

            (Orientation.Rotate180FlipX, FlipDirection.Horizontal, Orientation.Rotate180FlipNone),
            (Orientation.Rotate180FlipX, FlipDirection.Vertical, Orientation.Rotate180FlipXY),

            (Orientation.Rotate270FlipX, FlipDirection.Horizontal, Orientation.Rotate270FlipNone),
            (Orientation.Rotate270FlipX, FlipDirection.Vertical, Orientation.Rotate270FlipXY),
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
        Assert.AreEqual(Orientation.Rotate270FlipNone, OrientationHelper.GetRotatedOrientation(Orientation.RotateNoneFlipNone, RotationDirection.CounterClockwise));
        Assert.AreEqual(Orientation.RotateNoneFlipNone, OrientationHelper.GetRotatedOrientation(Orientation.Rotate90FlipNone, RotationDirection.CounterClockwise));
        Assert.AreEqual(Orientation.Rotate90FlipNone, OrientationHelper.GetRotatedOrientation(Orientation.Rotate180FlipNone, RotationDirection.CounterClockwise));
        Assert.AreEqual(Orientation.Rotate180FlipNone, OrientationHelper.GetRotatedOrientation(Orientation.Rotate270FlipNone, RotationDirection.CounterClockwise));

        Assert.AreEqual(Orientation.Rotate90FlipNone, OrientationHelper.GetRotatedOrientation(Orientation.RotateNoneFlipNone, RotationDirection.Clockwise));
        Assert.AreEqual(Orientation.Rotate180FlipNone, OrientationHelper.GetRotatedOrientation(Orientation.Rotate90FlipNone, RotationDirection.Clockwise));
        Assert.AreEqual(Orientation.Rotate270FlipNone, OrientationHelper.GetRotatedOrientation(Orientation.Rotate180FlipNone, RotationDirection.Clockwise));
        Assert.AreEqual(Orientation.RotateNoneFlipNone, OrientationHelper.GetRotatedOrientation(Orientation.Rotate270FlipNone, RotationDirection.Clockwise));

        Assert.AreEqual(Orientation.Rotate90FlipX, OrientationHelper.GetRotatedOrientation(Orientation.RotateNoneFlipX, RotationDirection.CounterClockwise));
        Assert.AreEqual(Orientation.Rotate180FlipX, OrientationHelper.GetRotatedOrientation(Orientation.Rotate90FlipX, RotationDirection.CounterClockwise));
        Assert.AreEqual(Orientation.Rotate270FlipX, OrientationHelper.GetRotatedOrientation(Orientation.Rotate180FlipX, RotationDirection.CounterClockwise));
        Assert.AreEqual(Orientation.RotateNoneFlipX, OrientationHelper.GetRotatedOrientation(Orientation.Rotate270FlipX, RotationDirection.CounterClockwise));

        Assert.AreEqual(Orientation.Rotate270FlipX, OrientationHelper.GetRotatedOrientation(Orientation.RotateNoneFlipX, RotationDirection.Clockwise));
        Assert.AreEqual(Orientation.RotateNoneFlipX, OrientationHelper.GetRotatedOrientation(Orientation.Rotate90FlipX, RotationDirection.Clockwise));
        Assert.AreEqual(Orientation.Rotate90FlipX, OrientationHelper.GetRotatedOrientation(Orientation.Rotate180FlipX, RotationDirection.Clockwise));
        Assert.AreEqual(Orientation.Rotate180FlipX, OrientationHelper.GetRotatedOrientation(Orientation.Rotate270FlipX, RotationDirection.Clockwise));
    }

    [TestMethod]
    public void IsTurnedTest()
    {
        Assert.IsTrue(OrientationHelper.IsTurned(Orientation.Rotate90FlipNone));
        Assert.IsTrue(OrientationHelper.IsTurned(Orientation.Rotate270FlipNone));
        Assert.IsTrue(OrientationHelper.IsTurned(Orientation.Rotate90FlipX));
        Assert.IsTrue(OrientationHelper.IsTurned(Orientation.Rotate270FlipX));
        Assert.IsFalse(OrientationHelper.IsTurned(Orientation.RotateNoneFlipNone));
        Assert.IsFalse(OrientationHelper.IsTurned(Orientation.Rotate180FlipNone));
        Assert.IsFalse(OrientationHelper.IsTurned(Orientation.RotateNoneFlipX));
        Assert.IsFalse(OrientationHelper.IsTurned(Orientation.Rotate180FlipX));
    }

    [TestMethod]
    public void GetOrientedImageSizeTest()
    {
        int height = 200;
        int width = 300;
        Size imageSize = new(width, height);
        Size imageSizeOriented = new(height, width);
        Assert.AreEqual(imageSize, OrientationHelper.GetOrientedImageSize(imageSize, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(imageSizeOriented, OrientationHelper.GetOrientedImageSize(imageSize, Orientation.Rotate90FlipNone));
        Assert.AreEqual(imageSize, OrientationHelper.GetOrientedImageSize(imageSize, Orientation.Rotate180FlipNone));
        Assert.AreEqual(imageSizeOriented, OrientationHelper.GetOrientedImageSize(imageSize, Orientation.Rotate270FlipNone));
        Assert.AreEqual(imageSize, OrientationHelper.GetOrientedImageSize(imageSize, Orientation.RotateNoneFlipX));
        Assert.AreEqual(imageSizeOriented, OrientationHelper.GetOrientedImageSize(imageSize, Orientation.Rotate90FlipX));
        Assert.AreEqual(imageSize, OrientationHelper.GetOrientedImageSize(imageSize, Orientation.Rotate180FlipX));
        Assert.AreEqual(imageSizeOriented, OrientationHelper.GetOrientedImageSize(imageSize, Orientation.Rotate270FlipX));
    }

    [TestMethod]
    public void GetRotationStepsTest()
    {
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(Orientation.RotateNoneFlipNone, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(Orientation.RotateNoneFlipNone, Orientation.Rotate90FlipNone));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(Orientation.RotateNoneFlipNone, Orientation.Rotate180FlipNone));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(Orientation.RotateNoneFlipNone, Orientation.Rotate270FlipNone));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(Orientation.RotateNoneFlipNone, Orientation.RotateNoneFlipX));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(Orientation.RotateNoneFlipNone, Orientation.Rotate90FlipX));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(Orientation.RotateNoneFlipNone, Orientation.Rotate180FlipX));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(Orientation.RotateNoneFlipNone, Orientation.Rotate270FlipX));

        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(Orientation.Rotate90FlipNone, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(Orientation.Rotate90FlipNone, Orientation.Rotate90FlipNone));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(Orientation.Rotate90FlipNone, Orientation.Rotate180FlipNone));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(Orientation.Rotate90FlipNone, Orientation.Rotate270FlipNone));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(Orientation.Rotate90FlipNone, Orientation.RotateNoneFlipX));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(Orientation.Rotate90FlipNone, Orientation.Rotate90FlipX));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(Orientation.Rotate90FlipNone, Orientation.Rotate180FlipX));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(Orientation.Rotate90FlipNone, Orientation.Rotate270FlipX));

        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(Orientation.Rotate180FlipNone, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(Orientation.Rotate180FlipNone, Orientation.Rotate90FlipNone));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(Orientation.Rotate180FlipNone, Orientation.Rotate180FlipNone));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(Orientation.Rotate180FlipNone, Orientation.Rotate270FlipNone));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(Orientation.Rotate180FlipNone, Orientation.RotateNoneFlipX));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(Orientation.Rotate180FlipNone, Orientation.Rotate90FlipX));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(Orientation.Rotate180FlipNone, Orientation.Rotate180FlipX));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(Orientation.Rotate180FlipNone, Orientation.Rotate270FlipX));

        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(Orientation.Rotate270FlipNone, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(Orientation.Rotate270FlipNone, Orientation.Rotate90FlipNone));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(Orientation.Rotate270FlipNone, Orientation.Rotate180FlipNone));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(Orientation.Rotate270FlipNone, Orientation.Rotate270FlipNone));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(Orientation.Rotate270FlipNone, Orientation.RotateNoneFlipX));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(Orientation.Rotate270FlipNone, Orientation.Rotate90FlipX));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(Orientation.Rotate270FlipNone, Orientation.Rotate180FlipX));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(Orientation.Rotate270FlipNone, Orientation.Rotate270FlipX));

        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(Orientation.Rotate90FlipX, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(Orientation.Rotate90FlipX, Orientation.Rotate90FlipNone));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(Orientation.Rotate90FlipX, Orientation.Rotate180FlipNone));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(Orientation.Rotate90FlipX, Orientation.Rotate270FlipNone));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(Orientation.Rotate90FlipX, Orientation.RotateNoneFlipX));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(Orientation.Rotate90FlipX, Orientation.Rotate90FlipX));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(Orientation.Rotate90FlipX, Orientation.Rotate180FlipX));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(Orientation.Rotate90FlipX, Orientation.Rotate270FlipX));

        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(Orientation.RotateNoneFlipX, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(Orientation.RotateNoneFlipX, Orientation.Rotate90FlipNone));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(Orientation.RotateNoneFlipX, Orientation.Rotate180FlipNone));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(Orientation.RotateNoneFlipX, Orientation.Rotate270FlipNone));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(Orientation.RotateNoneFlipX, Orientation.RotateNoneFlipX));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(Orientation.RotateNoneFlipX, Orientation.Rotate90FlipX));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(Orientation.RotateNoneFlipX, Orientation.Rotate180FlipX));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(Orientation.RotateNoneFlipX, Orientation.Rotate270FlipX));

        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(Orientation.Rotate270FlipX, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(Orientation.Rotate270FlipX, Orientation.Rotate90FlipNone));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(Orientation.Rotate270FlipX, Orientation.Rotate180FlipNone));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(Orientation.Rotate270FlipX, Orientation.Rotate270FlipNone));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(Orientation.Rotate270FlipX, Orientation.RotateNoneFlipX));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(Orientation.Rotate270FlipX, Orientation.Rotate90FlipX));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(Orientation.Rotate270FlipX, Orientation.Rotate180FlipX));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(Orientation.Rotate270FlipX, Orientation.Rotate270FlipX));

        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(Orientation.Rotate180FlipX, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(Orientation.Rotate180FlipX, Orientation.Rotate90FlipNone));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(Orientation.Rotate180FlipX, Orientation.Rotate180FlipNone));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(Orientation.Rotate180FlipX, Orientation.Rotate270FlipNone));
        Assert.AreEqual(2, OrientationHelper.GetRotationSteps(Orientation.Rotate180FlipX, Orientation.RotateNoneFlipX));
        Assert.AreEqual(3, OrientationHelper.GetRotationSteps(Orientation.Rotate180FlipX, Orientation.Rotate90FlipX));
        Assert.AreEqual(0, OrientationHelper.GetRotationSteps(Orientation.Rotate180FlipX, Orientation.Rotate180FlipX));
        Assert.AreEqual(1, OrientationHelper.GetRotationSteps(Orientation.Rotate180FlipX, Orientation.Rotate270FlipX));
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

        Assert.AreEqual(quad2Portrait, OrientationHelper.ToCanonicalCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(quad2Portrait, OrientationHelper.ToCanonicalCropRect(quad1Landscape, imageSize, Orientation.Rotate90FlipNone));
        Assert.AreEqual(quad2Portrait, OrientationHelper.ToCanonicalCropRect(quad4Portrait, imageSize, Orientation.Rotate180FlipNone));
        Assert.AreEqual(quad2Portrait, OrientationHelper.ToCanonicalCropRect(quad3Landscape, imageSize, Orientation.Rotate270FlipNone));
        Assert.AreEqual(quad2Portrait, OrientationHelper.ToCanonicalCropRect(quad1Portrait, imageSize, Orientation.RotateNoneFlipX));
        Assert.AreEqual(quad2Portrait, OrientationHelper.ToCanonicalCropRect(quad2Landscape, imageSize, Orientation.Rotate90FlipX));
        Assert.AreEqual(quad2Portrait, OrientationHelper.ToCanonicalCropRect(quad3Portrait, imageSize, Orientation.Rotate180FlipX));
        Assert.AreEqual(quad2Portrait, OrientationHelper.ToCanonicalCropRect(quad4Landscape, imageSize, Orientation.Rotate270FlipX));
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

        Assert.AreEqual(quad2Portrait, OrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(quad1Landscape, OrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, Orientation.Rotate90FlipNone));
        Assert.AreEqual(quad4Portrait, OrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipNone));
        Assert.AreEqual(quad3Landscape, OrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, Orientation.Rotate270FlipNone));
        Assert.AreEqual(quad1Portrait, OrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipX));
        Assert.AreEqual(quad2Landscape, OrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, Orientation.Rotate90FlipX));
        Assert.AreEqual(quad3Portrait, OrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipX));
        Assert.AreEqual(quad4Landscape, OrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, Orientation.Rotate270FlipX));
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

        Assert.AreEqual(quad2Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipNone, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(quad1Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipNone, Orientation.Rotate90FlipNone));
        Assert.AreEqual(quad4Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipNone, Orientation.Rotate180FlipNone));
        Assert.AreEqual(quad3Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipNone, Orientation.Rotate270FlipNone));
        Assert.AreEqual(quad1Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipNone, Orientation.RotateNoneFlipX));
        Assert.AreEqual(quad2Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipNone, Orientation.Rotate90FlipX));
        Assert.AreEqual(quad3Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipNone, Orientation.Rotate180FlipX));
        Assert.AreEqual(quad4Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipNone, Orientation.Rotate270FlipX));

        Assert.AreEqual(quad3Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate90FlipNone, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(quad2Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate90FlipNone, Orientation.Rotate90FlipNone));
        Assert.AreEqual(quad1Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate90FlipNone, Orientation.Rotate180FlipNone));
        Assert.AreEqual(quad4Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate90FlipNone, Orientation.Rotate270FlipNone));
        Assert.AreEqual(quad4Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate90FlipNone, Orientation.RotateNoneFlipX));
        Assert.AreEqual(quad1Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate90FlipNone, Orientation.Rotate90FlipX));
        Assert.AreEqual(quad2Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate90FlipNone, Orientation.Rotate180FlipX));
        Assert.AreEqual(quad3Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate90FlipNone, Orientation.Rotate270FlipX));

        Assert.AreEqual(quad4Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipNone, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(quad3Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipNone, Orientation.Rotate90FlipNone));
        Assert.AreEqual(quad2Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipNone, Orientation.Rotate180FlipNone));
        Assert.AreEqual(quad1Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipNone, Orientation.Rotate270FlipNone));
        Assert.AreEqual(quad3Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipNone, Orientation.RotateNoneFlipX));
        Assert.AreEqual(quad4Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipNone, Orientation.Rotate90FlipX));
        Assert.AreEqual(quad1Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipNone, Orientation.Rotate180FlipX));
        Assert.AreEqual(quad2Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipNone, Orientation.Rotate270FlipX));

        Assert.AreEqual(quad1Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate270FlipNone, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(quad4Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate270FlipNone, Orientation.Rotate90FlipNone));
        Assert.AreEqual(quad3Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate270FlipNone, Orientation.Rotate180FlipNone));
        Assert.AreEqual(quad2Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate270FlipNone, Orientation.Rotate270FlipNone));
        Assert.AreEqual(quad2Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate270FlipNone, Orientation.RotateNoneFlipX));
        Assert.AreEqual(quad3Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate270FlipNone, Orientation.Rotate90FlipX));
        Assert.AreEqual(quad4Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate270FlipNone, Orientation.Rotate180FlipX));
        Assert.AreEqual(quad1Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate270FlipNone, Orientation.Rotate270FlipX));

        Assert.AreEqual(quad1Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipX, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(quad4Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipX, Orientation.Rotate90FlipNone));
        Assert.AreEqual(quad3Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipX, Orientation.Rotate180FlipNone));
        Assert.AreEqual(quad2Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipX, Orientation.Rotate270FlipNone));
        Assert.AreEqual(quad2Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipX, Orientation.RotateNoneFlipX));
        Assert.AreEqual(quad3Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipX, Orientation.Rotate90FlipX));
        Assert.AreEqual(quad4Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipX, Orientation.Rotate180FlipX));
        Assert.AreEqual(quad1Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.RotateNoneFlipX, Orientation.Rotate270FlipX));

        Assert.AreEqual(quad2Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate90FlipX, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(quad1Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate90FlipX, Orientation.Rotate90FlipNone));
        Assert.AreEqual(quad4Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate90FlipX, Orientation.Rotate180FlipNone));
        Assert.AreEqual(quad3Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate90FlipX, Orientation.Rotate270FlipNone));
        Assert.AreEqual(quad1Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate90FlipX, Orientation.RotateNoneFlipX));
        Assert.AreEqual(quad2Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate90FlipX, Orientation.Rotate90FlipX));
        Assert.AreEqual(quad3Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate90FlipX, Orientation.Rotate180FlipX));
        Assert.AreEqual(quad4Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate90FlipX, Orientation.Rotate270FlipX));

        Assert.AreEqual(quad3Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipX, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(quad2Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipX, Orientation.Rotate90FlipNone));
        Assert.AreEqual(quad1Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipX, Orientation.Rotate180FlipNone));
        Assert.AreEqual(quad4Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipX, Orientation.Rotate270FlipNone));
        Assert.AreEqual(quad4Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipX, Orientation.RotateNoneFlipX));
        Assert.AreEqual(quad1Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipX, Orientation.Rotate90FlipX));
        Assert.AreEqual(quad2Portrait, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipX, Orientation.Rotate180FlipX));
        Assert.AreEqual(quad3Landscape, OrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, Orientation.Rotate180FlipX, Orientation.Rotate270FlipX));

        Assert.AreEqual(quad4Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate270FlipX, Orientation.RotateNoneFlipNone));
        Assert.AreEqual(quad3Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate270FlipX, Orientation.Rotate90FlipNone));
        Assert.AreEqual(quad2Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate270FlipX, Orientation.Rotate180FlipNone));
        Assert.AreEqual(quad1Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate270FlipX, Orientation.Rotate270FlipNone));
        Assert.AreEqual(quad3Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate270FlipX, Orientation.RotateNoneFlipX));
        Assert.AreEqual(quad4Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate270FlipX, Orientation.Rotate90FlipX));
        Assert.AreEqual(quad1Portrait, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate270FlipX, Orientation.Rotate180FlipX));
        Assert.AreEqual(quad2Landscape, OrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, Orientation.Rotate270FlipX, Orientation.Rotate270FlipX));
    }
}
