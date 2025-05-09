using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop;

namespace CaptureTool.ViewModels;

public sealed partial class VideoEditPageViewModel : ViewModelBase
{
    private VideoFile? _videoFile;
    public VideoFile? VideoFile
    {
        get => _videoFile;
        set => Set(ref _videoFile, value);
    }

    public VideoEditPageViewModel()
    {

    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (parameter is VideoFile videoFile)
        {
            VideoFile = videoFile;
        }

        return base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        VideoFile = null;
        base.Unload();
    }
}
