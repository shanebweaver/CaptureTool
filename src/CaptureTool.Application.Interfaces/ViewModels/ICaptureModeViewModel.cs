using CaptureTool.Domain.Capture.Interfaces;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ICaptureModeViewModel
{
    CaptureMode CaptureMode { get; }
    string DisplayName { get; }
    string AutomationName { get; }
    string IconSymbolName { get; }
}
