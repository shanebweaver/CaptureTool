using System;
using CaptureTool.Edit.Image.Win2D.Drawable;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Windows.UI;

namespace CaptureTool.Edit.Image.Win2D;

public sealed partial class ImageCanvasRenderer : IDisposable
{
    private static readonly Color ClearColor = Colors.Black;

    private readonly CanvasDevice _device;

    public ImageCanvasRenderer()
    {
        _device = CanvasDevice.GetSharedDevice();
    }

    public CanvasCommandList Render(IDrawable[] drawables)
    {
        CanvasCommandList commandList = new(_device);
        using CanvasDrawingSession drawingSession = commandList.CreateDrawingSession();
        Render(drawables, drawingSession);
        return commandList;
    }

    public static void Render(IDrawable[] drawables, CanvasDrawingSession drawingSession)
    {
        // Without this, the bitmap will be initialized with undefined content. Drawing sessions created through CanvasRenderTarget
        // are different from those created on Win2D's XAML controls, in terms of the Clear behavior. Controls are always cleared
        // automatically by Win2D when a drawing session is created. CanvasRenderTargets are not. This way, apps have the ability
        // to make incremental changes to CanvasRenderTargets, and avoid redrawing an entire scene every time.
        drawingSession.Clear(ClearColor);

        foreach (IDrawable drawable in drawables)
        {
            drawable.Draw(drawingSession);
        }
    }

    public void Dispose()
    {
        _device.Dispose();
    }
}
