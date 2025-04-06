using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Localization;

namespace CaptureTool.ViewModels;

public sealed partial class DesktopCaptureModeViewModel : ViewModelBase
{
    private readonly ICancellationService _cancellationService;
    private readonly ILocalizationService _localizationService;

    private string? _displayName;
    public string? DisplayName
    {
        get => _displayName;
        set => Set(ref  _displayName, value);
    }

    public DesktopCaptureMode? CaptureMode { get; private set; }

    public DesktopCaptureModeViewModel(
        ICancellationService cancellationService,
        ILocalizationService localizationService)
    {
        _cancellationService = cancellationService;
        _localizationService = localizationService;
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Unload();
        Debug.Assert(IsUnloaded);
        StartLoading();

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            if (parameter is DesktopCaptureMode desktopCaptureMode)
            {
                CaptureMode = desktopCaptureMode;
                DisplayName = _localizationService.GetString($"DesktopCaptureMode_{Enum.GetName(desktopCaptureMode)}");
            }
        }
        catch (OperationCanceledException)
        {
            // Load canceled
        }
        finally
        {
            cts.Dispose();
        }

        return base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        DisplayName = null;
        CaptureMode = null;
        base.Unload();
    }
}
