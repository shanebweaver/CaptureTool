using CaptureTool.Capture;
using CaptureTool.Services.Localization;
using System;

namespace CaptureTool.ViewModels;

public sealed partial class DesktopCaptureModeViewModel : LoadableViewModelBase
{
    public string DisplayName { get; }
    public CaptureMode CaptureMode { get; }

    public DesktopCaptureModeViewModel(
        CaptureMode captureMode,
        ILocalizationService localizationService)
    {
        CaptureMode = captureMode;
        DisplayName = localizationService.GetString($"DesktopCaptureMode_{Enum.GetName(CaptureMode)}");
    }
}
