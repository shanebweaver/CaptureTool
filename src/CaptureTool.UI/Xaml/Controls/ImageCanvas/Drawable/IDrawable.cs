using Microsoft.Graphics.Canvas;
using Windows.Foundation;

namespace CaptureTool.UI.Xaml.Controls.ImageCanvas.Drawable;

public interface IDrawable
{
    public Point Position { get; set; }

    void Draw(CanvasDrawingSession drawingSession, Rect sessionBounds);
}
