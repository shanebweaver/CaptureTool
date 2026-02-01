using CaptureTool.Domain.Edit.Implementations.Windows.ChromaKey;
using CaptureTool.Domain.Edit.Interfaces;
using CaptureTool.Domain.Edit.Interfaces.Drawable;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI;
using System.Numerics;
using Windows.Foundation;
using Color = Windows.UI.Color;

namespace CaptureTool.Domain.Edit.Implementations.Windows;

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
        else if (drawable is EllipseDrawable ellipseDrawable)
            DrawEllipse(ellipseDrawable, drawingSession);
        else if (drawable is LineDrawable lineDrawable)
            DrawLine(lineDrawable, drawingSession);
        else if (drawable is ArrowDrawable arrowDrawable)
            DrawArrow(arrowDrawable, drawingSession);
        else if (drawable is ImageDrawable imageDrawable)
            DrawImage(imageDrawable, drawingSession);
    }

    private static void DrawText(TextDrawable drawable, CanvasDrawingSession drawingSession)
    {
        Vector2 textPosition = new(drawable.Offset.X, drawable.Offset.Y);
        Color color = Color.FromArgb(drawable.Color.A, drawable.Color.R, drawable.Color.G, drawable.Color.B);
        
        // Create text format with specified font family and size
        using var textFormat = new Microsoft.Graphics.Canvas.Text.CanvasTextFormat
        {
            FontFamily = drawable.FontFamily,
            FontSize = drawable.FontSize
        };
        
        drawingSession.DrawText(drawable.Text, textPosition, color, textFormat);
    }

    private static void DrawRectangle(RectangleDrawable drawable, CanvasDrawingSession drawingSession)
    {
        Rect rectangleRect = new(drawable.Offset.X, drawable.Offset.Y, drawable.Size.Width, drawable.Size.Height);
        
        // Draw fill if FillColor is not transparent
        if (drawable.FillColor.A > 0)
        {
            Color fillColor = Color.FromArgb(drawable.FillColor.A, drawable.FillColor.R, drawable.FillColor.G, drawable.FillColor.B);
            drawingSession.FillRectangle(rectangleRect, fillColor);
        }
        
        // Draw stroke
        Color strokeColor = Color.FromArgb(drawable.StrokeColor.A, drawable.StrokeColor.R, drawable.StrokeColor.G, drawable.StrokeColor.B);
        drawingSession.DrawRectangle(rectangleRect, strokeColor, drawable.StrokeWidth);
    }

    private static void DrawEllipse(EllipseDrawable drawable, CanvasDrawingSession drawingSession)
    {
        float centerX = drawable.Offset.X + drawable.Size.Width / 2f;
        float centerY = drawable.Offset.Y + drawable.Size.Height / 2f;
        float radiusX = drawable.Size.Width / 2f;
        float radiusY = drawable.Size.Height / 2f;
        
        // Draw fill if FillColor is not transparent
        if (drawable.FillColor.A > 0)
        {
            Color fillColor = Color.FromArgb(drawable.FillColor.A, drawable.FillColor.R, drawable.FillColor.G, drawable.FillColor.B);
            drawingSession.FillEllipse(centerX, centerY, radiusX, radiusY, fillColor);
        }
        
        // Draw stroke
        Color strokeColor = Color.FromArgb(drawable.StrokeColor.A, drawable.StrokeColor.R, drawable.StrokeColor.G, drawable.StrokeColor.B);
        drawingSession.DrawEllipse(centerX, centerY, radiusX, radiusY, strokeColor, drawable.StrokeWidth);
    }

    private static void DrawLine(LineDrawable drawable, CanvasDrawingSession drawingSession)
    {
        Vector2 startPoint = drawable.Offset;
        Vector2 endPoint = drawable.EndPoint;
        Color strokeColor = Color.FromArgb(drawable.StrokeColor.A, drawable.StrokeColor.R, drawable.StrokeColor.G, drawable.StrokeColor.B);
        drawingSession.DrawLine(startPoint, endPoint, strokeColor, drawable.StrokeWidth);
    }

    private static void DrawArrow(ArrowDrawable drawable, CanvasDrawingSession drawingSession)
    {
        Vector2 startPoint = drawable.Offset;
        Vector2 endPoint = drawable.EndPoint;
        Color strokeColor = Color.FromArgb(drawable.StrokeColor.A, drawable.StrokeColor.R, drawable.StrokeColor.G, drawable.StrokeColor.B);
        
        // Draw the main line
        drawingSession.DrawLine(startPoint, endPoint, strokeColor, drawable.StrokeWidth);
        
        // Calculate arrow head
        Vector2 direction = Vector2.Normalize(endPoint - startPoint);
        float arrowHeadLength = Math.Max(15f, drawable.StrokeWidth * 3f);
        float arrowHeadWidth = Math.Max(10f, drawable.StrokeWidth * 2f);
        
        // Perpendicular vector
        Vector2 perpendicular = new(-direction.Y, direction.X);
        
        // Arrow head points
        Vector2 arrowBase = endPoint - direction * arrowHeadLength;
        Vector2 arrowLeft = arrowBase + perpendicular * arrowHeadWidth / 2f;
        Vector2 arrowRight = arrowBase - perpendicular * arrowHeadWidth / 2f;
        
        // Draw arrow head
        drawingSession.DrawLine(endPoint, arrowLeft, strokeColor, drawable.StrokeWidth);
        drawingSession.DrawLine(endPoint, arrowRight, strokeColor, drawable.StrokeWidth);
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
        ICanvasImage prepared = await CanvasBitmap.LoadAsync(resourceCreator, imageDrawable.File.FilePath);
        imageDrawable.SetPreparedImage(prepared);
    }
}
