using CaptureTool.Domain.Edit.Abstractions.Drawable;

namespace CaptureTool.Domain.Edit.Abstractions;

public interface IImageCanvasPrinter
{
    Task ShowPrintUIAsync(IDrawable[] drawables, ImageCanvasRenderOptions options, nint hwnd);
}