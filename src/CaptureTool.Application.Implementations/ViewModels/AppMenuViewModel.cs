using CaptureTool.Application.Implementations.ViewModels.Helpers;
using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Application.Interfaces.UseCases.AppMenu;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Implementations.ViewModels;
using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.Factories;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using System.Collections.ObjectModel;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class AppMenuViewModel : LoadableViewModelBase, IAppMenuViewModel
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = "LoadAppMenu";
        public static readonly string NewImageCapture = "NewImageCapture";
        public static readonly string NewVideoCapture = "NewVideoCapture";
        public static readonly string OpenFile = "OpenFile";
        public static readonly string NavigateToSettings = "NavigateToSettings";
        public static readonly string ShowAboutApp = "ShowAboutApp";
        public static readonly string ShowAddOns = "ShowAddOns";
        public static readonly string ExitApplication = "ExitApplication";
        public static readonly string SendFeedback = "SendFeedback";
        public static readonly string RefreshRecentCaptures = "RefreshRecentCaptures";
        public static readonly string OpenRecentCapture = "OpenRecentCapture";
    }

    private const string TelemetryContext = "AppMenu";

    private readonly IAppMenuUseCases _appMenuActions;
    private readonly IImageCaptureHandler _imageCaptureHandler;
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly IFactoryServiceWithArgs<IRecentCaptureViewModel, string> _recentCaptureViewModelFactory;

    public event EventHandler? RecentCapturesUpdated;

    public IAppCommand NewImageCaptureCommand { get; }
    public IAppCommand NewVideoCaptureCommand { get; }
    public IAsyncAppCommand OpenFileCommand { get; }
    public IAppCommand NavigateToSettingsCommand { get; }
    public IAppCommand ShowAboutAppCommand { get; }
    public IAppCommand ShowAddOnsCommand { get; }
    public IAppCommand ExitApplicationCommand { get; }
    public IAppCommand RefreshRecentCapturesCommand { get; }
    public IAppCommand<IRecentCaptureViewModel> OpenRecentCaptureCommand { get; }

    public bool ShowAddOnsOption { get; }
    public bool IsVideoCaptureEnabled { get; }

    private ObservableCollection<IRecentCaptureViewModel> _recentCaptures = [];

    public IReadOnlyList<IRecentCaptureViewModel> RecentCaptures
    {
        get => _recentCaptures;
        set
        {
            _recentCaptures = value as ObservableCollection<IRecentCaptureViewModel> ?? new ObservableCollection<IRecentCaptureViewModel>(value);
            RaisePropertyChanged(nameof(RecentCaptures));
        }
    }

    public AppMenuViewModel(
        IAppMenuUseCases appMenuActions,
        ITelemetryService telemetryService,
        IFeatureManager featureManager,
        IImageCaptureHandler imageCaptureHandler,
        IVideoCaptureHandler videoCaptureHandler,
        IFactoryServiceWithArgs<IRecentCaptureViewModel, string> recentCaptureViewModelFactory)
    {
        _appMenuActions = appMenuActions;
        _imageCaptureHandler = imageCaptureHandler;
        _videoCaptureHandler = videoCaptureHandler;
        _recentCaptureViewModelFactory = recentCaptureViewModelFactory;

        TelemetryAppCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        NewImageCaptureCommand = commandFactory.Create(ActivityIds.NewImageCapture, NewImageCapture);
        NewVideoCaptureCommand = commandFactory.Create(ActivityIds.NewVideoCapture, NewVideoCapture, () => IsVideoCaptureEnabled);
        OpenFileCommand = commandFactory.CreateAsync(ActivityIds.OpenFile, OpenFileAsync);
        NavigateToSettingsCommand = commandFactory.Create(ActivityIds.NavigateToSettings, NavigateToSettings);
        ShowAboutAppCommand = commandFactory.Create(ActivityIds.ShowAboutApp, ShowAboutApp);
        ShowAddOnsCommand = commandFactory.Create(ActivityIds.ShowAddOns, ShowAddOns);
        ExitApplicationCommand = commandFactory.Create(ActivityIds.ExitApplication, ExitApplication);
        RefreshRecentCapturesCommand = commandFactory.Create(ActivityIds.RefreshRecentCaptures, RefreshRecentCaptures);
        OpenRecentCaptureCommand = commandFactory.Create<IRecentCaptureViewModel>(ActivityIds.OpenRecentCapture, OpenRecentCapture);

        ShowAddOnsOption = featureManager.IsEnabled(CaptureToolFeatures.Feature_AddOns_Store);
        IsVideoCaptureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);
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

    private void NewVideoCapture()
    {
        _appMenuActions.NewVideoCapture();
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

        _recentCaptures.Clear();
        foreach (var recentCapture in recentCaptures)
        {
            var recentCaptureViewModel = _recentCaptureViewModelFactory.Create(recentCapture.FilePath);
            _recentCaptures.Add(recentCaptureViewModel);
        }

        RecentCapturesUpdated?.Invoke(this, EventArgs.Empty);
    }
}
