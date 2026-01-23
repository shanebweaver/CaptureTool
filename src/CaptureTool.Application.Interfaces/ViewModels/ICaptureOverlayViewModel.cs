using CaptureTool.Application.Interfaces.ViewModels.Options;
using System.Windows.Input;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ICaptureOverlayViewModel
{
    bool IsRecording { get; }
    bool IsPaused { get; }
    TimeSpan CaptureTime { get; }
    Infrastructure.Interfaces.Themes.AppTheme CurrentAppTheme { get; }
    Infrastructure.Interfaces.Themes.AppTheme DefaultAppTheme { get; }
    bool IsDesktopAudioEnabled { get; }
    ICommand CloseOverlayCommand { get; }
    ICommand GoBackCommand { get; }
    ICommand StartVideoCaptureCommand { get; }
    ICommand StopVideoCaptureCommand { get; }
    ICommand ToggleDesktopAudioCommand { get; }
    ICommand TogglePauseResumeCommand { get; }
    
    void Load(CaptureOverlayViewModelOptions options);
}
