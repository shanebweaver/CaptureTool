using CaptureTool.Application.Abstractions.AppMenu;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Domain.Capture.Abstractions;

namespace CaptureTool.Application.AppMenu;

internal class NewVideoCaptureAppCommand : INewVideoCaptureAppCommand
{
    private readonly IGoToImageCaptureAppCommand _goToImageCaptureAppCommand;
    private readonly CaptureOptions _captureOptions;

    public NewVideoCaptureAppCommand(IGoToImageCaptureAppCommand goToImageCaptureAppCommand)
    {
        _goToImageCaptureAppCommand = goToImageCaptureAppCommand;
        _captureOptions = CaptureOptions.VideoDefault;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        return _goToImageCaptureAppCommand.CanExecute(_captureOptions);
    }

    public void Execute()
    {
        _goToImageCaptureAppCommand.Execute(_captureOptions);
    }
}