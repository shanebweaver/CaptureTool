using CaptureTool.Edit.Drawable;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Color = Windows.UI.Color;

namespace CaptureTool.Edit.Windows;

public sealed partial class Win2DImageCanvasRenderer
{
    private static readonly Color ClearColor = Colors.Transparent;
    private static readonly Dictionary<ImageDrawable, ICanvasImage> _preparedImages = [];

    public static void Render(IDrawable[] drawables, ImageCanvasRenderOptions options, object drawingSessionObj)
    {
        if (drawingSessionObj is not CanvasDrawingSession drawingSession)
        {
            throw new InvalidOperationException("Invalid drawing session object.");
        }

        // Clear the drawing session
        drawingSession.Clear(ClearColor);

        // Apply the final transform to the drawing session
        drawingSession.Transform = OrientationHelper.CalculateRenderTransform(options.CropRect, options.CanvasSize, options.Orientation);

        // Draw all the drawables
        foreach (IDrawable drawable in drawables)
        {
            Draw(drawable, drawingSession);
        }
    }

    public static void Draw(IDrawable drawable, CanvasDrawingSession drawingSession)
    {
        if (drawable is TextDrawable textDrawable)
            DrawText(textDrawable, drawingSession);
        else if (drawable is RectangleDrawable rectangleDrawable)
            DrawRectangle(rectangleDrawable, drawingSession);
        else if (drawable is ImageDrawable imageDrawable)
            DrawImage(imageDrawable, drawingSession);
    }

    private static void DrawText(TextDrawable drawable, CanvasDrawingSession drawingSession)
    {
        Vector2 textPosition = new(drawable.Offset.X, drawable.Offset.Y);
        Color color = Color.FromArgb(drawable.Color.A, drawable.Color.R, drawable.Color.G, drawable.Color.B);
        drawingSession.DrawText(drawable.Text, textPosition, color);
    }

    private static void DrawRectangle(RectangleDrawable drawable, CanvasDrawingSession drawingSession)
    {
        Rect rectangleRect = new(drawable.Offset.X, drawable.Offset.Y, drawable.Size.Width, drawable.Size.Height);
        Color color = Color.FromArgb(drawable.Color.A, drawable.Color.R, drawable.Color.G, drawable.Color.B);
        drawingSession.DrawRectangle(rectangleRect, color, drawable.StrokeWidth);
    }

    private static void DrawImage(ImageDrawable drawable, CanvasDrawingSession drawingSession)
    {
        if (_preparedImages.TryGetValue(drawable, out ICanvasImage? preparedImage))
        {
            drawingSession.DrawImage(preparedImage, drawable.Offset);
        }
    }

    public static async Task PrepareAsync(ImageDrawable imageDrawable, ICanvasResourceCreator resourceCreator)
    {
        var prepared = await CanvasBitmap.LoadAsync(resourceCreator, imageDrawable.FileName.Path);
        _preparedImages[imageDrawable] = prepared;
    }
}
