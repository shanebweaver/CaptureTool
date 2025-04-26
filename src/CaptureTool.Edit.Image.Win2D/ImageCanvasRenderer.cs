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
    private static readonly Windows.UI.Color ClearColor = Colors.White;

    public static async Task CopyImageToClipboardAsync(IDrawable[] drawables, ImageCanvasRenderOptions options, float width, float height, float dpi = 96)
    {
        float renderWidth = options.IsTurned ? height : width;
        float renderHeight = options.IsTurned ? width : height;

        using CanvasRenderTarget renderTarget = new(CanvasDevice.GetSharedDevice(), renderWidth, renderHeight, dpi);
        using CanvasDrawingSession drawingSession = renderTarget.CreateDrawingSession();
        Render(drawables, options, drawingSession);
        drawingSession.Flush();

        using var stream = new InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);

        DataPackage dataPackage = new();
        dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
        Clipboard.SetContent(dataPackage);
        Clipboard.Flush();
    }

    public static async Task SaveImageAsync(string filePath, IDrawable[] drawables, ImageCanvasRenderOptions options, float width, float height, float dpi = 96)
    {
        float renderWidth = options.IsTurned ? height : width;
        float renderHeight = options.IsTurned ? width : height;

        using CanvasRenderTarget renderTarget = new(CanvasDevice.GetSharedDevice(), renderWidth, renderHeight, dpi);
        using CanvasDrawingSession drawingSession = renderTarget.CreateDrawingSession();
        Render(drawables, options, drawingSession);
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
        float canvasWidth = options.CanvasSize.Width;
        float canvasHeight = options.CanvasSize.Height;
        float heightLessWidth = canvasHeight - canvasWidth;
        float widthLessHeight = canvasWidth - canvasHeight;

        float maxDimension = Math.Max(canvasHeight, canvasWidth);
        Vector2 rotationPoint = new(maxDimension / 2, maxDimension / 2);

        bool isLandscape = canvasWidth > canvasHeight;

        Matrix3x2 transform = Matrix3x2.Identity;
        switch (options.Orientation)
        {
            case RotateFlipType.Rotate90FlipNone:
                transform *= Matrix3x2.CreateRotation(GetRadians(90), rotationPoint);
                if (isLandscape)
                {
                    transform *= Matrix3x2.CreateTranslation(heightLessWidth, 0);
                }
                break;

            case RotateFlipType.Rotate180FlipNone:
                transform *= Matrix3x2.CreateRotation(GetRadians(180), rotationPoint);
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
                transform *= Matrix3x2.CreateRotation(GetRadians(270), rotationPoint);
                if (!isLandscape)
                {
                    transform *= Matrix3x2.CreateTranslation(0, widthLessHeight);
                }
                break;

            case RotateFlipType.Rotate180FlipY:
                transform *= Matrix3x2.CreateRotation(GetRadians(180), rotationPoint);
                if (isLandscape)
                {
                    transform *= Matrix3x2.CreateTranslation(0, heightLessWidth);
                }
                else
                {
                    transform *= Matrix3x2.CreateTranslation(widthLessHeight, 0);
                }
                transform *= Matrix3x2.CreateScale(1, -1, new(canvasWidth / 2, canvasHeight / 2));
                break;

            case RotateFlipType.Rotate90FlipX:
                transform *= Matrix3x2.CreateRotation(GetRadians(90), rotationPoint);
                if (isLandscape)
                {
                    transform *= Matrix3x2.CreateTranslation(heightLessWidth, 0);
                }
                else
                {
                    //transform *= Matrix3x2.CreateTranslation(0, widthLessHeight);
                }
                transform *= Matrix3x2.CreateScale(1, -1, new(canvasHeight / 2, canvasWidth / 2));
                break;

            case RotateFlipType.Rotate180FlipX:
                transform *= Matrix3x2.CreateRotation(GetRadians(180), rotationPoint);
                if (isLandscape)
                {
                    transform *= Matrix3x2.CreateTranslation(0, heightLessWidth);
                }
                else
                {
                    transform *= Matrix3x2.CreateTranslation(widthLessHeight, 0);
                }
                transform *= Matrix3x2.CreateScale(-1, 1, new(canvasWidth / 2, canvasHeight / 2));
                break;

            case RotateFlipType.Rotate90FlipY:
                transform *= Matrix3x2.CreateRotation(GetRadians(90), rotationPoint); 
                if (isLandscape)
                {
                    transform *= Matrix3x2.CreateTranslation(heightLessWidth, 0);
                }
                else
                {
                    //transform *= Matrix3x2.CreateTranslation(0, widthLessHeight);
                }
                transform *= Matrix3x2.CreateScale(-1, 1, new(canvasHeight / 2, canvasWidth / 2));
                break;
        }

        return transform;
    }

    private static float GetRadians(double angle)
    {
        return (float)(Math.PI * angle / 180.0);
    }
}
