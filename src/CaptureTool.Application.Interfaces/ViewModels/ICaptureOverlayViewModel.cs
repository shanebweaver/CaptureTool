using CaptureTool.Common.Commands;
using CaptureTool.Domains.Capture.Interfaces;

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
    
    void Load(MonitorCaptureResult monitor, System.Drawing.Rectangle area);
}
