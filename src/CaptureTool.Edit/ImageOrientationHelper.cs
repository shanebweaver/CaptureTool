using System;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Edit;

public static partial class ImageOrientationHelper
{
    public static bool IsTurned(ImageOrientation orientation)
    {
        return
            orientation == ImageOrientation.Rotate90FlipNone ||
            orientation == ImageOrientation.Rotate270FlipNone ||
            orientation == ImageOrientation.Rotate90FlipX ||
            orientation == ImageOrientation.Rotate270FlipX;
    }

    public static Size GetOrientedImageSize(Size imageSize, ImageOrientation orientation)
    {
        bool isTurned = IsTurned(orientation);
        int imageWidth = isTurned ? imageSize.Height : imageSize.Width;
        int imageHeight = isTurned ? imageSize.Width : imageSize.Height;
        return new(imageWidth, imageHeight);
    }

    public static ImageOrientation GetFlippedOrientation(ImageOrientation currentOrientation, FlipDirection flipDirection)
    {
        // Flip horizontally
        if (flipDirection == FlipDirection.Horizontal)
        {
            return currentOrientation switch
            {
                ImageOrientation.RotateNoneFlipNone => ImageOrientation.RotateNoneFlipX,
                ImageOrientation.RotateNoneFlipX => ImageOrientation.RotateNoneFlipNone,
                ImageOrientation.Rotate90FlipNone => ImageOrientation.Rotate90FlipX,
                ImageOrientation.Rotate90FlipX => ImageOrientation.Rotate90FlipNone,
                ImageOrientation.Rotate180FlipNone => ImageOrientation.Rotate180FlipX,
                ImageOrientation.Rotate180FlipX => ImageOrientation.Rotate180FlipNone,
                ImageOrientation.Rotate270FlipNone => ImageOrientation.Rotate270FlipX,
                ImageOrientation.Rotate270FlipX => ImageOrientation.Rotate270FlipNone,
                _ => throw new NotImplementedException("Unexpected orientation value"),
            };
        }
        // Flip vertically
        else
        {
            return currentOrientation switch
            {
                ImageOrientation.RotateNoneFlipNone => ImageOrientation.Rotate180FlipX,
                ImageOrientation.Rotate180FlipX => ImageOrientation.RotateNoneFlipNone,
                ImageOrientation.Rotate90FlipNone => ImageOrientation.Rotate270FlipX,
                ImageOrientation.Rotate270FlipX => ImageOrientation.Rotate90FlipNone,
                ImageOrientation.Rotate180FlipNone => ImageOrientation.RotateNoneFlipX,
                ImageOrientation.RotateNoneFlipX => ImageOrientation.Rotate180FlipNone,
                ImageOrientation.Rotate270FlipNone => ImageOrientation.Rotate90FlipX,
                ImageOrientation.Rotate90FlipX => ImageOrientation.Rotate270FlipNone,
                _ => throw new NotImplementedException("Unexpected orientation value"),
            };
        }
    }

    public static ImageOrientation GetRotatedOrientation(ImageOrientation orientation, RotationDirection rotationDirection)
    {
        return rotationDirection switch
        {
            RotationDirection.Clockwise => orientation switch
            {
                ImageOrientation.RotateNoneFlipNone => ImageOrientation.Rotate90FlipNone,
                ImageOrientation.Rotate90FlipNone => ImageOrientation.Rotate180FlipNone,
                ImageOrientation.Rotate180FlipNone => ImageOrientation.Rotate270FlipNone,
                ImageOrientation.Rotate270FlipNone => ImageOrientation.RotateNoneFlipNone,

                ImageOrientation.RotateNoneFlipX => ImageOrientation.Rotate270FlipX,
                ImageOrientation.Rotate90FlipX => ImageOrientation.RotateNoneFlipX,
                ImageOrientation.Rotate180FlipX => ImageOrientation.Rotate90FlipX,
                ImageOrientation.Rotate270FlipX => ImageOrientation.Rotate180FlipX,

                _ => throw new NotImplementedException("Unexpected orientation value"),
            },

            RotationDirection.CounterClockwise => orientation switch
            {
                ImageOrientation.RotateNoneFlipNone => ImageOrientation.Rotate270FlipNone,
                ImageOrientation.Rotate90FlipNone => ImageOrientation.RotateNoneFlipNone,
                ImageOrientation.Rotate180FlipNone => ImageOrientation.Rotate90FlipNone,
                ImageOrientation.Rotate270FlipNone => ImageOrientation.Rotate180FlipNone,

                ImageOrientation.RotateNoneFlipX => ImageOrientation.Rotate90FlipX,
                ImageOrientation.Rotate90FlipX => ImageOrientation.Rotate180FlipX,
                ImageOrientation.Rotate180FlipX => ImageOrientation.Rotate270FlipX,
                ImageOrientation.Rotate270FlipX => ImageOrientation.RotateNoneFlipX,

                _ => throw new NotImplementedException("Unexpected orientation value"),
            },

            _ => throw new NotImplementedException("Unexpected RotationDirection value"),
        };
    }

