using CaptureTool.Edit.Drawable;
using System.Threading.Tasks;

namespace CaptureTool.Edit;

public interface IImageCanvasPrinter
{
    Task ShowPrintUIAsync(IDrawable[] drawables, ImageCanvasRenderOptions options);
}