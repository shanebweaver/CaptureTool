using System.Drawing;

namespace CaptureTool.Domain.Edit.Drawable;

public readonly record struct ShapeStyle(
    Color StrokeColor,
    Color FillColor,
    int StrokeWidth);
