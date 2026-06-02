using CaptureTool.Domain.Edit.Drawable;

namespace CaptureTool.Application.Abstractions.Features.ImageEdit.Rendering;

public interface IImageCanvasPrinter
{
    Task ShowPrintUIAsync(IDrawable[] drawables, ImageCanvasRenderOptions options, nint hwnd);
}
