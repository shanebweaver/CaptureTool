using CaptureTool.Mcp.CaptureServer.Models;
using CaptureTool.Mcp.CaptureServer.Capture;
using CaptureTool.Mcp.CaptureServer.Abstractions;
using CaptureTool.Domain.Edit.Drawable;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;

namespace CaptureTool.Mcp.CaptureServer.Annotations;

public sealed class AnnotationService : IAnnotationService
{
    private readonly IMcpCaptureStore _captureStore;
    private readonly AnnotationDrawableFactory _drawableFactory;
    private readonly TimeProvider _timeProvider;

    public AnnotationService(IMcpCaptureStore captureStore, AnnotationDrawableFactory drawableFactory, TimeProvider timeProvider)
    {
        _captureStore = captureStore;
        _drawableFactory = drawableFactory;
        _timeProvider = timeProvider;
    }

    public McpCapture AnnotateWithArrow(string captureId, int arrowStartX, int arrowStartY, int arrowEndX, int arrowEndY, string? label)
    {
        if (!_captureStore.TryGet(captureId, out McpCapture sourceCapture))
        {
            throw new InvalidOperationException($"Capture '{captureId}' is not available.");
        }

        using var inputStream = new MemoryStream(sourceCapture.PngBytes);
        using var sourceBitmap = new Bitmap(inputStream);
        using var outputBitmap = new Bitmap(sourceBitmap.Width, sourceBitmap.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(outputBitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        graphics.DrawImage(sourceBitmap, Point.Empty);

        AnnotationDrawableSet annotationDrawables = _drawableFactory.CreateArrowWithOptionalLabel(
            outputBitmap.Size,
            arrowStartX,
            arrowStartY,
            arrowEndX,
            arrowEndY,
            label);

        foreach (IDrawable drawable in annotationDrawables.Drawables)
        {
            Draw(drawable, graphics);
        }

        using var outputStream = new MemoryStream();
        outputBitmap.Save(outputStream, ImageFormat.Png);

        var metadata = McpCaptureMetadata.Create(
            McpCaptureIds.Create(),
            _timeProvider.GetUtcNow(),
            outputBitmap.Width,
            outputBitmap.Height,
            sourceCapture.Metadata.Dpi,
            sourceCapture.Metadata.Scale,
            sourceCapture.Metadata.SourceBounds.ToRectangle(),
            "annotatedImage",
            "png",
            sourceCapture.Metadata.MonitorBounds?.ToRectangle(),
            sourceCapture.Metadata.WorkAreaBounds?.ToRectangle(),
            sourceCapture.Metadata.IsPrimary,
            sourceCaptureId: sourceCapture.Metadata.CaptureId,
            annotationPlacements: annotationDrawables.Placements);

        var annotatedCapture = new McpCapture(outputStream.ToArray(), metadata);
        _captureStore.Store(annotatedCapture);
        return annotatedCapture;
    }

    private static void Draw(IDrawable drawable, Graphics graphics)
    {
        switch (drawable)
        {
            case ArrowDrawable arrow:
                DrawArrow(arrow, graphics);
                break;
            case TextDrawable text:
                DrawText(text, graphics);
                break;
        }
    }

    private static void DrawArrow(ArrowDrawable drawable, Graphics graphics)
    {
        using var pen = new Pen(drawable.StrokeColor, drawable.StrokeWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Custom,
            CustomEndCap = new AdjustableArrowCap(drawable.StrokeWidth * 3f, drawable.StrokeWidth * 4f),
        };

        graphics.DrawLine(pen, ToPointF(drawable.Offset), ToPointF(drawable.EndPoint));
    }

    private static void DrawText(TextDrawable drawable, Graphics graphics)
    {
        var bounds = new RectangleF(drawable.Offset.X, drawable.Offset.Y, drawable.Size.Width, drawable.Size.Height);
        using var backgroundBrush = new SolidBrush(drawable.BackgroundColor);
        using var textBrush = new SolidBrush(drawable.Color);
        using var font = new Font(drawable.FontFamily, drawable.FontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var stringFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
        };

        graphics.FillRectangle(backgroundBrush, bounds);
        graphics.DrawString(drawable.Text, font, textBrush, bounds, stringFormat);
    }

    private static PointF ToPointF(Vector2 vector)
        => new(vector.X, vector.Y);
}
