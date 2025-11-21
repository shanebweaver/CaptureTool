using CaptureTool.Domains.Edit.Interfaces;
using System.Drawing;

namespace CaptureTool.Domains.Edit.Tests;

[TestClass]
public sealed class ImageOrientationHelperTests
{
    [TestMethod]
    public void GetFlippedOrientationTest()
    {
        (ImageOrientation original, FlipDirection flip, ImageOrientation expected)[] testValues = [
            (ImageOrientation.RotateNoneFlipNone, FlipDirection.Horizontal, ImageOrientation.RotateNoneFlipX),
            (ImageOrientation.RotateNoneFlipNone, FlipDirection.Vertical, ImageOrientation.Rotate180FlipX),

            (ImageOrientation.Rotate90FlipNone, FlipDirection.Horizontal, ImageOrientation.Rotate90FlipX),
            (ImageOrientation.Rotate90FlipNone, FlipDirection.Vertical, ImageOrientation.Rotate270FlipX),

            (ImageOrientation.Rotate180FlipNone, FlipDirection.Horizontal, ImageOrientation.Rotate180FlipX),
            (ImageOrientation.Rotate180FlipNone, FlipDirection.Vertical, ImageOrientation.RotateNoneFlipX),

            (ImageOrientation.Rotate270FlipNone, FlipDirection.Horizontal, ImageOrientation.Rotate270FlipX),
            (ImageOrientation.Rotate270FlipNone, FlipDirection.Vertical, ImageOrientation.Rotate90FlipX),

            (ImageOrientation.RotateNoneFlipX, FlipDirection.Horizontal, ImageOrientation.RotateNoneFlipNone),
            (ImageOrientation.RotateNoneFlipX, FlipDirection.Vertical, ImageOrientation.Rotate180FlipNone),

            (ImageOrientation.Rotate90FlipX, FlipDirection.Horizontal, ImageOrientation.Rotate90FlipNone),
            (ImageOrientation.Rotate90FlipX, FlipDirection.Vertical, ImageOrientation.Rotate270FlipNone),

            (ImageOrientation.Rotate180FlipX, FlipDirection.Horizontal, ImageOrientation.Rotate180FlipNone),
            (ImageOrientation.Rotate180FlipX, FlipDirection.Vertical, ImageOrientation.RotateNoneFlipNone),

            (ImageOrientation.Rotate270FlipX, FlipDirection.Horizontal, ImageOrientation.Rotate270FlipNone),
            (ImageOrientation.Rotate270FlipX, FlipDirection.Vertical, ImageOrientation.Rotate90FlipNone),
        ];

        foreach (var (original, flip, expected) in testValues)
        {
            Assert.AreEqual(expected, ImageOrientationHelper.GetFlippedOrientation(original, flip));
        }
    }

    [TestMethod]
    public void GetFlippedCropRectTest()
    {
        Rectangle rect = new(0, 0, 100, 100);
        Size imageSize = new(200, 200);

        Rectangle xFlippedRect = ImageOrientationHelper.GetFlippedCropRect(rect, imageSize, FlipDirection.Horizontal);
        Assert.AreEqual(new Rectangle(100, 0, 100, 100), xFlippedRect);

        Rectangle yFlippedRect = ImageOrientationHelper.GetFlippedCropRect(rect, imageSize, FlipDirection.Vertical);
        Assert.AreEqual(new Rectangle(0, 100, 100, 100), yFlippedRect);
    }

