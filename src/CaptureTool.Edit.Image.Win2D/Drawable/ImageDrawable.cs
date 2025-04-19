using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;

namespace CaptureTool.Edit.Image.Win2D.Drawable;

public sealed partial class ImageDrawable : IDrawable
{
    public Point Offset { get; set; }

    public string FileName { get; set; }

    private ICanvasImage? _canvasImage;

    public ImageDrawable(Point offset, string fileName)
    {
        Offset = offset;
        FileName = fileName;
    }

    public void Draw(CanvasDrawingSession drawingSession)
    {
        Debug.Assert(_canvasImage != null);
        Vector2 offset = new((float)Offset.X, (float)Offset.Y);
        drawingSession.DrawImage(_canvasImage, offset);
    }

    public async Task PrepareAsync(ICanvasResourceCreator resourceCreator)
    {
        _canvasImage = await CanvasBitmap.LoadAsync(resourceCreator, FileName);
    }
}
