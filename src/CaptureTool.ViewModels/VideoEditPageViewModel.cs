using CaptureTool.Common.Storage;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public sealed partial class VideoEditPageViewModel : AsyncLoadableViewModelBase
{
    private string? _videoPath;
    public string? VideoPath
    {
        get => _videoPath;
        set => Set(ref _videoPath, value);
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (parameter is VideoFile video)
        {
            VideoPath = video.Path;
        }

        return base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        _videoPath = null;
        base.Unload();
    }
}
