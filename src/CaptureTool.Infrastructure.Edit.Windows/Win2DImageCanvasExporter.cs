using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
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
        ClipboardStreamSource clipboardImage = new(stream);
        await _clipboardService.CopyStreamAsync(clipboardImage);
    }

    public async Task<MemoryStream> RenderToStreamAsync(IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        using InMemoryRandomAccessStream stream = await RenderToRandomAccessStreamAsync(
            drawables,
            options,
            CanvasBitmapFileFormat.Png);

        var memoryStream = new MemoryStream();
        stream.Seek(0);
        await stream.AsStream().CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        return memoryStream;
    }

    public async Task SaveImageAsync(string filePath, IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        using InMemoryRandomAccessStream stream = await RenderToRandomAccessStreamAsync(
            drawables,
            options,
            GetFileFormat(filePath));

        await SaveStreamAtomicallyAsync(filePath, stream);
    }

    private static async Task SaveStreamAtomicallyAsync(string filePath, IRandomAccessStream stream)
    {
        await AtomicImageFileWriter.WriteAsync(filePath, async temporaryFilePath =>
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(temporaryFilePath);
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
        });
    }

    private static async Task<InMemoryRandomAccessStream> RenderToRandomAccessStreamAsync(
        IDrawable[] drawables,
        ImageCanvasRenderOptions options,
        CanvasBitmapFileFormat fileFormat)
    {
        float renderWidth = options.CropRect.Width;
        float renderHeight = options.CropRect.Height;

        CanvasDevice device = CanvasDevice.GetSharedDevice();
        using CanvasRenderTarget renderTarget = new(device, renderWidth, renderHeight, options.Dpi);
        using Win2DImageRenderResourceScope imageResources = await Win2DImageRenderResourceScope.CreateAsync(drawables, device);
        using CanvasDrawingSession drawingSession = renderTarget.CreateDrawingSession();

        Win2DImageCanvasRenderer.Render(drawables, options, drawingSession, imageResources.GetImage);

        drawingSession.Flush();

        var stream = new InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(stream, fileFormat);

        return stream;
    }

    private static CanvasBitmapFileFormat GetFileFormat(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => CanvasBitmapFileFormat.Jpeg,
            ".bmp" => CanvasBitmapFileFormat.Bmp,
            _ => CanvasBitmapFileFormat.Png,
        };
    }

    private sealed class ClipboardStreamSource : IClipboardStreamSource
    {
        private readonly Stream _stream;

        public ClipboardStreamSource(Stream stream)
        {
            _stream = stream;
        }

        public Stream GetStream() => _stream;
    }
}
