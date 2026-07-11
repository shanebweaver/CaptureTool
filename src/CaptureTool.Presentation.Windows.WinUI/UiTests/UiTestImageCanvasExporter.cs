using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
using CaptureTool.Domain.Edit.Drawable;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal sealed class UiTestImageCanvasExporter : IImageCanvasExporter
{
    private static readonly byte[] TransparentPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];

    public Task CopyImageToClipboardAsync(IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        return Task.CompletedTask;
    }

    public Task<MemoryStream> RenderToStreamAsync(IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        return Task.FromResult(new MemoryStream(TransparentPng, writable: false));
    }

    public async Task SaveImageAsync(string filePath, IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        string? directoryPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await File.WriteAllBytesAsync(filePath, TransparentPng);
    }
}
