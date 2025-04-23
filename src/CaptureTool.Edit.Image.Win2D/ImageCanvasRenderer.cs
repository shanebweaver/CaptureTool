using System;
using System.Drawing;
using System.Numerics;
using System.Threading.Tasks;
using CaptureTool.Edit.Image.Win2D.Drawable;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Windows.ApplicationModel.DataTransfer;
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
        // Determine the canvas dimensions based on whether the canvas is turned
        float canvasWidth = options.IsTurned ? options.CanvasSize.Height : options.CanvasSize.Width;
        float canvasHeight = options.IsTurned ? options.CanvasSize.Width : options.CanvasSize.Height;

        // Calculate the center point of the canvas
        Vector2 centerPoint = new(canvasWidth / 2, canvasHeight / 2);

        // Initialize the transform matrix
        Matrix3x2 transform = Matrix3x2.Identity;

        // Apply flipping based on the RotateFlipType
        switch (options.Orientation)
        {
            case RotateFlipType.RotateNoneFlipX:
            case RotateFlipType.Rotate90FlipX:
            case RotateFlipType.Rotate180FlipX:
            case RotateFlipType.Rotate270FlipX:
                transform *= Matrix3x2.CreateScale(-1, 1, centerPoint);
                break;
        }

        var maxdimension = Math.Max(canvasHeight, canvasWidth);
        Vector2 rotationPoint = new(maxdimension / 2, maxdimension / 2);

        // Apply rotation based on the RotateFlipType
        switch (options.Orientation)
        {
            case RotateFlipType.Rotate90FlipNone:
                transform *= Matrix3x2.CreateRotation(GetRadians(90), rotationPoint);
                transform *= Matrix3x2.CreateTranslation(canvasWidth - canvasHeight, 0);
                break;

            case RotateFlipType.Rotate90FlipX:
            case RotateFlipType.Rotate90FlipY:
                transform *= Matrix3x2.CreateRotation(GetRadians(90), rotationPoint);
                transform *= Matrix3x2.CreateTranslation(canvasWidth - canvasHeight, canvasHeight - canvasWidth);
                break;

            case RotateFlipType.Rotate180FlipNone:
            case RotateFlipType.Rotate180FlipX:
            case RotateFlipType.Rotate180FlipY:
                transform *= Matrix3x2.CreateRotation(GetRadians(180), rotationPoint);
                transform *= Matrix3x2.CreateTranslation(0, canvasHeight - canvasWidth);
                break;

            case RotateFlipType.Rotate270FlipNone:
                transform *= Matrix3x2.CreateRotation(GetRadians(270), rotationPoint);
                break;
        }

        return transform;
    }

    private static float GetRadians(double angle)
    {
        return (float)(Math.PI * angle / 180.0);
    }
}
