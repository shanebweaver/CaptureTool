using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.ViewModels;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IAudioCapturePageViewModel : IViewModel
{
    IAppCommand PlayCommand { get; }
    IAppCommand StopCommand { get; }
    IAppCommand PauseCommand { get; }
    IAppCommand MuteCommand { get; }
    IAppCommand ToggleDesktopAudioCommand { get; }
    
    bool CanPlay { get; }
    bool IsPlaying { get; }
    bool IsPaused { get; }
    bool IsMuted { get; }
    bool IsDesktopAudioEnabled { get; }
}