    public static Rectangle GetFlippedCropRect(Rectangle cropRect, Size imageSize, FlipDirection flipDirection)
    {
        int x = cropRect.X;
        int y = cropRect.Y;
        int w = cropRect.Width;
        int h = cropRect.Height;

        // Use the current orientation to determine image dimensions
        var imageWidth = imageSize.Width;
        var imageHeight = imageSize.Height;

        if (flipDirection == FlipDirection.Horizontal)
        {
            // Flip horizontally: move crop from left to right
            x = imageWidth - (x + w);
        }
        else
        {
            // Flip vertically: move crop from top to bottom
            y = imageHeight - (y + h);
        }

        // Clamp to image bounds
        x = Math.Max(0, Math.Min(x, imageWidth - w));
        y = Math.Max(0, Math.Min(y, imageHeight - h));
        w = Math.Min(w, imageWidth - x);
        h = Math.Min(h, imageHeight - y);

        return new(x, y, w, h);
    }

    public static int GetRotationSteps(ImageOrientation from, ImageOrientation to)
    {
        int[] angles = [
            0,
            1,//90,
            2,//180,
            3,//270,
        ];
        int fromIdx = (int)from % 4;
        int toIdx = (int)to % 4;
        int delta = (angles[toIdx] - angles[fromIdx] + 4) % 4;
        return delta;
    }

    public static Rectangle GetOrientedCropRect(Rectangle cropRect, Size imageSize, ImageOrientation from, ImageOrientation to)
    {
        // Get the canonical crop rectangle based on the original orientation
        Rectangle canonicalCropRect = ToCanonicalCropRect(cropRect, imageSize, from);
        
        // Calculate the new crop rectangle based on the target orientation
        Rectangle orientedCropRect = FromCanonicalCropRect(canonicalCropRect, imageSize, to);

        return orientedCropRect;
    }

    public static Rectangle ToCanonicalCropRect(Rectangle cropRect, Size imageSize, ImageOrientation orientation)
    {
        // The canonical orientation is RotateNoneFlipNone (no rotation, no flip).
        // This method transforms the cropRect from the given orientation to the canonical orientation.
        Rectangle rect = cropRect;
        Size size = imageSize;

        // If the orientation is turned, swap width and height
        bool turned = orientation == ImageOrientation.Rotate90FlipNone ||
                      orientation == ImageOrientation.Rotate270FlipNone ||
                      orientation == ImageOrientation.Rotate90FlipX ||
                      orientation == ImageOrientation.Rotate270FlipX;

        if (turned)
            size = new Size(size.Height, size.Width);

        // Undo flip
        switch (orientation)
        {
            case ImageOrientation.RotateNoneFlipX:
            case ImageOrientation.Rotate90FlipX:
            case ImageOrientation.Rotate180FlipX:
            case ImageOrientation.Rotate270FlipX:
                rect = new Rectangle(
                    size.Width - rect.X - rect.Width,
                    rect.Y,
                    rect.Width,
                    rect.Height
                );
                break;
        }

        // Undo rotation
        switch (orientation)
        {
            case ImageOrientation.Rotate90FlipNone:
            case ImageOrientation.Rotate90FlipX:
                rect = new Rectangle(
                    rect.Y,
                    size.Width - rect.X - rect.Width,
                    rect.Height,
                    rect.Width
                );
                break;
            case ImageOrientation.Rotate180FlipNone:
            case ImageOrientation.Rotate180FlipX:
                rect = new Rectangle(
                    size.Width - rect.X - rect.Width,
                    size.Height - rect.Y - rect.Height,
                    rect.Width,
                    rect.Height
                );
                break;
            case ImageOrientation.Rotate270FlipNone:
            case ImageOrientation.Rotate270FlipX:
                rect = new Rectangle(
                    size.Height - rect.Y - rect.Height,
                    rect.X,
                    rect.Height,
                    rect.Width
                );
                break;
        }

        return rect;
    }

