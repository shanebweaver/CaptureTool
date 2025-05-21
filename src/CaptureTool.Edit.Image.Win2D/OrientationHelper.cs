using System;
using System.Drawing;

namespace CaptureTool.Edit.Image.Win2D;

public static partial class OrientationHelper
{
    public static bool IsTurned(RotateFlipType orientation)
    {
        return
            orientation == RotateFlipType.Rotate90FlipNone ||
            orientation == RotateFlipType.Rotate270FlipNone ||
            orientation == RotateFlipType.Rotate90FlipX ||
            orientation == RotateFlipType.Rotate270FlipX;
    }

    public static bool IsXFlipped(RotateFlipType orientation)
    {
        return
            orientation == RotateFlipType.RotateNoneFlipX ||
            orientation == RotateFlipType.Rotate90FlipX ||
            orientation == RotateFlipType.Rotate180FlipX ||
            orientation == RotateFlipType.Rotate270FlipX;
    }

    public static Size GetOrientedImageSize(Size imageSize, RotateFlipType orientation)
    {
        bool isTurned = IsTurned(orientation);
        int imageWidth = isTurned ? imageSize.Height : imageSize.Width;
        int imageHeight = isTurned ? imageSize.Width : imageSize.Height;
        return new(imageWidth, imageHeight);
    }

    public static RotateFlipType GetFlippedOrientation(RotateFlipType currentOrientation, FlipDirection flipDirection)
    {
        // Flip horizontally
        if (flipDirection == FlipDirection.Horizontal)
        {
            return currentOrientation switch
            {
                RotateFlipType.RotateNoneFlipNone => RotateFlipType.RotateNoneFlipX,
                RotateFlipType.RotateNoneFlipX => RotateFlipType.RotateNoneFlipNone,
                RotateFlipType.Rotate90FlipNone => RotateFlipType.Rotate90FlipX,
                RotateFlipType.Rotate90FlipX => RotateFlipType.Rotate90FlipNone,
                RotateFlipType.Rotate180FlipNone => RotateFlipType.Rotate180FlipX,
                RotateFlipType.Rotate180FlipX => RotateFlipType.Rotate180FlipNone,
                RotateFlipType.Rotate270FlipNone => RotateFlipType.Rotate270FlipX,
                RotateFlipType.Rotate270FlipX => RotateFlipType.Rotate270FlipNone,
                _ => throw new NotImplementedException("Unexpected RotateFlipType value"),
            };
        }
        // Flip vertically
        else
        {
            return currentOrientation switch
            {
                RotateFlipType.RotateNoneFlipNone => RotateFlipType.RotateNoneFlipY,
                RotateFlipType.RotateNoneFlipY => RotateFlipType.RotateNoneFlipNone,
                RotateFlipType.Rotate90FlipNone => RotateFlipType.Rotate90FlipY,
                RotateFlipType.Rotate90FlipY => RotateFlipType.Rotate90FlipNone,
                RotateFlipType.Rotate180FlipNone => RotateFlipType.Rotate180FlipY,
                RotateFlipType.Rotate180FlipY => RotateFlipType.Rotate180FlipNone,
                RotateFlipType.Rotate270FlipNone => RotateFlipType.Rotate270FlipY,
                RotateFlipType.Rotate270FlipY => RotateFlipType.Rotate270FlipNone,
                _ => throw new NotImplementedException("Unexpected RotateFlipType value"),
            };
        }
    }

