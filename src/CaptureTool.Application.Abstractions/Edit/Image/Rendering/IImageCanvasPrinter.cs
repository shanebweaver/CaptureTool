using CaptureTool.Domain.Edit.Drawable;

namespace CaptureTool.Application.Abstractions.Edit.Image.Rendering;

public interface IImageCanvasPrinter
{
    Task ShowPrintUIAsync(IDrawable[] drawables, ImageCanvasRenderOptions options);
}
