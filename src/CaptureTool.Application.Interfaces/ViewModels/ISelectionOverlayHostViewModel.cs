using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ISelectionOverlayHostViewModel
{
    event EventHandler? AllScreensCaptureRequested;
    
    void UpdateOptions(CaptureMode captureMode, CaptureType captureType);
    void AddWindowViewModel(ISelectionOverlayWindowViewModel newVM, bool isPrimary = false);
}
