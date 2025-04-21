using System.Numerics;
using Microsoft.Graphics.Canvas;

namespace CaptureTool.Edit.Image.Win2D.Drawable;

public partial interface IDrawable
{
    public Vector2 Offset { get; set; }

    void Draw(CanvasDrawingSession drawingSession);
}
