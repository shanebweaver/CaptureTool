using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.TaskEnvironment;
using CaptureTool.Services.Telemetry;
using CaptureTool.ViewModels.Commands;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace CaptureTool.ViewModels;

public sealed partial class AppTitleBarViewModel : ViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "Load";
        public static readonly string Unload = "Unload";
        public static readonly string GoBack = "GoBack";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly ICancellationService _cancellationService;
    private readonly INavigationService _navigationService;
    private readonly ITaskEnvironment _taskEnvironment;

    public RelayCommand GoBackCommand => new(GoBack);

    private bool _canGoBack;
    public bool CanGoBack
    {
        get => _canGoBack;
        set => Set(ref _canGoBack, value);
    }

    private string? _icon;
    public string? Icon
    {
        get => _icon;
        set => Set(ref _icon, value);
    }

    private string? _title;
    public string? Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    public AppTitleBarViewModel(
        ITelemetryService telemetryService,
        ICancellationService cancellationService,
        INavigationService navigationService,
        ITaskEnvironment taskEnvironment)
    {
        _telemetryService = telemetryService;
        _cancellationService = cancellationService;
        _navigationService = navigationService;
        _taskEnvironment = taskEnvironment;
        _icon = "ms-appx:///Assets/StoreLogo.png";
        _title = AppInfo.Current.DisplayInfo.DisplayName;
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Debug.Assert(IsUnloaded);
        StartLoading();

        string activityId = ActivityIds.Load;
        _telemetryService.ActivityInitiated(activityId);

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            CanGoBack = _navigationService.CanGoBack;
            _navigationService.Navigated += OnNavigated;

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

        return base.LoadAsync(parameter, cancellationToken);
    }

    private void OnNavigated(object? sender, NavigationEventArgs e)
    {
        _taskEnvironment.TryExecute(() =>
        {
            CanGoBack = _navigationService.CanGoBack;
        });
    }

    public override void Unload()
    {
        string activityId = ActivityIds.Unload;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            Icon = null;
            Title = null;
            CanGoBack = false;
            _navigationService.Navigated -= OnNavigated;

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
        string activityId = ActivityIds.GoBack;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            Debug.Assert(_navigationService.CanGoBack);
            _navigationService.GoBack();

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }
}
