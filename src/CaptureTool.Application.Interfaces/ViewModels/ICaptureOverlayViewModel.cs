using CaptureTool.Application.Interfaces.ViewModels.Options;
using CaptureTool.Common.Commands;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ICaptureOverlayViewModel
{
    bool IsRecording { get; }
    bool IsPaused { get; }
    TimeSpan CaptureTime { get; }
    Infrastructure.Interfaces.Themes.AppTheme CurrentAppTheme { get; }
    Infrastructure.Interfaces.Themes.AppTheme DefaultAppTheme { get; }
    bool IsDesktopAudioEnabled { get; }
    RelayCommand CloseOverlayCommand { get; }
    RelayCommand GoBackCommand { get; }
    RelayCommand StartVideoCaptureCommand { get; }
    RelayCommand StopVideoCaptureCommand { get; }
    RelayCommand ToggleDesktopAudioCommand { get; }
    RelayCommand TogglePauseResumeCommand { get; }
    
    void Load(CaptureOverlayViewModelOptions options);
}
