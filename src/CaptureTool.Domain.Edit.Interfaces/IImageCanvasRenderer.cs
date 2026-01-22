using CaptureTool.Domain.Edit.Interfaces.Drawable;

namespace CaptureTool.Domain.Edit.Interfaces;

public interface IImageCanvasExporter
{
    Task CopyImageToClipboardAsync(IDrawable[] drawables, ImageCanvasRenderOptions options);
    Task SaveImageAsync(string filePath, IDrawable[] drawables, ImageCanvasRenderOptions options);
}