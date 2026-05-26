using CaptureTool.Application.Abstractions.Home;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Home;

public sealed partial class HomeNewAudioCaptureAppCommand : IHomeNewAudioCaptureAppCommand
{
    private readonly IGoToAudioCaptureAppCommand _goToAudioCaptureAppCommand;
    public HomeNewAudioCaptureAppCommand(IGoToAudioCaptureAppCommand goToAudioCaptureAppCommand)
    {
        _goToAudioCaptureAppCommand = goToAudioCaptureAppCommand;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        return _goToAudioCaptureAppCommand.CanExecute();
    }

    public void Execute()
    {
        _goToAudioCaptureAppCommand.Execute();
    }
}
