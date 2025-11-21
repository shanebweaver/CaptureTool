using CaptureTool.Domains.Edit.Interfaces.Drawable;

namespace CaptureTool.Domains.Edit.Interfaces;

public interface IImageCanvasPrinter
{
    Task ShowPrintUIAsync(IDrawable[] drawables, ImageCanvasRenderOptions options, nint hwnd);
}