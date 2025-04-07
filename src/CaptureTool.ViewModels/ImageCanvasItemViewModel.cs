using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

[WinRT.GeneratedBindableCustomProperty]
public sealed partial class ImageCanvasItemViewModel : CanvasItemViewModel
{
    private ImageFile? _imageFile;
    public ImageFile? ImageFile
    {
        get => _imageFile;
        set => Set(ref _imageFile, value);
    }

    public ImageCanvasItemViewModel()
    {
        Left = 150;
        Top = 50;
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (parameter is ImageFile imageFile)
        {
            ImageFile = imageFile;
        }

        return base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        ImageFile = null;
        base.Unload();
    }
}
