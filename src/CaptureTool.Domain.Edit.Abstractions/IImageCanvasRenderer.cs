using CaptureTool.Domain.Edit.Abstractions.Drawable;

namespace CaptureTool.Domain.Edit.Abstractions;

public interface IImageCanvasExporter
{
    Task CopyImageToClipboardAsync(IDrawable[] drawables, ImageCanvasRenderOptions options);
    Task SaveImageAsync(string filePath, IDrawable[] drawables, ImageCanvasRenderOptions options);
}