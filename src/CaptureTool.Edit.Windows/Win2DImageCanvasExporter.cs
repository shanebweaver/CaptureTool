using CaptureTool.Edit.Drawable;
using CaptureTool.Services.Clipboard;
using Microsoft.Graphics.Canvas;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Provider;
using Windows.Storage.Streams;

namespace CaptureTool.Edit.Windows;

public sealed partial class Win2DImageCanvasExporter : IImageCanvasExporter
{
    private readonly IClipboardService _clipboardService;

    public Win2DImageCanvasExporter(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }

    public async Task CopyImageToClipboardAsync(IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        float renderWidth = options.CropRect.Width;
        float renderHeight = options.CropRect.Height;

        using CanvasRenderTarget renderTarget = new(CanvasDevice.GetSharedDevice(), renderWidth, renderHeight, options.Dpi);
        using CanvasDrawingSession drawingSession = renderTarget.CreateDrawingSession();

        Win2DImageCanvasRenderer.Render(drawables, options, drawingSession);
        drawingSession.Flush();

        using var stream = new InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);

        ClipboardImageWrapper clipboardImage = new(stream.AsStream());
        await _clipboardService.CopyImageAsync(clipboardImage);
    }

    public async Task SaveImageAsync(string filePath, IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        float renderWidth = options.CropRect.Width;
        float renderHeight = options.CropRect.Height;

        using CanvasRenderTarget renderTarget = new(CanvasDevice.GetSharedDevice(), renderWidth, renderHeight, options.Dpi);
        using CanvasDrawingSession drawingSession = renderTarget.CreateDrawingSession();

        Win2DImageCanvasRenderer.Render(drawables, options, drawingSession);

        drawingSession.Flush();

        using var stream = new InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);

        StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
        CachedFileManager.DeferUpdates(file);

        using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
        {
            stream.Seek(0);
            await RandomAccessStream.CopyAsync(stream, fileStream);
        }

        FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
        if (status != FileUpdateStatus.Complete)
        {
            throw new Exception("File could not be saved.");
        }
    }
}
