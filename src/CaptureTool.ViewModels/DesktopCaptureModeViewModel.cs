using CaptureTool.Capture.Desktop;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Telemetry;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public sealed partial class DesktopCaptureModeViewModel : LoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "DesktopCaptureModeViewModel_Load";
        public static readonly string Unload = "DesktopCaptureModeViewModel_Unload";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly ICancellationService _cancellationService;
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
        set => Set(ref _captureMode, value);
    }

    public DesktopCaptureModeViewModel(
        ITelemetryService telemetryService,
        ICancellationService cancellationService,
        ILocalizationService localizationService)
    {
        _telemetryService = telemetryService;
        _cancellationService = cancellationService;
        _localizationService = localizationService;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Debug.Assert(IsUnloaded);
        StartLoading();

        string activityId = ActivityIds.Load;
        _telemetryService.ActivityInitiated(activityId);

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            if (parameter is DesktopCaptureMode captureMode)
            {
                CaptureMode = captureMode;
                DisplayName = _localizationService.GetString($"DesktopCaptureMode_{Enum.GetName(CaptureMode.Value)}");
            }

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (OperationCanceledException)
        {
            _telemetryService.ActivityCanceled(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
        finally
        {
            cts.Dispose();
        }

        await base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        string activityId = ActivityIds.Unload;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            CaptureMode = null;
            DisplayName = null;

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }

        base.Unload();
    }
}
