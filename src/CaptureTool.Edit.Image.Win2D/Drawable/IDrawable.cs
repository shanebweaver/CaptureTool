using Microsoft.Graphics.Canvas;
using Windows.Foundation;

namespace CaptureTool.Edit.Image.Win2D.Drawable;

public partial interface IDrawable
{
    public Point Offset { get; set; }

    void Draw(CanvasDrawingSession drawingSession);
}
