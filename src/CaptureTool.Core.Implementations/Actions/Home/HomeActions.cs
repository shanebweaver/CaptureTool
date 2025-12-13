using CaptureTool.Common.Commands.Extensions;
using CaptureTool.Core.Interfaces.Actions.Home;

namespace CaptureTool.Core.Implementations.Actions.Home;

public sealed partial class HomeActions : IHomeActions
{
    private readonly IHomeNewImageCaptureAction _newImageCapture;
    private readonly IHomeNewVideoCaptureAction _newVideoCapture;

    public HomeActions(
        IHomeNewImageCaptureAction newImageCapture,
        IHomeNewVideoCaptureAction newVideoCapture)
    {
        _newImageCapture = newImageCapture;
        _newVideoCapture = newVideoCapture;
    }

    public bool CanNewImageCapture() => _newImageCapture.CanExecute();
    public bool CanNewVideoCapture() => _newVideoCapture.CanExecute();
    public void NewImageCapture() => _newImageCapture.ExecuteCommand();
    public void NewVideoCapture() => _newVideoCapture.ExecuteCommand();
}
