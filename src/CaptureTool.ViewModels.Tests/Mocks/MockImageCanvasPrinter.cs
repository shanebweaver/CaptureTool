using CaptureTool.Edit;
using CaptureTool.Edit.Drawable;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels.Tests.Mocks;

internal sealed partial class MockImageCanvasPrinter : IImageCanvasPrinter
{
    public Task ShowPrintUIAsync(IDrawable[] drawables, ImageCanvasRenderOptions options, nint hwnd)
    {
        return Task.CompletedTask;
    }
}
