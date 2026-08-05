using System.Drawing;

namespace CaptureTool.Presentation.Features.SelectionOverlay;

public readonly record struct SelectionOverlaySelection(Rectangle Area, nint WindowHandle = 0)
{
    public static SelectionOverlaySelection Empty { get; } = new(Rectangle.Empty);
}
