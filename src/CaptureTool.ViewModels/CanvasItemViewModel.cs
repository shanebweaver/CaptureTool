using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop.Annotation;

namespace CaptureTool.ViewModels;

public abstract partial class CanvasItemViewModel : ViewModelBase
{
    private int _left;
    public int Left
    {
        get => _left;
        set => Set(ref _left, value);
    }

    private int _top;
    public int Top
    {
        get => _top;
        set => Set(ref _top, value);
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (parameter is AnnotationItem annotationItem)
        {
            Left = annotationItem.Left;
            Top = annotationItem.Top;
        }

        return base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        Left = 0;
        Top = 0;
        base.Unload();
    }
}
