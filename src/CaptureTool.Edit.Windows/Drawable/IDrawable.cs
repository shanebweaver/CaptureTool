using Microsoft.Graphics.Canvas;
using System.Numerics;

namespace CaptureTool.Edit.Windows.Drawable;

public partial interface IDrawable
{
    public Vector2 Offset { get; set; }

    void Draw(CanvasDrawingSession drawingSession);
}
