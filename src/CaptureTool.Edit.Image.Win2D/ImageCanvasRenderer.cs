using System;
using System.Drawing;
using System.Numerics;
using System.Threading.Tasks;
using CaptureTool.Edit.Image.Win2D.Drawable;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Provider;
using Windows.Storage.Streams;

namespace CaptureTool.Edit.Image.Win2D;

public static partial class ImageCanvasRenderer
{
    private static readonly Windows.UI.Color ClearColor = Colors.Transparent;

    public static async Task CopyImageToClipboardAsync(IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        float renderWidth = options.CropRect.Width;
        float renderHeight = options.CropRect.Height;

        using CanvasRenderTarget renderTarget = new(CanvasDevice.GetSharedDevice(), renderWidth, renderHeight, options.Dpi);
        using CanvasDrawingSession drawingSession = renderTarget.CreateDrawingSession();

        drawingSession.Transform = CalculateTransform(options);

        foreach (IDrawable drawable in drawables)
        {
            drawable.Draw(drawingSession);
        }

        drawingSession.Flush();

        using var stream = new InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);

        DataPackage dataPackage = new();
        dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
        Clipboard.SetContent(dataPackage);
        Clipboard.Flush();
    }

    public static async Task SaveImageAsync(string filePath, IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        float renderWidth = options.CropRect.Width;
        float renderHeight = options.CropRect.Height;

        using CanvasRenderTarget renderTarget = new(CanvasDevice.GetSharedDevice(), renderWidth, renderHeight, options.Dpi);
        using CanvasDrawingSession drawingSession = renderTarget.CreateDrawingSession();

        drawingSession.Transform = CalculateTransform(options);

        foreach (IDrawable drawable in drawables)
        {
            drawable.Draw(drawingSession);
        }

        drawingSession.Flush();

        using var stream = new InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);

        StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
        CachedFileManager.DeferUpdates(file);

        using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
        {
            stream.Seek(0);
            await RandomAccessStream.CopyAsync(stream, fileStream);
        }

        FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
        if (status != FileUpdateStatus.Complete)
        {
            throw new Exception("File could not be saved.");
        }
    }

    public static void Render(IDrawable[] drawables, ImageCanvasRenderOptions options, CanvasDrawingSession drawingSession)
    {
        // Clear the drawing session
        drawingSession.Clear(ClearColor);

        // Apply the final transform to the drawing session
        drawingSession.Transform = CalculateTransform(options);

        // Draw all the drawables
        foreach (IDrawable drawable in drawables)
        {
            drawable.Draw(drawingSession);
        }
    }

    private static Matrix3x2 CalculateTransform(ImageCanvasRenderOptions options)
    {
        Matrix3x2 transform = Matrix3x2.Identity;
        double canvasWidth = options.CanvasSize.Width;
        double canvasHeight = options.CanvasSize.Height;

        // Apply rotation
        double maxDimension = Math.Max(canvasHeight, canvasWidth);
        Vector2 rotationPoint = new((float)maxDimension / 2, (float)maxDimension / 2);
        switch (options.Orientation)
        {
            case RotateFlipType.Rotate90FlipNone:
            case RotateFlipType.Rotate90FlipX:
            case RotateFlipType.Rotate90FlipY:
                transform *= Matrix3x2.CreateRotation(GetRadians(90), rotationPoint);
                break;

            case RotateFlipType.Rotate180FlipNone:
            case RotateFlipType.Rotate180FlipX:
            case RotateFlipType.Rotate180FlipY:
                transform *= Matrix3x2.CreateRotation(GetRadians(180), rotationPoint);
                break;

            case RotateFlipType.Rotate270FlipNone:
                transform *= Matrix3x2.CreateRotation(GetRadians(270), rotationPoint);
                break;
        }

        // Apply translation to reposition at 0,0
        bool isLandscape = canvasWidth > canvasHeight;
        float heightLessWidth = (float)(canvasHeight - canvasWidth);
        float widthLessHeight = (float)(canvasWidth - canvasHeight);
        switch (options.Orientation)
        {
            case RotateFlipType.Rotate90FlipNone:
            case RotateFlipType.Rotate90FlipX:
            case RotateFlipType.Rotate90FlipY:
                if (isLandscape)
                {
                    transform *= Matrix3x2.CreateTranslation(heightLessWidth, 0);
                }
                break;

            case RotateFlipType.Rotate180FlipNone:
            case RotateFlipType.Rotate180FlipX:
            case RotateFlipType.Rotate180FlipY:
                if (isLandscape)
                {
                    transform *= Matrix3x2.CreateTranslation(0, heightLessWidth);
                }
                else
                {
                    transform *= Matrix3x2.CreateTranslation(widthLessHeight, 0);
                }
                break;

            case RotateFlipType.Rotate270FlipNone:
                if (!isLandscape)
                {
                    transform *= Matrix3x2.CreateTranslation(0, widthLessHeight);
                }
                break;
        }

        // Apply flipping
        switch (options.Orientation)
        {
            case RotateFlipType.Rotate180FlipY:
                transform *= Matrix3x2.CreateScale(1, -1, new((float)canvasWidth / 2, (float)canvasHeight / 2));
                break;

            case RotateFlipType.Rotate90FlipX:
                transform *= Matrix3x2.CreateScale(1, -1, new((float)canvasHeight / 2, (float)canvasWidth / 2));
                break;

            case RotateFlipType.Rotate180FlipX:
                transform *= Matrix3x2.CreateScale(-1, 1, new((float)canvasWidth / 2, (float)canvasHeight / 2));
                break;

            case RotateFlipType.Rotate90FlipY:
                transform *= Matrix3x2.CreateScale(-1, 1, new((float)canvasHeight / 2, (float)canvasWidth / 2));
                break;
        }

        // Apply cropping
        transform *= Matrix3x2.CreateTranslation(-(float)options.CropRect.X, -(float)options.CropRect.Y);

        return transform;
    }

    private static float GetRadians(double angle)
    {
        return (float)(Math.PI * angle / 180.0);
    }
}
