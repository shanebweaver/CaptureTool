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

    public static Rectangle GetOrientedCropRect(Rectangle cropRect, Size imageSize, RotateFlipType oldOrientation, RotateFlipType newOrientation)
    {
        Rectangle oldRect = cropRect;
        int oldWidth = imageSize.Width;
        int oldHeight = imageSize.Height;

        // Helper: get rotation steps (in 90-degree increments, clockwise)
        static int GetRotationSteps(RotateFlipType from, RotateFlipType to)
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

        // Helper: get flipX and flipY for a given orientation
        static (bool flipX, bool flipY) GetFlips(RotateFlipType orientation)
        {
            return orientation switch
            {
                RotateFlipType.RotateNoneFlipNone => (false, false),
                RotateFlipType.Rotate90FlipNone => (false, false),
                RotateFlipType.Rotate180FlipNone => (false, false),
                RotateFlipType.Rotate270FlipNone => (false, false),
                RotateFlipType.RotateNoneFlipX => (true, false),
                RotateFlipType.Rotate90FlipX => (true, false),
                RotateFlipType.Rotate180FlipX => (true, false),
                RotateFlipType.Rotate270FlipX => (true, false),
                _ => (false, false)
            };
        }

        int steps = GetRotationSteps(oldOrientation, newOrientation);

        // Calculate net flip (X and Y) between old and new orientation
        (bool oldFlipX, bool oldFlipY) = GetFlips(oldOrientation);
        (bool newFlipX, bool newFlipY) = GetFlips(newOrientation);
        bool flipX = oldFlipX != newFlipX;
        bool flipY = oldFlipY != newFlipY;

        int x = oldRect.X, y = oldRect.Y, w = oldRect.Width, h = oldRect.Height;
        int width = oldWidth, height = oldHeight;

        // Apply rotation
        if (steps == 1) // 90° CW
        {
            int newX = height - (y + h);
            int newY = x;
            int newW = h;
            int newH = w;
            x = newX;
            y = newY;
            w = newW;
            h = newH;
            (height, width) = (width, height);
        }
        else if (steps == 2) // 180°
        {
            int newX = width - (x + w);
            int newY = height - (y + h);
            x = newX;
            y = newY;
            // w and h stay the same
        }
        else if (steps == 3) // 270° CW (or 90° CCW)
        {
            int newX = y;
            int newY = width - (x + w);
            int newW = h;
            int newH = w;
            x = newX;
            y = newY;
            w = newW;
            h = newH;
            (height, width) = (width, height);
        }
        // steps == 0: no rotation

        // Apply flipping
        if (flipX)
        {
            x = width - (x + w);
        }
        if (flipY)
        {
            y = height - (y + h);
        }

        // Clamp to new image bounds
        x = Math.Max(0, Math.Min(x, width - w));
        y = Math.Max(0, Math.Min(y, height - h));
        w = Math.Min(w, width - x);
        h = Math.Min(h, height - y);

        return new(x, y, w, h);
    }
}
