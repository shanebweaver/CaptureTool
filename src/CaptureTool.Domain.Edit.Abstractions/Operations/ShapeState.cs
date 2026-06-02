using CaptureTool.Domain.Edit.Abstractions.Drawable;
using System.Drawing;

namespace CaptureTool.Domain.Edit.Abstractions.Operations;

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
                    FontFamily = string.Empty;
                    FontSize = default;
                    break;

                case TextDrawable text:
                    Offset = text.Offset;
                    Size = text.Size;
                    EndPoint = default;
                    StrokeColor = text.Color;
                    FillColor = default;
                    StrokeWidth = default;
                    Text = text.Text;
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
                    FontFamily = string.Empty;
                    FontSize = default;
                    break;
            }
        }
    }
}
