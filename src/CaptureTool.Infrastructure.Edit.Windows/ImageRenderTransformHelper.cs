using CaptureTool.Domain.Edit;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Infrastructure.Edit.Windows;

public static class ImageRenderTransformHelper
{
    public static Matrix3x2 CalculateRenderTransform(Size imageSize, ImageOrientation orientation, float scale = 1f)
    {
        Matrix3x2 transform = Matrix3x2.Identity;
        double imageWidth = imageSize.Width;
        double imageHeight = imageSize.Height;

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

        transform *= Matrix3x2.CreateScale(scale);

        return transform;
    }

    private static float GetRadians(double angle)
    {
        return (float)(Math.PI * angle / 180.0);
    }
}
