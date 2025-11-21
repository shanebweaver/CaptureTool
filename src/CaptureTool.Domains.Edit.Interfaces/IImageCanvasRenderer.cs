using CaptureTool.Domains.Edit.Interfaces.Drawable;

namespace CaptureTool.Domains.Edit.Interfaces;

public interface IImageCanvasExporter
{
    Task CopyImageToClipboardAsync(IDrawable[] drawables, ImageCanvasRenderOptions options);
    Task SaveImageAsync(string filePath, IDrawable[] drawables, ImageCanvasRenderOptions options);
}