    [TestMethod]
    public void GetRotatedOrientationTest()
    {
        Assert.AreEqual(ImageOrientation.Rotate270FlipNone, ImageOrientationHelper.GetRotatedOrientation(ImageOrientation.RotateNoneFlipNone, RotationDirection.CounterClockwise));
        Assert.AreEqual(ImageOrientation.RotateNoneFlipNone, ImageOrientationHelper.GetRotatedOrientation(ImageOrientation.Rotate90FlipNone, RotationDirection.CounterClockwise));
        Assert.AreEqual(ImageOrientation.Rotate90FlipNone, ImageOrientationHelper.GetRotatedOrientation(ImageOrientation.Rotate180FlipNone, RotationDirection.CounterClockwise));
        Assert.AreEqual(ImageOrientation.Rotate180FlipNone, ImageOrientationHelper.GetRotatedOrientation(ImageOrientation.Rotate270FlipNone, RotationDirection.CounterClockwise));

        Assert.AreEqual(ImageOrientation.Rotate90FlipNone, ImageOrientationHelper.GetRotatedOrientation(ImageOrientation.RotateNoneFlipNone, RotationDirection.Clockwise));
        Assert.AreEqual(ImageOrientation.Rotate180FlipNone, ImageOrientationHelper.GetRotatedOrientation(ImageOrientation.Rotate90FlipNone, RotationDirection.Clockwise));
        Assert.AreEqual(ImageOrientation.Rotate270FlipNone, ImageOrientationHelper.GetRotatedOrientation(ImageOrientation.Rotate180FlipNone, RotationDirection.Clockwise));
        Assert.AreEqual(ImageOrientation.RotateNoneFlipNone, ImageOrientationHelper.GetRotatedOrientation(ImageOrientation.Rotate270FlipNone, RotationDirection.Clockwise));

        Assert.AreEqual(ImageOrientation.Rotate90FlipX, ImageOrientationHelper.GetRotatedOrientation(ImageOrientation.RotateNoneFlipX, RotationDirection.CounterClockwise));
        Assert.AreEqual(ImageOrientation.Rotate180FlipX, ImageOrientationHelper.GetRotatedOrientation(ImageOrientation.Rotate90FlipX, RotationDirection.CounterClockwise));
        Assert.AreEqual(ImageOrientation.Rotate270FlipX, ImageOrientationHelper.GetRotatedOrientation(ImageOrientation.Rotate180FlipX, RotationDirection.CounterClockwise));
        Assert.AreEqual(ImageOrientation.RotateNoneFlipX, ImageOrientationHelper.GetRotatedOrientation(ImageOrientation.Rotate270FlipX, RotationDirection.CounterClockwise));

        Assert.AreEqual(ImageOrientation.Rotate270FlipX, ImageOrientationHelper.GetRotatedOrientation(ImageOrientation.RotateNoneFlipX, RotationDirection.Clockwise));
        Assert.AreEqual(ImageOrientation.RotateNoneFlipX, ImageOrientationHelper.GetRotatedOrientation(ImageOrientation.Rotate90FlipX, RotationDirection.Clockwise));
        Assert.AreEqual(ImageOrientation.Rotate90FlipX, ImageOrientationHelper.GetRotatedOrientation(ImageOrientation.Rotate180FlipX, RotationDirection.Clockwise));
        Assert.AreEqual(ImageOrientation.Rotate180FlipX, ImageOrientationHelper.GetRotatedOrientation(ImageOrientation.Rotate270FlipX, RotationDirection.Clockwise));
    }

    [TestMethod]
    public void IsTurnedTest()
    {
        Assert.IsTrue(ImageOrientationHelper.IsTurned(ImageOrientation.Rotate90FlipNone));
        Assert.IsTrue(ImageOrientationHelper.IsTurned(ImageOrientation.Rotate270FlipNone));
        Assert.IsTrue(ImageOrientationHelper.IsTurned(ImageOrientation.Rotate90FlipX));
        Assert.IsTrue(ImageOrientationHelper.IsTurned(ImageOrientation.Rotate270FlipX));
        Assert.IsFalse(ImageOrientationHelper.IsTurned(ImageOrientation.RotateNoneFlipNone));
        Assert.IsFalse(ImageOrientationHelper.IsTurned(ImageOrientation.Rotate180FlipNone));
        Assert.IsFalse(ImageOrientationHelper.IsTurned(ImageOrientation.RotateNoneFlipX));
        Assert.IsFalse(ImageOrientationHelper.IsTurned(ImageOrientation.Rotate180FlipX));
    }

