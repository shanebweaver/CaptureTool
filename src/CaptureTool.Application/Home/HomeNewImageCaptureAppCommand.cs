using CaptureTool.Application.Abstractions.Home;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Domain.Capture.Abstractions;

namespace CaptureTool.Application.Home;

public sealed partial class HomeNewImageCaptureAppCommand : IHomeNewImageCaptureAppCommand
{
    private readonly IGoToImageCaptureAppCommand _goToImageCaptureAppCommand;
    private readonly CaptureOptions _captureOptions;
    
    public HomeNewImageCaptureAppCommand(IGoToImageCaptureAppCommand goToImageCaptureAppCommand)
    {
        _goToImageCaptureAppCommand = goToImageCaptureAppCommand;
        _captureOptions = CaptureOptions.ImageDefault;
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