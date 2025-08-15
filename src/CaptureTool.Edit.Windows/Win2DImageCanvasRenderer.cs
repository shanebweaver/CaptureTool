using CaptureTool.Edit.Drawable;
using CaptureTool.Edit.Windows.ChromaKey;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Color = Windows.UI.Color;

namespace CaptureTool.Edit.Windows;

public static partial class Win2DImageCanvasRenderer
{
    private static readonly Color ClearColor = Colors.Transparent;

    public static void Render(IDrawable[] drawables, ImageCanvasRenderOptions options, CanvasDrawingSession drawingSession, float scale = 1f)
    {
        drawingSession.Clear(ClearColor);

        var device = drawingSession.Device;
        var renderTarget = new CanvasRenderTarget(device, options.CanvasSize.Width, options.CanvasSize.Height, options.Dpi);

        using (var tempSession = renderTarget.CreateDrawingSession())
        {
            tempSession.Clear(ClearColor);

            foreach (IDrawable drawable in drawables)
            {
                Draw(drawable, tempSession);
            }
        }

        //  Rotate, flip/mirror, scale
        var rotateEffect = new Transform2DEffect
        {
            Source = renderTarget,
            TransformMatrix = ImageOrientationHelper.CalculateRenderTransform(options.CanvasSize, options.Orientation, scale)
        };

        // Crop
        var cropEffect = new CropEffect
        {
            Source = rotateEffect,
            SourceRectangle = new(
                new Point(options.CropRect.Location.X, options.CropRect.Location.Y),
                new Size(options.CropRect.Width, options.CropRect.Height))
        };
        var cropAlignmentEffect = new Transform2DEffect
        {
            Source = cropEffect,
            TransformMatrix = Matrix3x2.CreateTranslation(-options.CropRect.X, -options.CropRect.Y)
        };

        drawingSession.DrawImage(cropAlignmentEffect);
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
        ChromaKeyProcessor processor = new();
        ICanvasImage? preparedImage = drawable.GetPreparedImage();
        if (preparedImage != null)
        {
            if (drawable.ImageEffect is ImageChromaKeyEffect imageChromaKeyEffect && imageChromaKeyEffect.IsEnabled)
            {
                var keyColor = Color.FromArgb(
                    imageChromaKeyEffect.Color.A,
                    imageChromaKeyEffect.Color.R,
                    imageChromaKeyEffect.Color.G,
                    imageChromaKeyEffect.Color.B);
                var tolerance = imageChromaKeyEffect.Tolerance;
                var desaturation = imageChromaKeyEffect.Desaturation;

                drawingSession.Clear(Colors.White);
                processor.DrawChromaKeyMaskedImage(drawingSession, preparedImage, drawable.Offset, keyColor, tolerance, desaturation);
            }
            else
            {
                drawingSession.Clear(Colors.White);
                drawingSession.DrawImage(preparedImage, drawable.Offset);
            }
        }
    }

    public static async Task PrepareAsync(ImageDrawable imageDrawable, ICanvasResourceCreator resourceCreator)
    {
        ICanvasImage prepared = await CanvasBitmap.LoadAsync(resourceCreator, imageDrawable.FileName.Path);
        imageDrawable.SetPreparedImage(prepared);
    }
}
