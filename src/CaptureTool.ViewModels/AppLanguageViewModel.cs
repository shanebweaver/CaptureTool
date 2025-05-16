using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Telemetry;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public sealed partial class AppLanguageViewModel : LoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "AppLanguageViewModel_Load";
        public static readonly string Unload = "AppLanguageViewModel_Unload";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly ICancellationService _cancellationService;

    private string? _language;
    public string? Language
    {
        get => _language;
        set => Set(ref _language, value);
    }

    private string? _displayName;
    public string? DisplayName
    {
        get => _displayName;
        set => Set(ref _displayName, value);
    }

    public AppLanguageViewModel(
        ITelemetryService telemetryService,
        ICancellationService cancellationService)
    {
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
            if (parameter is string language)
            {
                Language = language;

                CultureInfo langInfo = new(language);
                DisplayName = langInfo.NativeName;
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
            Language = null;
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
