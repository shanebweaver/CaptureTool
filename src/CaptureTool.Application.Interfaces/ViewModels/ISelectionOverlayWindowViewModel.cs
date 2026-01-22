using CaptureTool.Application.Interfaces.ViewModels.Options;
using CaptureTool.Common.Commands;
using CaptureTool.Domain.Capture.Interfaces;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ISelectionOverlayWindowViewModel : INotifyPropertyChanged, IDisposable
{
    event EventHandler<CaptureOptions>? CaptureOptionsUpdated;
    
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
    RelayCommand<CaptureOptions> UpdateCaptureOptionsCommand { get; }
    
    CaptureType? GetSelectedCaptureType();
    CaptureMode? GetSelectedCaptureMode();
    void Load(SelectionOverlayWindowOptions options);
}
