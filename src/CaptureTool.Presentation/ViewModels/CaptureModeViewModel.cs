using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Implementations.ViewModels;
using CaptureTool.Infrastructure.Interfaces.Localization;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class CaptureModeViewModel : ViewModelBase
{
    public CaptureMode CaptureMode { get; }
    public string DisplayName { get; }
    public string AutomationName { get; }

    public CaptureModeViewModel(
        CaptureMode captureMode,
        ILocalizationService localizationService)
    {
        CaptureMode = captureMode;

        string captureModeString = localizationService.GetString($"CaptureMode_{Enum.GetName(captureMode)}");
        DisplayName = captureModeString;
        AutomationName = captureModeString;
    }
}