    [TestMethod]
    public void GetOrientedImageSizeTest()
    {
        int height = 200;
        int width = 300;
        Size imageSize = new(width, height);
        Size imageSizeOriented = new(height, width);
        Assert.AreEqual(imageSize, ImageOrientationHelper.GetOrientedImageSize(imageSize, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(imageSizeOriented, ImageOrientationHelper.GetOrientedImageSize(imageSize, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(imageSize, ImageOrientationHelper.GetOrientedImageSize(imageSize, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(imageSizeOriented, ImageOrientationHelper.GetOrientedImageSize(imageSize, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(imageSize, ImageOrientationHelper.GetOrientedImageSize(imageSize, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(imageSizeOriented, ImageOrientationHelper.GetOrientedImageSize(imageSize, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(imageSize, ImageOrientationHelper.GetOrientedImageSize(imageSize, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(imageSizeOriented, ImageOrientationHelper.GetOrientedImageSize(imageSize, ImageOrientation.Rotate270FlipX));
    }

    [TestMethod]
    public void GetRotationStepsTest()
    {
        Assert.AreEqual(0, ImageOrientationHelper.GetRotationSteps(ImageOrientation.RotateNoneFlipNone, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(1, ImageOrientationHelper.GetRotationSteps(ImageOrientation.RotateNoneFlipNone, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(2, ImageOrientationHelper.GetRotationSteps(ImageOrientation.RotateNoneFlipNone, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(3, ImageOrientationHelper.GetRotationSteps(ImageOrientation.RotateNoneFlipNone, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(0, ImageOrientationHelper.GetRotationSteps(ImageOrientation.RotateNoneFlipNone, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(1, ImageOrientationHelper.GetRotationSteps(ImageOrientation.RotateNoneFlipNone, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(2, ImageOrientationHelper.GetRotationSteps(ImageOrientation.RotateNoneFlipNone, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(3, ImageOrientationHelper.GetRotationSteps(ImageOrientation.RotateNoneFlipNone, ImageOrientation.Rotate270FlipX));

        Assert.AreEqual(3, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate90FlipNone, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(0, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate90FlipNone, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(1, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate90FlipNone, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(2, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate90FlipNone, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(3, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate90FlipNone, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(0, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate90FlipNone, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(1, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate90FlipNone, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(2, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate90FlipNone, ImageOrientation.Rotate270FlipX));

        Assert.AreEqual(2, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate180FlipNone, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(3, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate180FlipNone, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(0, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate180FlipNone, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(1, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate180FlipNone, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(2, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate180FlipNone, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(3, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate180FlipNone, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(0, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate180FlipNone, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(1, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate180FlipNone, ImageOrientation.Rotate270FlipX));

        Assert.AreEqual(1, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate270FlipNone, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(2, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate270FlipNone, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(3, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate270FlipNone, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(0, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate270FlipNone, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(1, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate270FlipNone, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(2, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate270FlipNone, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(3, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate270FlipNone, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(0, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate270FlipNone, ImageOrientation.Rotate270FlipX));

        Assert.AreEqual(3, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate90FlipX, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(0, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate90FlipX, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(1, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate90FlipX, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(2, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate90FlipX, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(3, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate90FlipX, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(0, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate90FlipX, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(1, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate90FlipX, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(2, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate90FlipX, ImageOrientation.Rotate270FlipX));

        Assert.AreEqual(0, ImageOrientationHelper.GetRotationSteps(ImageOrientation.RotateNoneFlipX, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(1, ImageOrientationHelper.GetRotationSteps(ImageOrientation.RotateNoneFlipX, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(2, ImageOrientationHelper.GetRotationSteps(ImageOrientation.RotateNoneFlipX, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(3, ImageOrientationHelper.GetRotationSteps(ImageOrientation.RotateNoneFlipX, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(0, ImageOrientationHelper.GetRotationSteps(ImageOrientation.RotateNoneFlipX, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(1, ImageOrientationHelper.GetRotationSteps(ImageOrientation.RotateNoneFlipX, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(2, ImageOrientationHelper.GetRotationSteps(ImageOrientation.RotateNoneFlipX, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(3, ImageOrientationHelper.GetRotationSteps(ImageOrientation.RotateNoneFlipX, ImageOrientation.Rotate270FlipX));

        Assert.AreEqual(1, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate270FlipX, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(2, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate270FlipX, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(3, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate270FlipX, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(0, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate270FlipX, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(1, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate270FlipX, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(2, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate270FlipX, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(3, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate270FlipX, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(0, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate270FlipX, ImageOrientation.Rotate270FlipX));

        Assert.AreEqual(2, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate180FlipX, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(3, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate180FlipX, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(0, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate180FlipX, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(1, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate180FlipX, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(2, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate180FlipX, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(3, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate180FlipX, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(0, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate180FlipX, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(1, ImageOrientationHelper.GetRotationSteps(ImageOrientation.Rotate180FlipX, ImageOrientation.Rotate270FlipX));
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

        Assert.AreEqual(quad2Portrait, ImageOrientationHelper.ToCanonicalCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(quad2Portrait, ImageOrientationHelper.ToCanonicalCropRect(quad1Landscape, imageSize, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(quad2Portrait, ImageOrientationHelper.ToCanonicalCropRect(quad4Portrait, imageSize, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(quad2Portrait, ImageOrientationHelper.ToCanonicalCropRect(quad3Landscape, imageSize, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(quad2Portrait, ImageOrientationHelper.ToCanonicalCropRect(quad1Portrait, imageSize, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(quad2Portrait, ImageOrientationHelper.ToCanonicalCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(quad2Portrait, ImageOrientationHelper.ToCanonicalCropRect(quad3Portrait, imageSize, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(quad2Portrait, ImageOrientationHelper.ToCanonicalCropRect(quad4Landscape, imageSize, ImageOrientation.Rotate270FlipX));
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

        Assert.AreEqual(quad2Portrait, ImageOrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(quad1Landscape, ImageOrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(quad4Portrait, ImageOrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(quad3Landscape, ImageOrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(quad1Portrait, ImageOrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(quad2Landscape, ImageOrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(quad3Portrait, ImageOrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(quad4Landscape, ImageOrientationHelper.FromCanonicalCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate270FlipX));
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
        // If oldOrientation is turned 90 degrees, the cropRect should be oriented to match, swapping height and width.
        // The imageSize should always be the correctly oriented size of the image, unaffected by rotation.
        // This test assumes that the image is portrait, taller than it is wide.
        // The resulting cropRect should be in the correct placement and size for the new orientation.

        Assert.AreEqual(quad2Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipNone, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(quad1Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipNone, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(quad4Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipNone, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(quad3Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipNone, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(quad1Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipNone, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(quad2Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipNone, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(quad3Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipNone, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(quad4Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipNone, ImageOrientation.Rotate270FlipX));

        Assert.AreEqual(quad3Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate90FlipNone, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(quad2Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate90FlipNone, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(quad1Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate90FlipNone, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(quad4Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate90FlipNone, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(quad4Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate90FlipNone, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(quad1Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate90FlipNone, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(quad2Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate90FlipNone, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(quad3Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate90FlipNone, ImageOrientation.Rotate270FlipX));

        Assert.AreEqual(quad4Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipNone, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(quad3Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipNone, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(quad2Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipNone, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(quad1Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipNone, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(quad3Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipNone, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(quad4Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipNone, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(quad1Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipNone, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(quad2Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipNone, ImageOrientation.Rotate270FlipX));

        Assert.AreEqual(quad1Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate270FlipNone, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(quad4Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate270FlipNone, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(quad3Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate270FlipNone, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(quad2Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate270FlipNone, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(quad2Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate270FlipNone, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(quad3Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate270FlipNone, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(quad4Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate270FlipNone, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(quad1Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate270FlipNone, ImageOrientation.Rotate270FlipX));

        Assert.AreEqual(quad1Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipX, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(quad4Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipX, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(quad3Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipX, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(quad2Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipX, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(quad2Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipX, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(quad3Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipX, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(quad4Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipX, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(quad1Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.RotateNoneFlipX, ImageOrientation.Rotate270FlipX));

        Assert.AreEqual(quad2Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate90FlipX, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(quad1Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate90FlipX, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(quad4Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate90FlipX, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(quad3Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate90FlipX, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(quad1Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate90FlipX, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(quad2Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate90FlipX, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(quad3Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate90FlipX, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(quad4Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate90FlipX, ImageOrientation.Rotate270FlipX));

        Assert.AreEqual(quad3Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipX, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(quad2Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipX, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(quad1Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipX, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(quad4Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipX, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(quad4Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipX, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(quad1Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipX, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(quad2Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipX, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(quad3Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Portrait, imageSize, ImageOrientation.Rotate180FlipX, ImageOrientation.Rotate270FlipX));

        Assert.AreEqual(quad4Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate270FlipX, ImageOrientation.RotateNoneFlipNone));
        Assert.AreEqual(quad3Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate270FlipX, ImageOrientation.Rotate90FlipNone));
        Assert.AreEqual(quad2Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate270FlipX, ImageOrientation.Rotate180FlipNone));
        Assert.AreEqual(quad1Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate270FlipX, ImageOrientation.Rotate270FlipNone));
        Assert.AreEqual(quad3Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate270FlipX, ImageOrientation.RotateNoneFlipX));
        Assert.AreEqual(quad4Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate270FlipX, ImageOrientation.Rotate90FlipX));
        Assert.AreEqual(quad1Portrait, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate270FlipX, ImageOrientation.Rotate180FlipX));
        Assert.AreEqual(quad2Landscape, ImageOrientationHelper.GetOrientedCropRect(quad2Landscape, imageSize, ImageOrientation.Rotate270FlipX, ImageOrientation.Rotate270FlipX));
    }
}