    public static RotateFlipType GetRotatedOrientation(RotateFlipType orientation, RotationDirection rotationDirection)
    {
        return rotationDirection switch
        {
            RotationDirection.Clockwise => orientation switch
            {
                RotateFlipType.RotateNoneFlipNone => RotateFlipType.Rotate90FlipNone,
                RotateFlipType.Rotate90FlipNone => RotateFlipType.Rotate180FlipNone,
                RotateFlipType.Rotate180FlipNone => RotateFlipType.Rotate270FlipNone,
                RotateFlipType.Rotate270FlipNone => RotateFlipType.RotateNoneFlipNone,

                RotateFlipType.RotateNoneFlipX => RotateFlipType.Rotate90FlipX,
                RotateFlipType.Rotate90FlipX => RotateFlipType.Rotate180FlipX,
                RotateFlipType.Rotate180FlipX => RotateFlipType.Rotate270FlipX,
                RotateFlipType.Rotate270FlipX => RotateFlipType.RotateNoneFlipX,

                _ => throw new NotImplementedException("Unexpected RotateFlipType value"),
            },

            RotationDirection.CounterClockwise => orientation switch
            {
                RotateFlipType.RotateNoneFlipNone => RotateFlipType.Rotate270FlipNone,
                RotateFlipType.Rotate90FlipNone => RotateFlipType.RotateNoneFlipNone,
                RotateFlipType.Rotate180FlipNone => RotateFlipType.Rotate90FlipNone,
                RotateFlipType.Rotate270FlipNone => RotateFlipType.Rotate180FlipNone,

                RotateFlipType.RotateNoneFlipX => RotateFlipType.Rotate270FlipX,
                RotateFlipType.Rotate90FlipX => RotateFlipType.RotateNoneFlipX,
                RotateFlipType.Rotate180FlipX => RotateFlipType.Rotate90FlipX,
                RotateFlipType.Rotate270FlipX => RotateFlipType.Rotate180FlipX,

                _ => throw new NotImplementedException("Unexpected RotateFlipType value"),
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

    public static int GetRotationSteps(RotateFlipType from, RotateFlipType to)
    {
        int[] angles = [
            0,   // RotateNoneFlipNone
            90,  // Rotate90FlipNone
            180, // Rotate180FlipNone
            270, // Rotate270FlipNone
            0,   // RotateNoneFlipX
            90,  // Rotate90FlipX
            180, // Rotate180FlipX
            270, // Rotate270FlipX
        ];
        int fromIdx = (int)from % 8;
        int toIdx = (int)to % 8;
        int delta = (angles[toIdx] - angles[fromIdx] + 360) % 360;
        return (delta / 90) % 4;
    }

    public static Rectangle GetOrientedCropRect(Rectangle cropRect, Size imageSize, RotateFlipType from, RotateFlipType to)
    {
        // Get the canonical crop rectangle based on the original orientation
        Rectangle canonicalCropRect = ToCanonicalCropRect(cropRect, imageSize, from);
        
        // Calculate the new crop rectangle based on the target orientation
        Rectangle orientedCropRect = FromCanonicalCropRect(canonicalCropRect, imageSize, to);

        // Clamp to image bounds
        //orientedCropRect.X = Math.Max(0, Math.Min(orientedCropRect.X, imageSize.Width - orientedCropRect.Width));
        //orientedCropRect.Y = Math.Max(0, Math.Min(orientedCropRect.Y, imageSize.Height - orientedCropRect.Height));
        //orientedCropRect.Width = Math.Min(orientedCropRect.Width, imageSize.Width - orientedCropRect.X);
        //orientedCropRect.Height = Math.Min(orientedCropRect.Height, imageSize.Height - orientedCropRect.Y);
        return orientedCropRect;
    }

    public static Rectangle ToCanonicalCropRect(Rectangle cropRect, Size imageSize, RotateFlipType orientation)
    {
        // The canonical orientation is RotateNoneFlipNone (no rotation, no flip).
        // This method transforms the cropRect from the given orientation to the canonical orientation.
        Rectangle rect = cropRect;
        Size size = imageSize;

        // If the orientation is turned, swap width and height
        bool turned = orientation == RotateFlipType.Rotate90FlipNone ||
                      orientation == RotateFlipType.Rotate270FlipNone ||
                      orientation == RotateFlipType.Rotate90FlipX ||
                      orientation == RotateFlipType.Rotate270FlipX;

        if (turned)
            size = new Size(size.Height, size.Width);

        // Undo flip
        switch (orientation)
        {
            case RotateFlipType.RotateNoneFlipX:
            case RotateFlipType.Rotate90FlipX:
            case RotateFlipType.Rotate180FlipX:
            case RotateFlipType.Rotate270FlipX:
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
            case RotateFlipType.Rotate90FlipNone:
            case RotateFlipType.Rotate90FlipX:
                rect = new Rectangle(
                    rect.Y,
                    size.Width - rect.X - rect.Width,
                    rect.Height,
                    rect.Width
                );
                break;
            case RotateFlipType.Rotate180FlipNone:
            case RotateFlipType.Rotate180FlipX:
                rect = new Rectangle(
                    size.Width - rect.X - rect.Width,
                    size.Height - rect.Y - rect.Height,
                    rect.Width,
                    rect.Height
                );
                break;
            case RotateFlipType.Rotate270FlipNone:
            case RotateFlipType.Rotate270FlipX:
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

    public static Rectangle FromCanonicalCropRect(Rectangle canonical, Size canonicalImageSize, RotateFlipType orientation)
    {
        Rectangle result;
        switch (orientation)
        {
            case RotateFlipType.RotateNoneFlipNone:
                result = canonical;
                break;
            case RotateFlipType.Rotate90FlipNone:
                result = new Rectangle(
                    canonicalImageSize.Height - canonical.Y - canonical.Height,
                    canonical.X,
                    canonical.Height,
                    canonical.Width);
                break;
            case RotateFlipType.Rotate180FlipNone:
                result = new Rectangle(
                    canonicalImageSize.Width - canonical.X - canonical.Width,
                    canonicalImageSize.Height - canonical.Y - canonical.Height,
                    canonical.Width,
                    canonical.Height);
                break;
            case RotateFlipType.Rotate270FlipNone:
                result = new Rectangle(
                    canonical.Y,
                    canonicalImageSize.Width - canonical.X - canonical.Width,
                    canonical.Height,
                    canonical.Width);
                break;
            case RotateFlipType.RotateNoneFlipX:
                result = new Rectangle(
                    canonicalImageSize.Width - canonical.X - canonical.Width,
                    canonical.Y,
                    canonical.Width,
                    canonical.Height);
                break;
            case RotateFlipType.Rotate90FlipX:
                result = new Rectangle(
                    canonical.Y,
                    canonical.X,
                    canonical.Height,
                    canonical.Width);
                break;
            case RotateFlipType.Rotate180FlipX:
                result = new Rectangle(
                    canonical.X,
                    canonicalImageSize.Height - canonical.Y - canonical.Height,
                    canonical.Width,
                    canonical.Height);
                break;
            case RotateFlipType.Rotate270FlipX:
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
}
