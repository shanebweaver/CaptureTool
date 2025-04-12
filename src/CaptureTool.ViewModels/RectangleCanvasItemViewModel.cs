using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop.Annotation;

namespace CaptureTool.ViewModels;

public sealed partial class RectangleCanvasItemViewModel : CanvasItemViewModel
{
    private float _height;
    public float Height
    {
        get => _height;
        set => Set(ref _height, value);
    }

    private float _width;
    public float Width
    {
        get => _width;
        set => Set(ref _width, value);
    }

    public RectangleCanvasItemViewModel()
    {

    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (parameter is RectangleShapeAnnotationItem rectangleAnnotation)
        {
            Height = rectangleAnnotation.Height;
            Width = rectangleAnnotation.Width;
        }

        return base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        Height = 0;
        Width = 0;
        base.Unload();
    }
}
