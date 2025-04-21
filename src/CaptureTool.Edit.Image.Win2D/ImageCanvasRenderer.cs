using System;
using System.Numerics;
using System.Threading.Tasks;
using CaptureTool.Edit.Image.Win2D.Drawable;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.UI;

namespace CaptureTool.Edit.Image.Win2D;

public static partial class ImageCanvasRenderer
{
    private static readonly Color ClearColor = Colors.White;

    public static async Task CopyImageToClipboardAsync(IDrawable[] drawables, float width, float height, float dpi = 96, Vector2? offset = null)
    {
        CanvasCommandList canvasCommandList = Render(drawables);
        await CopyImageToClipboardAsync(canvasCommandList, width, height, dpi, offset);
    }

    public static async Task CopyImageToClipboardAsync(CanvasCommandList commandList, float width, float height, float dpi = 96, Vector2? offset = null)
    {
        using CanvasRenderTarget renderTarget = new(CanvasDevice.GetSharedDevice(), width, height, dpi);
        using CanvasDrawingSession drawingSession = renderTarget.CreateDrawingSession();

        Vector2 sceneTopLeft = offset ?? new(0, 0);
        drawingSession.DrawImage(commandList, sceneTopLeft);
        drawingSession.Flush();

        using var stream = new InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);

        DataPackage dataPackage = new();
        dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
        Clipboard.SetContent(dataPackage);
        Clipboard.Flush();
    }

    public static CanvasCommandList Render(IDrawable[] drawables)
    {
        CanvasCommandList commandList = new(CanvasDevice.GetSharedDevice());
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
}
