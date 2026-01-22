using CaptureTool.Domain.Capture.Interfaces;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ISelectionOverlayHostViewModel
{
    event EventHandler? AllScreensCaptureRequested;
    
    void UpdateOptions(CaptureOptions options);
    void AddWindowViewModel(ISelectionOverlayWindowViewModel newVM, bool isPrimary = false);
}
