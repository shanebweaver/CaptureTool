using System;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Edit;

public static partial class OrientationHelper
{
    public static bool IsTurned(Orientation orientation)
    {
        return
            orientation == Orientation.Rotate90FlipNone ||
            orientation == Orientation.Rotate270FlipNone ||
            orientation == Orientation.Rotate90FlipX ||
            orientation == Orientation.Rotate270FlipX;
    }

    public static bool IsXFlipped(Orientation orientation)
    {
        return
            orientation == Orientation.RotateNoneFlipX ||
            orientation == Orientation.Rotate90FlipX ||
            orientation == Orientation.Rotate180FlipX ||
            orientation == Orientation.Rotate270FlipX;
    }

    public static Size GetOrientedImageSize(Size imageSize, Orientation orientation)
    {
        bool isTurned = IsTurned(orientation);
        int imageWidth = isTurned ? imageSize.Height : imageSize.Width;
        int imageHeight = isTurned ? imageSize.Width : imageSize.Height;
        return new(imageWidth, imageHeight);
    }

    public static Orientation GetFlippedOrientation(Orientation currentOrientation, FlipDirection flipDirection)
    {
        // Flip horizontally
        if (flipDirection == FlipDirection.Horizontal)
        {
            return currentOrientation switch
            {
                Orientation.RotateNoneFlipNone => Orientation.RotateNoneFlipX,
                Orientation.RotateNoneFlipX => Orientation.RotateNoneFlipNone,
                Orientation.Rotate90FlipNone => Orientation.Rotate90FlipX,
                Orientation.Rotate90FlipX => Orientation.Rotate90FlipNone,
                Orientation.Rotate180FlipNone => Orientation.Rotate180FlipX,
                Orientation.Rotate180FlipX => Orientation.Rotate180FlipNone,
                Orientation.Rotate270FlipNone => Orientation.Rotate270FlipX,
                Orientation.Rotate270FlipX => Orientation.Rotate270FlipNone,
                _ => throw new NotImplementedException("Unexpected orientation value"),
            };
        }
        // Flip vertically
        else
        {
            return currentOrientation switch
            {
                Orientation.RotateNoneFlipNone => Orientation.Rotate180FlipX,
                Orientation.Rotate180FlipX => Orientation.RotateNoneFlipNone,
                Orientation.Rotate90FlipNone => Orientation.Rotate270FlipX,
                Orientation.Rotate270FlipX => Orientation.Rotate90FlipNone,
                Orientation.Rotate180FlipNone => Orientation.RotateNoneFlipX,
                Orientation.RotateNoneFlipX => Orientation.Rotate180FlipNone,
                Orientation.Rotate270FlipNone => Orientation.Rotate90FlipX,
                Orientation.Rotate90FlipX => Orientation.Rotate270FlipNone,
                _ => throw new NotImplementedException("Unexpected orientation value"),
            };
        }
    }

