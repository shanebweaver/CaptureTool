using System.Drawing;

namespace CaptureTool.Domain.Edit;

public static class ImageOrientationGeometry
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

        int imageWidth = imageSize.Width;
        int imageHeight = imageSize.Height;

        if (flipDirection == FlipDirection.Horizontal)
        {
            x = imageWidth - (x + w);
        }
        else
        {
            y = imageHeight - (y + h);
        }

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
            1,
            2,
            3,
        ];
        int fromIdx = (int)from % 4;
        int toIdx = (int)to % 4;
        int delta = (angles[toIdx] - angles[fromIdx] + 4) % 4;
        return delta;
    }

    public static Rectangle GetOrientedCropRect(Rectangle cropRect, Size imageSize, ImageOrientation from, ImageOrientation to)
    {
        Rectangle canonicalCropRect = ToCanonicalCropRect(cropRect, imageSize, from);
        return FromCanonicalCropRect(canonicalCropRect, imageSize, to);
    }

    public static Rectangle ToCanonicalCropRect(Rectangle cropRect, Size imageSize, ImageOrientation orientation)
    {
        Rectangle rect = cropRect;
        Size size = imageSize;

        if (IsTurned(orientation))
        {
            size = new Size(size.Height, size.Width);
        }

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
        return orientation switch
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
    }
}
