using CaptureTool.Domain.Capture.Interfaces;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ICaptureTypeViewModel
{
    CaptureType CaptureType { get; }
    string DisplayName { get; }
    string AutomationName { get; }
    string IconGlyphName { get; }
}
