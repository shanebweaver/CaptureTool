using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Features.ImageEdit.Rendering;
using CaptureTool.Domain.Edit.Drawable;
using Microsoft.Graphics.Canvas;
using Windows.Storage;
using Windows.Storage.Provider;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Edit.Windows;

public sealed partial class Win2DImageCanvasExporter : IImageCanvasExporter
{
    private readonly IClipboardService _clipboardService;

    public Win2DImageCanvasExporter(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }

    public async Task CopyImageToClipboardAsync(IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        using MemoryStream stream = await RenderToStreamAsync(drawables, options);
        SimpleClipboardStreamSource clipboardImage = new(stream);
        await _clipboardService.CopyStreamAsync(clipboardImage);
    }

    public async Task<MemoryStream> RenderToStreamAsync(IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        using InMemoryRandomAccessStream stream = await RenderToRandomAccessStreamAsync(drawables, options);

        var memoryStream = new MemoryStream();
        stream.Seek(0);
        await stream.AsStream().CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        return memoryStream;
    }

    public async Task SaveImageAsync(string filePath, IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        using InMemoryRandomAccessStream stream = await RenderToRandomAccessStreamAsync(drawables, options);

        StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
        CachedFileManager.DeferUpdates(file);

        using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
        {
            fileStream.Size = 0;
            stream.Seek(0);
            await RandomAccessStream.CopyAsync(stream, fileStream);
        }

        FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
        if (status != FileUpdateStatus.Complete)
        {
            throw new Exception("File could not be saved.");
        }
    }

    private static async Task<InMemoryRandomAccessStream> RenderToRandomAccessStreamAsync(IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        float renderWidth = options.CropRect.Width;
        float renderHeight = options.CropRect.Height;

        using CanvasRenderTarget renderTarget = new(CanvasDevice.GetSharedDevice(), renderWidth, renderHeight, options.Dpi);
        using CanvasDrawingSession drawingSession = renderTarget.CreateDrawingSession();

        Win2DImageCanvasRenderer.Render(drawables, options, drawingSession);

        drawingSession.Flush();

        var stream = new InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);

        return stream;
    }
}
