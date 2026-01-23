using CaptureTool.Application.Interfaces.ViewModels.Options;
using CaptureTool.Common;
using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Themes;
using System.Collections.ObjectModel;
using System.Drawing;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ISelectionOverlayWindowViewModel : IViewModel
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
    AppTheme CurrentAppTheme { get; }
    AppTheme DefaultAppTheme { get; }
    bool IsDesktopAudioEnabled { get; }
    bool IsCapturingVideo { get; }
    IAppCommand RequestCaptureCommand { get; }
    IAppCommand CloseOverlayCommand { get; }
    IAppCommand<int> UpdateSelectedCaptureModeCommand { get; }
    IAppCommand<int> UpdateSelectedCaptureTypeCommand { get; }
    IAppCommand<Rectangle> UpdateCaptureAreaCommand { get; }
    IAppCommand<CaptureOptions> UpdateCaptureOptionsCommand { get; }
    
    CaptureType? GetSelectedCaptureType();
    CaptureMode? GetSelectedCaptureMode();
    void Load(SelectionOverlayWindowOptions options);
}
