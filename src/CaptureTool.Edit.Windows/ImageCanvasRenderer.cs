using CaptureTool.Edit.Windows.Drawable;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Provider;
using Windows.Storage.Streams;
using Color = Windows.UI.Color;

namespace CaptureTool.Edit.Windows;

public static partial class ImageCanvasRenderer
{
    private static readonly Color ClearColor = Colors.Transparent;

    public static async Task CopyImageToClipboardAsync(IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        float renderWidth = options.CropRect.Width;
        float renderHeight = options.CropRect.Height;

        using CanvasRenderTarget renderTarget = new(CanvasDevice.GetSharedDevice(), renderWidth, renderHeight, options.Dpi);
        using CanvasDrawingSession drawingSession = renderTarget.CreateDrawingSession();

        drawingSession.Transform = CalculateTransform(options);

        foreach (IDrawable drawable in drawables)
        {
            drawable.Draw(drawingSession);
        }

        drawingSession.Flush();

        using var stream = new InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);

        DataPackage dataPackage = new();
        dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
        Clipboard.SetContent(dataPackage);
        Clipboard.Flush();
    }

    public static async Task SaveImageAsync(string filePath, IDrawable[] drawables, ImageCanvasRenderOptions options)
    {
        float renderWidth = options.CropRect.Width;
        float renderHeight = options.CropRect.Height;

        using CanvasRenderTarget renderTarget = new(CanvasDevice.GetSharedDevice(), renderWidth, renderHeight, options.Dpi);
        using CanvasDrawingSession drawingSession = renderTarget.CreateDrawingSession();

        drawingSession.Transform = CalculateTransform(options);

        foreach (IDrawable drawable in drawables)
        {
            drawable.Draw(drawingSession);
        }

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

    public static void Render(IDrawable[] drawables, ImageCanvasRenderOptions options, CanvasDrawingSession drawingSession)
    {
        // Clear the drawing session
        drawingSession.Clear(ClearColor);

        // Apply the final transform to the drawing session
        drawingSession.Transform = CalculateTransform(options);

        // Draw all the drawables
        foreach (IDrawable drawable in drawables)
        {
            drawable.Draw(drawingSession);
        }
    }

    private static Matrix3x2 CalculateTransform(ImageCanvasRenderOptions options)
    {
        return OrientationHelper.CalculateRenderTransform(options.CropRect, options.CanvasSize, options.Orientation);
    }
}
