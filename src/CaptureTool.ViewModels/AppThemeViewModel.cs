using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Telemetry;
using CaptureTool.Services.Themes;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public sealed partial class AppThemeViewModel : LoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "Load";
        public static readonly string Unload = "Unload";
    }

    private readonly ILocalizationService _localizationService;
    private readonly ITelemetryService _telemetryService;
    private readonly ICancellationService _cancellationService;

    private AppTheme? _appTheme;
    public AppTheme? AppTheme
    {
        get => _appTheme;
        set => Set(ref _appTheme, value);
    }

    private string? _displayName;
    public string? DisplayName
    {
        get => _displayName;
        set => Set(ref _displayName, value);
    }

    public AppThemeViewModel(
        ILocalizationService localizationService,
        ITelemetryService telemetryService,
        ICancellationService cancellationService)
    {
        _localizationService = localizationService;
        _telemetryService = telemetryService;
        _cancellationService = cancellationService;
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
            if (parameter is AppTheme appTheme)
            {
                AppTheme = appTheme;
                DisplayName = _localizationService.GetString($"AppTheme_{Enum.GetName(appTheme)}");
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
            AppTheme = null;
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
