using CaptureTool.Application.Abstractions.About;
using CaptureTool.Application.Abstractions.AppMenu;
using CaptureTool.Application.Abstractions.ImageCapture;
using CaptureTool.Application.Abstractions.RecentCaptures;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.VideoCapture;
using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.Abstractions.Factories;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class AppMenuViewModel : LoadableViewModelBase
{
    private readonly IImageCaptureHandler _imageCaptureHandler;
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly IOpenRecentCaptureAppCommand _openRecentCaptureAppCommand;
    private readonly IGetRecentCapturesAppQuery _getRecentCapturesAppQuery;
    private readonly IFactoryServiceWithArgs<RecentCaptureViewModel, string> _recentCaptureViewModelFactory;

    public event EventHandler? RecentCapturesUpdated;

    public IRelayCommand NewImageCaptureCommand { get; }
    public IRelayCommand NewVideoCaptureCommand { get; }
    public IAsyncRelayCommand OpenFileCommand { get; }
    public IRelayCommand NavigateToSettingsCommand { get; }
    public IRelayCommand ShowAboutAppCommand { get; }
    public IRelayCommand ShowAddOnsCommand { get; }
    public IRelayCommand ExitApplicationCommand { get; }
    public IRelayCommand RefreshRecentCapturesCommand { get; }
    public IRelayCommand<RecentCaptureViewModel> OpenRecentCaptureCommand { get; }

    public bool ShowAddOnsOption { get; }

    private ObservableCollection<RecentCaptureViewModel> _recentCaptures = [];

    public IReadOnlyList<RecentCaptureViewModel> RecentCaptures
    {
        get => _recentCaptures;
        set
        {
            _recentCaptures = value as ObservableCollection<RecentCaptureViewModel> ?? new ObservableCollection<RecentCaptureViewModel>(value);
            RaisePropertyChanged(nameof(RecentCaptures));
        }
    }

    public AppMenuViewModel(
        INewImageCaptureAppCommand newImageCaptureAppCommand,
        INewVideoCaptureAppCommand newVideoCaptureAppCommand,
        IOpenSettingsPageAppCommand openSettingsPageAppCommand,
        IOpenAboutPageAppCommand openAboutPageAppCommand,
        IOpenStorePageAppCommand openStorePageAppCommand,
        IOpenFileAsyncAppCommand openFileAsyncAppCommand,
        IExitApplicationAppCommand exitApplicationAppCommand,
        IOpenRecentCaptureAppCommand openRecentCaptureAppCommand,
        IGetRecentCapturesAppQuery getRecentCapturesAppQuery,
        IFeatureManager featureManager,
        IImageCaptureHandler imageCaptureHandler,
        IVideoCaptureHandler videoCaptureHandler,
        IFactoryServiceWithArgs<RecentCaptureViewModel, string> recentCaptureViewModelFactory)
    {
        _imageCaptureHandler = imageCaptureHandler;
        _videoCaptureHandler = videoCaptureHandler;
        _openRecentCaptureAppCommand = openRecentCaptureAppCommand;
        _getRecentCapturesAppQuery = getRecentCapturesAppQuery;
        _recentCaptureViewModelFactory = recentCaptureViewModelFactory;

        NewImageCaptureCommand = newImageCaptureAppCommand.ToRelayCommand();
        NewVideoCaptureCommand = newVideoCaptureAppCommand.ToRelayCommand();
        OpenFileCommand = openFileAsyncAppCommand.ToAsyncRelayCommand();
        NavigateToSettingsCommand = openSettingsPageAppCommand.ToRelayCommand();
        ShowAboutAppCommand = openAboutPageAppCommand.ToRelayCommand();
        ShowAddOnsCommand = openStorePageAppCommand.ToRelayCommand();
        ExitApplicationCommand = exitApplicationAppCommand.ToRelayCommand();
        RefreshRecentCapturesCommand = new RelayCommand(RefreshRecentCaptures);
        OpenRecentCaptureCommand = new RelayCommand<RecentCaptureViewModel>(OpenRecentCapture);

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

    private void OpenRecentCapture(RecentCaptureViewModel? model)
    {
        if (model != null)
        {
            if (!File.Exists(model.FilePath))
            {
                RefreshRecentCaptures();
            }
            else
            {
                _openRecentCaptureAppCommand.Execute(model.FilePath);
            }
        }
    }

    public void RefreshRecentCaptures()
    {
        // Execute async operation synchronously since this needs to be callable from non-async contexts
        // ConfigureAwait(false) helps avoid potential deadlocks
        var recentCaptures = _getRecentCapturesAppQuery.Execute();

        _recentCaptures.Clear();
        foreach (var recentCapture in recentCaptures)
        {
            var recentCaptureViewModel = _recentCaptureViewModelFactory.Create(recentCapture.FilePath);
            _recentCaptures.Add(recentCaptureViewModel);
        }

        RecentCapturesUpdated?.Invoke(this, EventArgs.Empty);
    }
}
