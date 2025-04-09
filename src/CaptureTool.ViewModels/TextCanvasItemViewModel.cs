using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop.Annotation;

namespace CaptureTool.ViewModels;

public sealed partial class TextCanvasItemViewModel : CanvasItemViewModel
{
    private string? _text;
    public string? Text
    {
        get => _text;
        set => Set(ref _text, value);
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Left = 50;
        Top = 100;

        if (parameter is TextAnnotationItem textAnnotation)
        {
            Text = textAnnotation.Text;
        }

        return base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        Text = null;
        base.Unload();
    }
}