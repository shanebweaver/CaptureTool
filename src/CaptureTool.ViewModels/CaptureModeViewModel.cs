using CaptureTool.Capture;
using CaptureTool.Core;
using CaptureTool.Services.Localization;
using System;

namespace CaptureTool.ViewModels;

public sealed partial class CaptureModeViewModel : ViewModelBase
{
    public CaptureMode CaptureMode { get; }
    public string DisplayName { get; }
    public string AutomationName { get; }
    public string IconSymbolName { get; }

    public CaptureModeViewModel(
        CaptureMode captureMode, 
        ILocalizationService localizationService)
    {
        CaptureMode = captureMode;

        string captureModeString = localizationService.GetString($"CaptureMode_{Enum.GetName(captureMode)}");
        DisplayName = captureModeString;
        AutomationName = captureModeString;

        IconSymbolName = CaptureMode switch
        {
            CaptureMode.Image => "Camera",
            CaptureMode.Video => "Video",
            _ => "Help",
        };
    }
}