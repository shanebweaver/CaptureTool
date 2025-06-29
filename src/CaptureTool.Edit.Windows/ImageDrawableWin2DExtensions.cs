using System.Runtime.CompilerServices;
using CaptureTool.Edit.Drawable;
using Microsoft.Graphics.Canvas;

namespace CaptureTool.Edit.Windows;

public static class ImageDrawableWin2DExtensions
{
    // ConditionalWeakTable automatically removes entries when keys are garbage collected.
    private static readonly ConditionalWeakTable<ImageDrawable, ICanvasImage> _resources = [];

    public static void SetPreparedImage(this ImageDrawable drawable, ICanvasImage image)
    {
        _resources.Remove(drawable);
        _resources.Add(drawable, image);
    }

    public static ICanvasImage? GetPreparedImage(this ImageDrawable drawable)
    {
        return _resources.TryGetValue(drawable, out var image) ? image : null;
    }

    public static void RemovePreparedImage(this ImageDrawable drawable)
    {
        _resources.Remove(drawable);
    }
}