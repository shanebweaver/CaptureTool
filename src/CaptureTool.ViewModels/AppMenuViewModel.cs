using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.AppMenu;
using CaptureTool.Core.Interfaces.FeatureManagement;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces;
using CaptureTool.Services.Interfaces.FeatureManagement;
using CaptureTool.Services.Interfaces.Storage;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.ViewModels.Helpers;
using System.Collections.ObjectModel;

namespace CaptureTool.ViewModels;

public sealed partial class AppMenuViewModel : LoadableViewModelBase
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = "LoadAppMenu";
        public static readonly string NewImageCapture = "NewImageCapture";
        public static readonly string OpenFile = "OpenFile";
        public static readonly string NavigateToSettings = "NavigateToSettings";
        public static readonly string ShowAboutApp = "ShowAboutApp";
        public static readonly string ShowAddOns = "ShowAddOns";
        public static readonly string ExitApplication = "ExitApplication";
        public static readonly string SendFeedback = "SendFeedback";
        public static readonly string OpenRecentCapture = "OpenRecentCapture";
    }

    private readonly IAppMenuActions _appMenuActions;
    private readonly ITelemetryService _telemetryService;
    private readonly IImageCaptureHandler _imageCaptureHandler;
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly IFactoryServiceWithArgs<RecentCaptureViewModel, string> _recentCaptureViewModelFactory;

    public event EventHandler? RecentCapturesUpdated;

    public RelayCommand NewImageCaptureCommand { get; }
    public AsyncRelayCommand OpenFileCommand { get; }
    public RelayCommand NavigateToSettingsCommand { get; }
    public RelayCommand ShowAboutAppCommand { get; }
    public RelayCommand ShowAddOnsCommand { get; }
    public RelayCommand ExitApplicationCommand { get; }
    public RelayCommand RefreshRecentCapturesCommand { get; }
    public RelayCommand<RecentCaptureViewModel> OpenRecentCaptureCommand { get; }

    public bool ShowAddOnsOption { get; }

    public ObservableCollection<RecentCaptureViewModel> RecentCaptures
    {
        get => field;
        set => Set(ref field, value);
    }

    public AppMenuViewModel(
        IAppMenuActions appMenuActions,
        ITelemetryService telemetryService,
        IFeatureManager featureManager,
        IImageCaptureHandler imageCaptureHandler,
        IVideoCaptureHandler videoCaptureHandler,
        IFactoryServiceWithArgs<RecentCaptureViewModel, string> recentCaptureViewModelFactory)
    {
        _appMenuActions = appMenuActions;
        _telemetryService = telemetryService;
        _imageCaptureHandler = imageCaptureHandler;
        _videoCaptureHandler = videoCaptureHandler;
        _recentCaptureViewModelFactory = recentCaptureViewModelFactory;

        NewImageCaptureCommand = new(NewImageCapture);
        OpenFileCommand = new(OpenFileAsync);
        NavigateToSettingsCommand = new(NavigateToSettings);
        ShowAboutAppCommand = new(ShowAboutApp);
        ShowAddOnsCommand = new(ShowAddOns);
        ExitApplicationCommand = new(ExitApplication);
        RefreshRecentCapturesCommand = new(RefreshRecentCaptures);
        OpenRecentCaptureCommand = new(OpenRecentCapture);

        ShowAddOnsOption = featureManager.IsEnabled(CaptureToolFeatures.Feature_AddOns_Store);
        RecentCaptures = [];
    }

    public override void Load()
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        RefreshRecentCaptures();
        _imageCaptureHandler.NewImageCaptured += OnNewImageCaptured;
        _videoCaptureHandler.NewVideoCaptured += OnNewVideoCaptured;

        base.Load();
    }

    public override void Dispose()
    {
        _imageCaptureHandler.NewImageCaptured -= OnNewImageCaptured;
        _videoCaptureHandler.NewVideoCaptured -= OnNewVideoCaptured;
        base.Dispose();
    }

    private void OnNewImageCaptured(object? sender, IImageFile e)
    {
        RefreshRecentCaptures();
    }

    private void OnNewVideoCaptured(object? sender, IVideoFile e)
    {
        RefreshRecentCaptures();
    }

    private void NewImageCapture()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.NewImageCapture, () =>
        {
            _appMenuActions.NewImageCapture();
        });
    }

    private Task OpenFileAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.OpenFile, async () =>
        {
            await _appMenuActions.OpenFileAsync(CancellationToken.None);
        });
    }

    private void NavigateToSettings()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.NavigateToSettings, () =>
        {
            _appMenuActions.NavigateToSettings();
        });
    }

    private void ShowAboutApp()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.ShowAboutApp, () =>
        {
            _appMenuActions.ShowAboutApp();
        });
    }

    private void ShowAddOns()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.ShowAddOns, () =>
        {
            _appMenuActions.ShowAddOns();
        });
    }

    private void ExitApplication()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.ExitApplication, () =>
        {
            _appMenuActions.ExitApplication();
        });
    }

    private void OpenRecentCapture(RecentCaptureViewModel? model)
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.OpenRecentCapture, () =>
        {
            if (model != null)
            {
                if (!File.Exists(model.FilePath))
                {
                    RefreshRecentCaptures();
                }
                else
                {
                    // Execute async operation synchronously since this is called from a command
                    // The command infrastructure doesn't support async void
                    // ConfigureAwait(false) helps avoid potential deadlocks
                    _appMenuActions.OpenRecentCaptureAsync(model.FilePath, CancellationToken.None)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                }
            }
        });
    }

    public void RefreshRecentCaptures()
    {
        // Execute async operation synchronously since this needs to be callable from non-async contexts
        // ConfigureAwait(false) helps avoid potential deadlocks
        var recentCaptures = _appMenuActions.LoadRecentCapturesAsync(CancellationToken.None)
            .ConfigureAwait(false).GetAwaiter().GetResult();

        RecentCaptures.Clear();
        foreach (var recentCapture in recentCaptures)
        {
            var recentCaptureViewModel = _recentCaptureViewModelFactory.Create(recentCapture.FilePath);
            RecentCaptures.Add(recentCaptureViewModel);
        }

        RecentCapturesUpdated?.Invoke(this, EventArgs.Empty);
    }
}
