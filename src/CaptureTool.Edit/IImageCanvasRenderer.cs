using CaptureTool.Edit.Drawable;
using System.Threading.Tasks;

namespace CaptureTool.Edit;

public interface IImageCanvasExporter
{
    Task CopyImageToClipboardAsync(IDrawable[] drawables, ImageCanvasRenderOptions options);
    Task SaveImageAsync(string filePath, IDrawable[] drawables, ImageCanvasRenderOptions options);
}