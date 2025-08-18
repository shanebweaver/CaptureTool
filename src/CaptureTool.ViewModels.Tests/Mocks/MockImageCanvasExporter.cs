using CaptureTool.Edit;
using CaptureTool.Edit.Drawable;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels.Tests.Mocks;

internal sealed partial class MockImageCanvasExporter : IImageCanvasExporter
{
    public Task CopyImageToClipboardAsync(IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        return Task.CompletedTask;
    }

    public Task SaveImageAsync(string filePath, IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        return Task.CompletedTask;
    }
}
