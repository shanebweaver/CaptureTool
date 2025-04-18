using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop;
using CaptureTool.Capture.Desktop.Annotation;

namespace CaptureTool.ViewModels.Annotation;

public sealed partial class ImageAnnotationViewModel : AnnotationItemViewModel
{
    private ImageFile? _imageFile;
    public ImageFile? ImageFile
    {
        get => _imageFile;
        set => Set(ref _imageFile, value);
    }

    public ImageAnnotationViewModel()
    {
        Left = 150;
        Top = 50;
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (parameter is ImageAnnotationItem imageAnnotation)
        {
            ImageFile = imageAnnotation.ImageFile;
        }

        return base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        ImageFile = null;
        base.Unload();
    }
}