    public static Orientation GetRotatedOrientation(Orientation orientation, RotationDirection rotationDirection)
    {
        return rotationDirection switch
        {
            RotationDirection.Clockwise => orientation switch
            {
                Orientation.RotateNoneFlipNone => Orientation.Rotate90FlipNone,
                Orientation.Rotate90FlipNone => Orientation.Rotate180FlipNone,
                Orientation.Rotate180FlipNone => Orientation.Rotate270FlipNone,
                Orientation.Rotate270FlipNone => Orientation.RotateNoneFlipNone,

                Orientation.RotateNoneFlipX => Orientation.Rotate270FlipX,
                Orientation.Rotate90FlipX => Orientation.RotateNoneFlipX,
                Orientation.Rotate180FlipX => Orientation.Rotate90FlipX,
                Orientation.Rotate270FlipX => Orientation.Rotate180FlipX,

                _ => throw new NotImplementedException("Unexpected orientation value"),
            },

            RotationDirection.CounterClockwise => orientation switch
            {
                Orientation.RotateNoneFlipNone => Orientation.Rotate270FlipNone,
                Orientation.Rotate90FlipNone => Orientation.RotateNoneFlipNone,
                Orientation.Rotate180FlipNone => Orientation.Rotate90FlipNone,
                Orientation.Rotate270FlipNone => Orientation.Rotate180FlipNone,

                Orientation.RotateNoneFlipX => Orientation.Rotate90FlipX,
                Orientation.Rotate90FlipX => Orientation.Rotate180FlipX,
                Orientation.Rotate180FlipX => Orientation.Rotate270FlipX,
                Orientation.Rotate270FlipX => Orientation.RotateNoneFlipX,

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

    public static int GetRotationSteps(Orientation from, Orientation to)
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

    public static Rectangle GetOrientedCropRect(Rectangle cropRect, Size imageSize, Orientation from, Orientation to)
    {
        // Get the canonical crop rectangle based on the original orientation
        Rectangle canonicalCropRect = ToCanonicalCropRect(cropRect, imageSize, from);
        
        // Calculate the new crop rectangle based on the target orientation
        Rectangle orientedCropRect = FromCanonicalCropRect(canonicalCropRect, imageSize, to);

        return orientedCropRect;
    }

    public static Rectangle ToCanonicalCropRect(Rectangle cropRect, Size imageSize, Orientation orientation)
    {
        // The canonical orientation is RotateNoneFlipNone (no rotation, no flip).
        // This method transforms the cropRect from the given orientation to the canonical orientation.
        Rectangle rect = cropRect;
        Size size = imageSize;

        // If the orientation is turned, swap width and height
        bool turned = orientation == Orientation.Rotate90FlipNone ||
                      orientation == Orientation.Rotate270FlipNone ||
                      orientation == Orientation.Rotate90FlipX ||
                      orientation == Orientation.Rotate270FlipX;

        if (turned)
            size = new Size(size.Height, size.Width);

        // Undo flip
        switch (orientation)
        {
            case Orientation.RotateNoneFlipX:
            case Orientation.Rotate90FlipX:
            case Orientation.Rotate180FlipX:
            case Orientation.Rotate270FlipX:
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
            case Orientation.Rotate90FlipNone:
            case Orientation.Rotate90FlipX:
                rect = new Rectangle(
                    rect.Y,
                    size.Width - rect.X - rect.Width,
                    rect.Height,
                    rect.Width
                );
                break;
            case Orientation.Rotate180FlipNone:
            case Orientation.Rotate180FlipX:
                rect = new Rectangle(
                    size.Width - rect.X - rect.Width,
                    size.Height - rect.Y - rect.Height,
                    rect.Width,
                    rect.Height
                );
                break;
            case Orientation.Rotate270FlipNone:
            case Orientation.Rotate270FlipX:
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

    public static Rectangle FromCanonicalCropRect(Rectangle canonical, Size canonicalImageSize, Orientation orientation)
    {
        Rectangle result;
        switch (orientation)
        {
            case Orientation.RotateNoneFlipNone:
                result = canonical;
                break;
            case Orientation.Rotate90FlipNone:
                result = new Rectangle(
                    canonicalImageSize.Height - canonical.Y - canonical.Height,
                    canonical.X,
                    canonical.Height,
                    canonical.Width);
                break;
            case Orientation.Rotate180FlipNone:
                result = new Rectangle(
                    canonicalImageSize.Width - canonical.X - canonical.Width,
                    canonicalImageSize.Height - canonical.Y - canonical.Height,
                    canonical.Width,
                    canonical.Height);
                break;
            case Orientation.Rotate270FlipNone:
                result = new Rectangle(
                    canonical.Y,
                    canonicalImageSize.Width - canonical.X - canonical.Width,
                    canonical.Height,
                    canonical.Width);
                break;
            case Orientation.RotateNoneFlipX:
                result = new Rectangle(
                    canonicalImageSize.Width - canonical.X - canonical.Width,
                    canonical.Y,
                    canonical.Width,
                    canonical.Height);
                break;
            case Orientation.Rotate90FlipX:
                result = new Rectangle(
                    canonical.Y,
                    canonical.X,
                    canonical.Height,
                    canonical.Width);
                break;
            case Orientation.Rotate180FlipX:
                result = new Rectangle(
                    canonical.X,
                    canonicalImageSize.Height - canonical.Y - canonical.Height,
                    canonical.Width,
                    canonical.Height);
                break;
            case Orientation.Rotate270FlipX:
                result = new Rectangle(
                    canonicalImageSize.Height - canonical.Y - canonical.Height,
                    canonicalImageSize.Width - canonical.X - canonical.Width,
                    canonical.Height,
                    canonical.Width);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(orientation));
        }
        return result;
    }

    public static Matrix3x2 CalculateRenderTransform(Rectangle cropRect, Size imageSize, Orientation orientation)
    {
        Matrix3x2 transform = Matrix3x2.Identity;
        double imageWidth = imageSize.Width;
        double imageHeight = imageSize.Height;

        // Apply rotation
        double maxDimension = Math.Max(imageHeight, imageWidth);
        Vector2 rotationPoint = new((float)maxDimension / 2, (float)maxDimension / 2);

        switch (orientation)
        {
            case Orientation.Rotate90FlipNone:
            case Orientation.Rotate90FlipX:
            case Orientation.Rotate270FlipX:
                transform *= Matrix3x2.CreateRotation(GetRadians(90), rotationPoint);
                break;

            case Orientation.Rotate180FlipNone:
            case Orientation.Rotate180FlipX:
            case Orientation.RotateNoneFlipX:
                transform *= Matrix3x2.CreateRotation(GetRadians(180), rotationPoint);
                break;

            case Orientation.Rotate270FlipNone:
                transform *= Matrix3x2.CreateRotation(GetRadians(270), rotationPoint);
                break;
        }

        // Apply translation to reposition at 0,0
        bool isLandscape = imageWidth > imageHeight;
        float heightLessWidth = (float)(imageHeight - imageWidth);
        float widthLessHeight = (float)(imageWidth - imageHeight);
        switch (orientation)
        {
            case Orientation.Rotate90FlipNone:
            case Orientation.Rotate90FlipX:
            case Orientation.Rotate270FlipX:
                if (isLandscape)
                {
                    transform *= Matrix3x2.CreateTranslation(heightLessWidth, 0);
                }
                break;

            case Orientation.Rotate180FlipNone:
            case Orientation.Rotate180FlipX:
            case Orientation.RotateNoneFlipX:
                if (isLandscape)
                {
                    transform *= Matrix3x2.CreateTranslation(0, heightLessWidth);
                }
                else
                {
                    transform *= Matrix3x2.CreateTranslation(widthLessHeight, 0);
                }
                break;

            case Orientation.Rotate270FlipNone:
                if (!isLandscape)
                {
                    transform *= Matrix3x2.CreateTranslation(0, widthLessHeight);
                }
                break;
        }

        // Apply flipping
        switch (orientation)
        {
            case Orientation.RotateNoneFlipX:
                transform *= Matrix3x2.CreateScale(1, -1, new((float)imageWidth / 2, (float)imageHeight / 2));
                break;

            case Orientation.Rotate270FlipX:
                transform *= Matrix3x2.CreateScale(1, -1, new((float)imageHeight / 2, (float)imageWidth / 2));
                break;

            case Orientation.Rotate180FlipX:
                transform *= Matrix3x2.CreateScale(-1, 1, new((float)imageWidth / 2, (float)imageHeight / 2));
                break;

            case Orientation.Rotate90FlipX:
                transform *= Matrix3x2.CreateScale(-1, 1, new((float)imageHeight / 2, (float)imageWidth / 2));
                break;
        }

        // Apply cropping
        transform *= Matrix3x2.CreateTranslation(-(float)cropRect.X, -(float)cropRect.Y);

        return transform;
    }

    public static float GetRadians(double angle)
    {
        return (float)(Math.PI * angle / 180.0);
    }
}
