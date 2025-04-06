using System;
using System.Threading;
using System.Threading.Tasks;
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

    public DesktopCaptureMode? CaptureMode { get; private set; }

    public DesktopCaptureModeViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (parameter is DesktopCaptureMode desktopCaptureMode)
        {
            CaptureMode = desktopCaptureMode;
            DisplayName = _localizationService.GetString($"DesktopCaptureMode_{Enum.GetName(desktopCaptureMode)}");
        }

        return base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        _displayName = null;
        CaptureMode = null;
        base.Unload();
    }
}
