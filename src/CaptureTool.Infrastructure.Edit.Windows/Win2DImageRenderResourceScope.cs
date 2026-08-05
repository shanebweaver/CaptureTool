using CaptureTool.Domain.Edit.Drawable;
using Microsoft.Graphics.Canvas;

namespace CaptureTool.Infrastructure.Edit.Windows;

internal sealed class Win2DImageRenderResourceScope : IDisposable
{
    private readonly Dictionary<ImageDrawable, CanvasBitmap> _images;

    private Win2DImageRenderResourceScope(Dictionary<ImageDrawable, CanvasBitmap> images)
    {
        _images = images;
    }

    public static async Task<Win2DImageRenderResourceScope> CreateAsync(
        IEnumerable<IDrawable> drawables,
        ICanvasResourceCreator resourceCreator)
    {
        var images = new Dictionary<ImageDrawable, CanvasBitmap>();

        try
        {
            foreach (ImageDrawable drawable in drawables.OfType<ImageDrawable>())
            {
                if (images.ContainsKey(drawable))
                {
                    continue;
                }

                CanvasBitmap image;
                try
                {
                    image = await CanvasBitmap.LoadAsync(resourceCreator, drawable.File.FilePath);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"Unable to load image '{drawable.File.FilePath}' for rendering.",
                        exception);
                }

                images.Add(drawable, image);
            }

            return new Win2DImageRenderResourceScope(images);
        }
        catch
        {
            foreach (CanvasBitmap image in images.Values)
            {
                image.Dispose();
            }

            throw;
        }
    }

    public ICanvasImage GetImage(ImageDrawable drawable)
    {
        return _images.TryGetValue(drawable, out CanvasBitmap? image)
            ? image
            : throw new InvalidOperationException($"No render resource is available for image '{drawable.File.FilePath}'.");
    }

    public void Dispose()
    {
        foreach (CanvasBitmap image in _images.Values)
        {
            image.Dispose();
        }

        _images.Clear();
    }
}
