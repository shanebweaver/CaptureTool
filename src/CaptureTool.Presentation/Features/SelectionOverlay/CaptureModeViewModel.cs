using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Presentation.ViewModels;

namespace CaptureTool.Presentation.Features.SelectionOverlay;

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