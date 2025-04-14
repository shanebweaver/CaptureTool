using Microsoft.Graphics.Canvas;
using Windows.Foundation;

namespace CaptureTool.UI.Xaml.Controls.ImageCanvas.Drawable;

internal interface IDrawable
{
    void Draw(CanvasDrawingSession drawingSession, Rect sessionBounds);
}
