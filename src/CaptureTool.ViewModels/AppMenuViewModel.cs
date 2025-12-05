using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.FeatureManagement;
using CaptureTool.Core.Navigation;
using CaptureTool.Core.Telemetry;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces;
using CaptureTool.Services.Interfaces.FeatureManagement;
using CaptureTool.Services.Interfaces.Shutdown;
using CaptureTool.Services.Interfaces.Storage;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.Services.Interfaces.Windowing;
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

    private readonly IShutdownHandler _shutdownHandler;
    private readonly IAppNavigation _appNavigation;
    private readonly IWindowHandleProvider _windowingService;
    private readonly ITelemetryService _telemetryService;
    private readonly IFilePickerService _filePickerService;
    private readonly IStorageService _storageService;
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
        IShutdownHandler shutdownHandler,
        IAppNavigation appNavigation,
        ITelemetryService telemetryService,
        IWindowHandleProvider windowingService,
        IFilePickerService filePickerService,
        IFeatureManager featureManager,
        IStorageService storageService,
        IImageCaptureHandler imageCaptureHandler,
        IVideoCaptureHandler videoCaptureHandler,
        IFactoryServiceWithArgs<RecentCaptureViewModel, string> recentCaptureViewModelFactory)
    {
        _shutdownHandler = shutdownHandler;
        _appNavigation = appNavigation;
        _telemetryService = telemetryService;
        _windowingService = windowingService;
        _filePickerService = filePickerService;
        _storageService = storageService;
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
            _appNavigation.GoToImageCapture(CaptureOptions.ImageDefault);
        });
    }

    private Task OpenFileAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.OpenFile, async () =>
        {
            nint hwnd = _windowingService.GetMainWindowHandle();
            IFile file = await _filePickerService.PickFileAsync(hwnd, FilePickerType.Image, UserFolder.Pictures) 
                ?? throw new OperationCanceledException("No file was selected.");
            _appNavigation.GoToImageEdit(new ImageFile(file.FilePath));
        });
    }

    private void NavigateToSettings()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.NavigateToSettings, () =>
        {
            _appNavigation.GoToSettings();
        });
    }

    private void ShowAboutApp()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.ShowAboutApp, () =>
        {
            _appNavigation.GoToAbout();
        });
    }

    private void ShowAddOns()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.ShowAddOns, () =>
        {
            _appNavigation.GoToAddOns();
        });
    }

    private void ExitApplication()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.ExitApplication, () =>
        {
            _shutdownHandler.Shutdown();
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
                    switch (model.CaptureFileType)
                    {
                        case CaptureFileType.Image:
                            ImageFile imageFile = new(model.FilePath);
                            _appNavigation.GoToImageEdit(imageFile);
                            break;

                        case CaptureFileType.Video:
                            VideoFile videoFile = new(model.FilePath);
                            _appNavigation.GoToVideoEdit(videoFile);
                            break;
                    }

                }
            }
        });
    }

    public void RefreshRecentCaptures()
    {
        string recentCapturesFolder = _storageService.GetApplicationTemporaryFolderPath();

        var recentCaptureFiles = Directory.GetFiles(recentCapturesFolder, "*.*")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(5);

        RecentCaptures.Clear();
        foreach (var filePath in recentCaptureFiles)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                continue;
            }

            var recentCaptureViewModel = _recentCaptureViewModelFactory.Create(filePath);
            RecentCaptures.Add(recentCaptureViewModel);
        }

        RecentCapturesUpdated?.Invoke(this, EventArgs.Empty);
    }
}
