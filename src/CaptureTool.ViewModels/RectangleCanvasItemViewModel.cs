using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop.Annotation;

namespace CaptureTool.ViewModels;

public sealed partial class RectangleCanvasItemViewModel : CanvasItemViewModel
{
    private double _height;
    public double Height
    {
        get => _height;
        set => Set(ref _height, value);
    }

    private double _width;
    public double Width
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
