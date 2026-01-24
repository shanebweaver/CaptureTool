using CaptureTool.Application.Interfaces.ViewModels.Options;
using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.ViewModels;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ICaptureOverlayViewModel : IViewModel
{
    bool IsRecording { get; }
    bool IsPaused { get; }
    TimeSpan CaptureTime { get; }
    Infrastructure.Interfaces.Themes.AppTheme CurrentAppTheme { get; }
    Infrastructure.Interfaces.Themes.AppTheme DefaultAppTheme { get; }
    bool IsDesktopAudioEnabled { get; }
    IAppCommand CloseOverlayCommand { get; }
    IAppCommand GoBackCommand { get; }
    IAppCommand StartVideoCaptureCommand { get; }
    IAppCommand StopVideoCaptureCommand { get; }
    IAppCommand ToggleDesktopAudioCommand { get; }
    IAppCommand TogglePauseResumeCommand { get; }

    void Load(CaptureOverlayViewModelOptions options);
}
