using CaptureTool.Domain.Edit.Drawable;
using System.Drawing;

namespace CaptureTool.Domain.Edit.Operations;

public sealed partial class ModifyShapeOperation
{
    public readonly struct ShapeState
    {
        public System.Numerics.Vector2 Offset { get; init; }
        public Size Size { get; init; }
        public System.Numerics.Vector2 EndPoint { get; init; }
        public Color StrokeColor { get; init; }
        public Color FillColor { get; init; }
        public int StrokeWidth { get; init; }
        public string Text { get; init; }
        public Color TextColor { get; init; }
        public Color TextBackgroundColor { get; init; }
        public string FontFamily { get; init; }
        public float FontSize { get; init; }

        public ShapeState(IDrawable shape)
        {
            switch (shape)
            {
                case RectangleDrawable rect:
                    Offset = rect.Offset;
                    Size = rect.Size;
                    EndPoint = default;
                    StrokeColor = rect.StrokeColor;
                    FillColor = rect.FillColor;
                    StrokeWidth = rect.StrokeWidth;
                    Text = string.Empty;
                    TextColor = default;
                    TextBackgroundColor = default;
                    FontFamily = string.Empty;
                    FontSize = default;
                    break;

                case EllipseDrawable ellipse:
                    Offset = ellipse.Offset;
                    Size = ellipse.Size;
                    EndPoint = default;
                    StrokeColor = ellipse.StrokeColor;
                    FillColor = ellipse.FillColor;
                    StrokeWidth = ellipse.StrokeWidth;
                    Text = string.Empty;
                    TextColor = default;
                    TextBackgroundColor = default;
                    FontFamily = string.Empty;
                    FontSize = default;
                    break;

                case LineDrawable line:
                    Offset = line.Offset;
                    Size = default;
                    EndPoint = line.EndPoint;
                    StrokeColor = line.StrokeColor;
                    FillColor = default;
                    StrokeWidth = line.StrokeWidth;
                    Text = string.Empty;
                    TextColor = default;
                    TextBackgroundColor = default;
                    FontFamily = string.Empty;
                    FontSize = default;
                    break;

                case ArrowDrawable arrow:
                    Offset = arrow.Offset;
                    Size = default;
                    EndPoint = arrow.EndPoint;
                    StrokeColor = arrow.StrokeColor;
                    FillColor = default;
                    StrokeWidth = arrow.StrokeWidth;
                    Text = string.Empty;
                    TextColor = default;
                    TextBackgroundColor = default;
                    FontFamily = string.Empty;
                    FontSize = default;
                    break;

                case TextDrawable text:
                    Offset = text.Offset;
                    Size = text.Size;
                    EndPoint = default;
                    StrokeColor = default;
                    FillColor = default;
                    StrokeWidth = default;
                    Text = text.Text;
                    TextColor = text.Color;
                    TextBackgroundColor = text.BackgroundColor;
                    FontFamily = text.FontFamily;
                    FontSize = text.FontSize;
                    break;

                default:
                    Offset = default;
                    Size = default;
                    EndPoint = default;
                    StrokeColor = default;
                    FillColor = default;
                    StrokeWidth = default;
                    Text = string.Empty;
                    TextColor = default;
                    TextBackgroundColor = default;
                    FontFamily = string.Empty;
                    FontSize = default;
                    break;
            }
        }
    }
}
