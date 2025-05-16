using CaptureTool.Capture.Desktop;
using CaptureTool.Services.Localization;
using System;

namespace CaptureTool.ViewModels;

public sealed partial class DesktopCaptureModeViewModel : LoadableViewModelBase
{
    public string DisplayName { get; }
    public DesktopCaptureMode CaptureMode { get; }

    public DesktopCaptureModeViewModel(
        DesktopCaptureMode captureMode,
        ILocalizationService localizationService)
    {
        CaptureMode = captureMode;
        DisplayName = localizationService.GetString($"DesktopCaptureMode_{Enum.GetName(CaptureMode)}");
    }
}
