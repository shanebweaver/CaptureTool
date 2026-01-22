using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.Actions.AppMenu;
using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using CaptureTool.Application.Implementations.ViewModels.Helpers;
using System.Collections.ObjectModel;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class AppMenuViewModel : LoadableViewModelBase, IAppMenuViewModel
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

    private const string TelemetryContext = "AppMenu";

    private readonly IAppMenuActions _appMenuActions;
    private readonly ITelemetryService _telemetryService;
    private readonly IImageCaptureHandler _imageCaptureHandler;
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly IFactoryServiceWithArgs<IRecentCaptureViewModel, string> _recentCaptureViewModelFactory;

    public event EventHandler? RecentCapturesUpdated;

    public RelayCommand NewImageCaptureCommand { get; }
    public AsyncRelayCommand OpenFileCommand { get; }
    public RelayCommand NavigateToSettingsCommand { get; }
    public RelayCommand ShowAboutAppCommand { get; }
    public RelayCommand ShowAddOnsCommand { get; }
    public RelayCommand ExitApplicationCommand { get; }
    public RelayCommand RefreshRecentCapturesCommand { get; }
    public RelayCommand<IRecentCaptureViewModel> OpenRecentCaptureCommand { get; }

    public bool ShowAddOnsOption { get; }

    public ObservableCollection<IRecentCaptureViewModel> RecentCaptures
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
        IFactoryServiceWithArgs<IRecentCaptureViewModel, string> recentCaptureViewModelFactory)
    {
        _appMenuActions = appMenuActions;
        _telemetryService = telemetryService;
        _imageCaptureHandler = imageCaptureHandler;
        _videoCaptureHandler = videoCaptureHandler;
        _recentCaptureViewModelFactory = recentCaptureViewModelFactory;

        TelemetryCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        NewImageCaptureCommand = commandFactory.Create(ActivityIds.NewImageCapture, NewImageCapture);
        OpenFileCommand = commandFactory.CreateAsync(ActivityIds.OpenFile, OpenFileAsync);
        NavigateToSettingsCommand = commandFactory.Create(ActivityIds.NavigateToSettings, NavigateToSettings);
        ShowAboutAppCommand = commandFactory.Create(ActivityIds.ShowAboutApp, ShowAboutApp);
        ShowAddOnsCommand = commandFactory.Create(ActivityIds.ShowAddOns, ShowAddOns);
        ExitApplicationCommand = commandFactory.Create(ActivityIds.ExitApplication, ExitApplication);
        RefreshRecentCapturesCommand = new(RefreshRecentCaptures);
        OpenRecentCaptureCommand = commandFactory.Create<IRecentCaptureViewModel>(ActivityIds.OpenRecentCapture, OpenRecentCapture);

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
        _appMenuActions.NewImageCapture();
    }

    private async Task OpenFileAsync()
    {
        await _appMenuActions.OpenFileAsync(CancellationToken.None);
    }

    private void NavigateToSettings()
    {
        _appMenuActions.NavigateToSettings();
    }

    private void ShowAboutApp()
    {
        _appMenuActions.ShowAboutApp();
    }

    private void ShowAddOns()
    {
        _appMenuActions.ShowAddOns();
    }

    private void ExitApplication()
    {
        _appMenuActions.ExitApplication();
    }

    private void OpenRecentCapture(IRecentCaptureViewModel? model)
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
