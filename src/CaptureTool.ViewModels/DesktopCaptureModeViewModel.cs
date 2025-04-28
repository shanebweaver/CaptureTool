using System;
using CaptureTool.Capture.Desktop;
using CaptureTool.Services.Localization;

namespace CaptureTool.ViewModels;

public sealed partial class DesktopCaptureModeViewModel : ViewModelBase
{
    private readonly ILocalizationService _localizationService;

    private string? _displayName;
    public string? DisplayName
    {
        get => _displayName;
        set => Set(ref  _displayName, value);
    }

    private DesktopCaptureMode? _captureMode;
    public DesktopCaptureMode? CaptureMode
    {
        get => _captureMode;
        set
        {
            Set(ref _captureMode, value);
            UpdateDisplayName();
        }
    }

    public DesktopCaptureModeViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    private void UpdateDisplayName()
    {
        if (CaptureMode == null)
        {
            DisplayName = null;
        }
        else
        {
            DisplayName = _localizationService.GetString($"DesktopCaptureMode_{Enum.GetName(CaptureMode.Value)}");
        }
    }
}
