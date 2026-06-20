using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Infrastructure.Edit.Windows.ChromaKey;
using Microsoft.Graphics.Canvas;
using System.Runtime.CompilerServices;

namespace CaptureTool.Infrastructure.Edit.Windows;

public static class ImageDrawableWin2DExtensions
{
    // ConditionalWeakTable automatically removes entries when keys are garbage collected.
    private static readonly ConditionalWeakTable<ImageDrawable, ICanvasImage> _resources = [];
    private static readonly ConditionalWeakTable<ImageDrawable, ChromaKeyProcessor> _chromaKeyProcessors = [];

    public static void SetPreparedImage(this ImageDrawable drawable, ICanvasImage image)
    {
        if (_resources.TryGetValue(drawable, out ICanvasImage? existing) && existing is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _resources.Remove(drawable);
        _resources.Add(drawable, image);
    }

    public static ICanvasImage? GetPreparedImage(this ImageDrawable drawable)
    {
        return _resources.TryGetValue(drawable, out var image) ? image : null;
    }

    public static ChromaKeyProcessor GetChromaKeyProcessor(this ImageDrawable drawable)
    {
        return _chromaKeyProcessors.GetValue(drawable, _ => new ChromaKeyProcessor());
    }

    public static void RemovePreparedImage(this ImageDrawable drawable)
    {
        if (_resources.TryGetValue(drawable, out ICanvasImage? existing) && existing is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _resources.Remove(drawable);
    }
}
