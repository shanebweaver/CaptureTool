using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Implementations.ViewModels;
using CaptureTool.Infrastructure.Interfaces.Localization;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class CaptureModeViewModel : ViewModelBase, ICaptureModeViewModel
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