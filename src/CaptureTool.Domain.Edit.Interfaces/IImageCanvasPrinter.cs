using CaptureTool.Domain.Edit.Interfaces.Drawable;

namespace CaptureTool.Domain.Edit.Interfaces;

public interface IImageCanvasPrinter
{
    Task ShowPrintUIAsync(IDrawable[] drawables, ImageCanvasRenderOptions options, nint hwnd);
}