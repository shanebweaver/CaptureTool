using CaptureTool.Common.Commands;
using CaptureTool.Domains.Capture.Interfaces;
using System.Collections.ObjectModel;
using System.Drawing;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ISelectionOverlayWindowViewModel
{
    event EventHandler<(CaptureMode captureMode, CaptureType captureType)>? CaptureOptionsUpdated;
    
    bool IsPrimary { get; }
    ObservableCollection<ICaptureTypeViewModel> SupportedCaptureTypes { get; }
    int SelectedCaptureTypeIndex { get; }
    ObservableCollection<ICaptureModeViewModel> SupportedCaptureModes { get; }
    int SelectedCaptureModeIndex { get; }
    Rectangle CaptureArea { get; }
    MonitorCaptureResult? Monitor { get; }
    IList<Rectangle> MonitorWindows { get; }
    Infrastructure.Interfaces.Themes.AppTheme CurrentAppTheme { get; }
    Infrastructure.Interfaces.Themes.AppTheme DefaultAppTheme { get; }
    bool IsDesktopAudioEnabled { get; }
    bool IsCapturingVideo { get; }
    RelayCommand RequestCaptureCommand { get; }
    RelayCommand CloseOverlayCommand { get; }
    RelayCommand<int> UpdateSelectedCaptureModeCommand { get; }
    RelayCommand<int> UpdateSelectedCaptureTypeCommand { get; }
    RelayCommand<Rectangle> UpdateCaptureAreaCommand { get; }
    RelayCommand<(CaptureMode captureMode, CaptureType captureType)> UpdateCaptureOptionsCommand { get; }
    
    CaptureType? GetSelectedCaptureType();
    CaptureMode? GetSelectedCaptureMode();
    void Load(MonitorCaptureResult monitor, IList<Rectangle> monitorWindows, CaptureMode captureMode, CaptureType captureType);
}
