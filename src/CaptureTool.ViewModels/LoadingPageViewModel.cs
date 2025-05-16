using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Telemetry;
using CaptureTool.ViewModels.Commands;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public class LoadingPageViewModel : ViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "Load";
        public static readonly string Unload = "Unload";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly ICancellationService _cancellationService;
    private readonly INavigationService _navigationService;

    public RelayCommand GoBackCommand => new(GoBack);

    public LoadingPageViewModel(
        ITelemetryService telemetryService,
        ICancellationService cancellationService,
        INavigationService navigationService)
    {
        _telemetryService = telemetryService;
        _cancellationService = cancellationService;
        _navigationService = navigationService;
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
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }

        base.Unload();
    }

    private void GoBack()
    {
        _navigationService.GoBack();
    }
}
