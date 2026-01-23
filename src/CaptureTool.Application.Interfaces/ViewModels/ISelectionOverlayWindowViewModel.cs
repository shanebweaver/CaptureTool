using CaptureTool.Application.Interfaces.ViewModels.Options;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Themes;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Input;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ISelectionOverlayWindowViewModel : INotifyPropertyChanged, IDisposable
{
    event EventHandler<CaptureOptions>? CaptureOptionsUpdated;
    
    bool IsPrimary { get; }
    IReadOnlyList<ICaptureTypeViewModel> SupportedCaptureTypes { get; }
    int SelectedCaptureTypeIndex { get; }
    IReadOnlyList<ICaptureModeViewModel> SupportedCaptureModes { get; }
    int SelectedCaptureModeIndex { get; }
    Rectangle CaptureArea { get; }
    MonitorCaptureResult? Monitor { get; }
    IList<Rectangle> MonitorWindows { get; }
    AppTheme CurrentAppTheme { get; }
    AppTheme DefaultAppTheme { get; }
    bool IsDesktopAudioEnabled { get; }
    bool IsCapturingVideo { get; }
    ICommand RequestCaptureCommand { get; }
    ICommand CloseOverlayCommand { get; }
    ICommand UpdateSelectedCaptureModeCommand { get; }
    ICommand UpdateSelectedCaptureTypeCommand { get; }
    ICommand UpdateCaptureAreaCommand { get; }
    ICommand UpdateCaptureOptionsCommand { get; }
    
    CaptureType? GetSelectedCaptureType();
    CaptureMode? GetSelectedCaptureMode();
    void Load(SelectionOverlayWindowOptions options);
}
