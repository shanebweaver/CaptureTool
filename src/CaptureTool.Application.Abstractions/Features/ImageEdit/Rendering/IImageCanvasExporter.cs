using CaptureTool.Domain.Edit.Drawable;

namespace CaptureTool.Application.Abstractions.Features.ImageEdit.Rendering;

public interface IImageCanvasExporter
{
    Task CopyImageToClipboardAsync(IDrawable[] drawables, ImageCanvasRenderOptions options);
    Task<MemoryStream> RenderToStreamAsync(IDrawable[] drawables, ImageCanvasRenderOptions options);
    Task SaveImageAsync(string filePath, IDrawable[] drawables, ImageCanvasRenderOptions options);
}
