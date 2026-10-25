using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Mcp.CaptureServer.Models;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Mcp.CaptureServer.Annotations;

public sealed class AnnotationDrawableFactory
{
    public const int MaxLabelLength = 1024;

    public AnnotationDrawableSet CreateArrowWithOptionalLabel(
        Size imageSize,
        int arrowStartX,
        int arrowStartY,
        int arrowEndX,
        int arrowEndY,
        string? label)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageSize), "Image dimensions must be positive.");
        }

        ValidateLabel(label);
        arrowStartX = Math.Clamp(arrowStartX, 0, imageSize.Width - 1);
        arrowStartY = Math.Clamp(arrowStartY, 0, imageSize.Height - 1);
        arrowEndX = Math.Clamp(arrowEndX, 0, imageSize.Width - 1);
        arrowEndY = Math.Clamp(arrowEndY, 0, imageSize.Height - 1);

        int strokeWidth = Math.Max(3, (int)Math.Round(Math.Min(imageSize.Width, imageSize.Height) / 240d));
        var arrow = new ArrowDrawable(
            new Vector2(arrowStartX, arrowStartY),
            new Vector2(arrowEndX, arrowEndY),
            Color.Red,
            strokeWidth);

        if (string.IsNullOrWhiteSpace(label))
        {
            return new AnnotationDrawableSet([arrow], []);
        }

        TextDrawable text = CreateTextDrawableNearPoint(imageSize, arrowStartX, arrowStartY, label.Trim());
        var placement = new AnnotationPlacementDto(
            "text",
            RectangleDto.FromRectangle(new Rectangle((int)Math.Round(text.Offset.X), (int)Math.Round(text.Offset.Y), text.Size.Width, text.Size.Height)),
            text.Text);

        return new AnnotationDrawableSet([arrow, text], [placement]);
    }

    private static TextDrawable CreateTextDrawableNearPoint(Size imageSize, int x, int y, string label)
    {
        float fontSize = Math.Max(18f, MathF.Round(Math.Min(imageSize.Width, imageSize.Height) / 32f));
        int horizontalPadding = Math.Max(10, (int)Math.Round(fontSize * 0.55f));
        int verticalPadding = Math.Max(6, (int)Math.Round(fontSize * 0.35f));

        using var measuringBitmap = new Bitmap(1, 1);
        using Graphics graphics = Graphics.FromImage(measuringBitmap);
        using var font = new Font(TextDrawable.DefaultFontFamily, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        SizeF measured = graphics.MeasureString(label, font);
        int labelWidth = Math.Min(
            Math.Max(80, (int)Math.Ceiling(measured.Width) + horizontalPadding * 2),
            imageSize.Width);
        int labelHeight = Math.Min(
            Math.Max(36, (int)Math.Ceiling(measured.Height) + verticalPadding * 2),
            imageSize.Height);

        int labelX = (int)Math.Clamp((long)x + 12, 0, imageSize.Width - labelWidth);
        int labelY = (int)Math.Clamp((long)y + 12, 0, imageSize.Height - labelHeight);

        return new TextDrawable(
            new Vector2(labelX, labelY),
            new Size(labelWidth, labelHeight),
            label,
            Color.White,
            Color.FromArgb(220, Color.Red),
            TextDrawable.DefaultFontFamily,
            fontSize);
    }

    internal static void ValidateLabel(string? label)
    {
        if (label?.Length > MaxLabelLength)
        {
            throw new ArgumentException($"Label must be at most {MaxLabelLength} characters. Shorten the label and retry.", nameof(label));
        }
    }
}

public sealed record AnnotationDrawableSet(IReadOnlyList<IDrawable> Drawables, AnnotationPlacementDto[] Placements);
