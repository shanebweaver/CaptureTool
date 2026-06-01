using CaptureTool.Domain.Edit.Abstractions;
using CaptureTool.Domain.Edit.Abstractions.Drawable;
using CaptureTool.Domain.Edit.Windows.ChromaKey;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI;
using System.Numerics;
using Windows.Foundation;
using Color = Windows.UI.Color;

namespace CaptureTool.Domain.Edit.Windows;

public static partial class Win2DImageCanvasRenderer
{
    private static readonly Color ClearColor = Colors.Transparent;

    public static void Render(IDrawable[] drawables, ImageCanvasRenderOptions options, CanvasDrawingSession drawingSession, float scale = 1f)
    {
        drawingSession.Clear(ClearColor);

        var device = drawingSession.Device;
        using var renderTarget = new CanvasRenderTarget(device, options.CanvasSize.Width, options.CanvasSize.Height, options.Dpi);

        using (var tempSession = renderTarget.CreateDrawingSession())
        {
            tempSession.Clear(ClearColor);

            foreach (IDrawable drawable in drawables)
            {
                Draw(drawable, tempSession);
            }
        }

        //  Rotate, flip/mirror, scale
        using var rotateEffect = new Transform2DEffect
        {
            Source = renderTarget,
            TransformMatrix = ImageOrientationHelper.CalculateRenderTransform(options.CanvasSize, options.Orientation, scale)
        };

        // Crop
        using var cropEffect = new CropEffect
        {
            Source = rotateEffect,
            SourceRectangle = new(
                new Point(options.CropRect.Location.X, options.CropRect.Location.Y),
                new Size(options.CropRect.Width, options.CropRect.Height))
        };
        using var cropAlignmentEffect = new Transform2DEffect
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
        using CanvasTextFormat textFormat = new()
        {
            FontFamily = string.IsNullOrWhiteSpace(drawable.FontFamily) ? TextDrawable.DefaultFontFamily : drawable.FontFamily,
            FontSize = drawable.FontSize > 0 ? drawable.FontSize : TextDrawable.DefaultFontSize,
        };

        drawingSession.DrawText(drawable.Text, textPosition, color, textFormat);
    }

    private static void DrawRectangle(RectangleDrawable drawable, CanvasDrawingSession drawingSession)
    {
        float strokeWidth = Math.Max(0, drawable.StrokeWidth);
        float halfStrokeWidth = strokeWidth / 2f;
        Rect strokeRect = InsetRect(
            new Rect(drawable.Offset.X, drawable.Offset.Y, drawable.Size.Width, drawable.Size.Height),
            halfStrokeWidth);

        // Draw fill if FillColor is not transparent
        if (drawable.FillColor.A > 0)
        {
            Rect fillRect = InsetRect(
                new Rect(drawable.Offset.X, drawable.Offset.Y, drawable.Size.Width, drawable.Size.Height),
                strokeWidth);
            Color fillColor = Color.FromArgb(drawable.FillColor.A, drawable.FillColor.R, drawable.FillColor.G, drawable.FillColor.B);
            if (fillRect.Width > 0 && fillRect.Height > 0)
            {
                drawingSession.FillRectangle(fillRect, fillColor);
            }
        }

        // Draw stroke
        Color strokeColor = Color.FromArgb(drawable.StrokeColor.A, drawable.StrokeColor.R, drawable.StrokeColor.G, drawable.StrokeColor.B);
        if (strokeRect.Width > 0 && strokeRect.Height > 0 && strokeWidth > 0)
        {
            drawingSession.DrawRectangle(strokeRect, strokeColor, strokeWidth);
        }
    }

    private static void DrawEllipse(EllipseDrawable drawable, CanvasDrawingSession drawingSession)
    {
        float strokeWidth = Math.Max(0, drawable.StrokeWidth);
        float centerX = drawable.Offset.X + drawable.Size.Width / 2f;
        float centerY = drawable.Offset.Y + drawable.Size.Height / 2f;
        float strokeRadiusX = Math.Max(0, (drawable.Size.Width - strokeWidth) / 2f);
        float strokeRadiusY = Math.Max(0, (drawable.Size.Height - strokeWidth) / 2f);

        // Draw fill if FillColor is not transparent
        if (drawable.FillColor.A > 0)
        {
            float fillRadiusX = Math.Max(0, (drawable.Size.Width - strokeWidth * 2f) / 2f);
            float fillRadiusY = Math.Max(0, (drawable.Size.Height - strokeWidth * 2f) / 2f);
            Color fillColor = Color.FromArgb(drawable.FillColor.A, drawable.FillColor.R, drawable.FillColor.G, drawable.FillColor.B);
            if (fillRadiusX > 0 && fillRadiusY > 0)
            {
                drawingSession.FillEllipse(centerX, centerY, fillRadiusX, fillRadiusY, fillColor);
            }
        }

        // Draw stroke
        Color strokeColor = Color.FromArgb(drawable.StrokeColor.A, drawable.StrokeColor.R, drawable.StrokeColor.G, drawable.StrokeColor.B);
        if (strokeRadiusX > 0 && strokeRadiusY > 0 && strokeWidth > 0)
        {
            drawingSession.DrawEllipse(centerX, centerY, strokeRadiusX, strokeRadiusY, strokeColor, strokeWidth);
        }
    }

    private static Rect InsetRect(Rect rect, float inset)
    {
        double width = Math.Max(0, rect.Width - inset * 2);
        double height = Math.Max(0, rect.Height - inset * 2);
        return new Rect(rect.X + inset, rect.Y + inset, width, height);
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
                var softness = imageChromaKeyEffect.Desaturation;

                drawingSession.Clear(Colors.White);
                drawable.GetChromaKeyProcessor().DrawChromaKeyMaskedImage(drawingSession, preparedImage, drawable.Offset, keyColor, tolerance, softness);
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
