using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;

namespace CaptureTool.Edit.Image.Win2D.Drawable;

public sealed partial class ImageDrawable : IDrawable
{
    public Vector2 Offset { get; set; }

    public string FileName { get; set; }

    private ICanvasImage? _canvasImage;

    public ImageDrawable(Vector2 offset, string fileName)
    {
        Offset = offset;
        FileName = fileName;
    }

    public void Draw(CanvasDrawingSession drawingSession)
    {
        Debug.Assert(_canvasImage != null);
        drawingSession.DrawImage(_canvasImage, Offset);
    }

    public async Task PrepareAsync(ICanvasResourceCreator resourceCreator)
    {
        _canvasImage = await CanvasBitmap.LoadAsync(resourceCreator, FileName);
    }
}
