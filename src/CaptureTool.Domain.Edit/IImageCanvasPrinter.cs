using CaptureTool.Domain.Edit.Drawable;

namespace CaptureTool.Domain.Edit;

public interface IImageCanvasPrinter
{
    Task ShowPrintUIAsync(IDrawable[] drawables, ImageCanvasRenderOptions options, nint hwnd);
}