    public static Rectangle FromCanonicalCropRect(Rectangle canonical, Size canonicalImageSize, ImageOrientation orientation)
    {
        var result = orientation switch
        {
            ImageOrientation.RotateNoneFlipNone => canonical,
            ImageOrientation.Rotate90FlipNone => new Rectangle(
                canonicalImageSize.Height - canonical.Y - canonical.Height,
                canonical.X,
                canonical.Height,
                canonical.Width),
            ImageOrientation.Rotate180FlipNone => new Rectangle(
                canonicalImageSize.Width - canonical.X - canonical.Width,
                canonicalImageSize.Height - canonical.Y - canonical.Height,
                canonical.Width,
                canonical.Height),
            ImageOrientation.Rotate270FlipNone => new Rectangle(
                canonical.Y,
                canonicalImageSize.Width - canonical.X - canonical.Width,
                canonical.Height,
                canonical.Width),
            ImageOrientation.RotateNoneFlipX => new Rectangle(
                canonicalImageSize.Width - canonical.X - canonical.Width,
                canonical.Y,
                canonical.Width,
                canonical.Height),
            ImageOrientation.Rotate90FlipX => new Rectangle(
                canonical.Y,
                canonical.X,
                canonical.Height,
                canonical.Width),
            ImageOrientation.Rotate180FlipX => new Rectangle(
                canonical.X,
                canonicalImageSize.Height - canonical.Y - canonical.Height,
                canonical.Width,
                canonical.Height),
            ImageOrientation.Rotate270FlipX => new Rectangle(
                canonicalImageSize.Height - canonical.Y - canonical.Height,
                canonicalImageSize.Width - canonical.X - canonical.Width,
                canonical.Height,
                canonical.Width),
            _ => throw new ArgumentOutOfRangeException(nameof(orientation)),
        };
        return result;
    }

    public static Matrix3x2 CalculateRenderTransform(Size imageSize, ImageOrientation orientation, float scale = 1f)
    {
        Matrix3x2 transform = Matrix3x2.Identity;
        double imageWidth = imageSize.Width;
        double imageHeight = imageSize.Height;

        // Apply rotation
        double maxDimension = Math.Max(imageHeight, imageWidth);
        Vector2 rotationPoint = new((float)maxDimension / 2, (float)maxDimension / 2);

        switch (orientation)
        {
            case ImageOrientation.Rotate90FlipNone:
            case ImageOrientation.Rotate90FlipX:
            case ImageOrientation.Rotate270FlipX:
                transform *= Matrix3x2.CreateRotation(GetRadians(90), rotationPoint);
                break;

            case ImageOrientation.Rotate180FlipNone:
            case ImageOrientation.Rotate180FlipX:
            case ImageOrientation.RotateNoneFlipX:
                transform *= Matrix3x2.CreateRotation(GetRadians(180), rotationPoint);
                break;

            case ImageOrientation.Rotate270FlipNone:
                transform *= Matrix3x2.CreateRotation(GetRadians(270), rotationPoint);
                break;
        }

        // Apply translation to reposition at 0,0
        bool isLandscape = imageWidth > imageHeight;
        float heightLessWidth = (float)(imageHeight - imageWidth);
        float widthLessHeight = (float)(imageWidth - imageHeight);
        switch (orientation)
        {
            case ImageOrientation.Rotate90FlipNone:
            case ImageOrientation.Rotate90FlipX:
            case ImageOrientation.Rotate270FlipX:
                if (isLandscape)
                {
                    transform *= Matrix3x2.CreateTranslation(heightLessWidth, 0);
                }
                break;

            case ImageOrientation.Rotate180FlipNone:
            case ImageOrientation.Rotate180FlipX:
            case ImageOrientation.RotateNoneFlipX:
                if (isLandscape)
                {
                    transform *= Matrix3x2.CreateTranslation(0, heightLessWidth);
                }
                else
                {
                    transform *= Matrix3x2.CreateTranslation(widthLessHeight, 0);
                }
                break;

            case ImageOrientation.Rotate270FlipNone:
                if (!isLandscape)
                {
                    transform *= Matrix3x2.CreateTranslation(0, widthLessHeight);
                }
                break;
        }

        // Apply flipping
        switch (orientation)
        {
            case ImageOrientation.RotateNoneFlipX:
                transform *= Matrix3x2.CreateScale(1, -1, new((float)imageWidth / 2, (float)imageHeight / 2));
                break;

            case ImageOrientation.Rotate270FlipX:
                transform *= Matrix3x2.CreateScale(1, -1, new((float)imageHeight / 2, (float)imageWidth / 2));
                break;

            case ImageOrientation.Rotate180FlipX:
                transform *= Matrix3x2.CreateScale(-1, 1, new((float)imageWidth / 2, (float)imageHeight / 2));
                break;

            case ImageOrientation.Rotate90FlipX:
                transform *= Matrix3x2.CreateScale(-1, 1, new((float)imageHeight / 2, (float)imageWidth / 2));
                break;
        }

        // Apply scaling
        transform *= Matrix3x2.CreateScale(scale);

        return transform;
    }

    public static float GetRadians(double angle)
    {
        return (float)(Math.PI * angle / 180.0);
    }
}
