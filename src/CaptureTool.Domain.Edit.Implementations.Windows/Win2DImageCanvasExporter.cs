using CaptureTool.Domain.Edit.Interfaces;
using CaptureTool.Domain.Edit.Interfaces.Drawable;
using CaptureTool.Infrastructure.Interfaces.Clipboard;
using Microsoft.Graphics.Canvas;
using Windows.Storage;
using Windows.Storage.Provider;
using Windows.Storage.Streams;

namespace CaptureTool.Domain.Edit.Implementations.Windows;

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

        CanvasDevice device = GetCanvasDevice();
        using CanvasRenderTarget renderTarget = new(device, renderWidth, renderHeight, options.Dpi);
        using CanvasDrawingSession drawingSession = renderTarget.CreateDrawingSession();

        Win2DImageCanvasRenderer.Render(drawables, options, drawingSession);
        drawingSession.Flush();

        using var stream = new InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);

        SimpleClipboardStreamSource clipboardImage = new(stream.AsStream());
        await _clipboardService.CopyStreamAsync(clipboardImage);
    }

    public async Task SaveImageAsync(string filePath, IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        float renderWidth = options.CropRect.Width;
        float renderHeight = options.CropRect.Height;

        CanvasDevice device = GetCanvasDevice();
        using CanvasRenderTarget renderTarget = new(device, renderWidth, renderHeight, options.Dpi);
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

    private static CanvasDevice GetCanvasDevice()
    {
        try
        {
            CanvasDevice? device = CanvasDevice.GetSharedDevice();
            if (device == null)
            {
                throw new InvalidOperationException(
                    "Failed to create Win2D device. Your system may not support the required Direct3D features. " +
                    "Please ensure your graphics drivers are up to date and your GPU supports DirectX 11.");
            }
            return device;
        }
        catch (Exception ex) when (ex.Message.Contains("0x887A0001", StringComparison.OrdinalIgnoreCase))
        {
            // DXGI_ERROR_UNSUPPORTED
            throw new InvalidOperationException(
                "Your graphics hardware does not support the required Direct3D features (DXGI_ERROR_UNSUPPORTED). " +
                "This application requires a GPU with DirectX 11 Feature Level 11.0 support.", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Failed to create Win2D device for image editing. " +
                "Please ensure your graphics drivers are up to date and your system supports DirectX 11.", ex);
        }
    }
}
