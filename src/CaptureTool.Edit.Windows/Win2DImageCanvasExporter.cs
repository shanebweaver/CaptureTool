using CaptureTool.Edit.Drawable;
using Microsoft.Graphics.Canvas;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Provider;
using Windows.Storage.Streams;

namespace CaptureTool.Edit.Windows;

public sealed partial class Win2DImageCanvasExporter : IImageCanvasExporter
{
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

        DataPackage dataPackage = new();
        dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
        Clipboard.SetContent(dataPackage);
        Clipboard.Flush();
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
