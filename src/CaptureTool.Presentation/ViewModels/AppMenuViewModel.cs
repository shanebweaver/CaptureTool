using CaptureTool.Application.Abstractions;
using CaptureTool.Application.Features.About.OpenAboutPage;
using CaptureTool.Application.Features.AppMenu.ExitApplication;
using CaptureTool.Application.Features.AppMenu.OpenFile;
using CaptureTool.Application.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Features.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Features.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Features.Settings.OpenSettingsPage;
using CaptureTool.Application.Features.Store.OpenStorePage;
using CaptureTool.Domain.Capture.Abstractions;
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
    private readonly IUseCase<OpenRecentCaptureRequest, OpenRecentCaptureResponse> _openRecentCaptureCommand;
    private readonly IUseCase<GetRecentCapturesRequest, GetRecentCapturesResponse> _getRecentCapturesQuery;
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
        IUseCase<OpenSelectionOverlayRequest, OpenSelectionOverlayResponse> openSelectionOverlayCommand,
        IUseCase<OpenSettingsPageRequest, OpenSettingsPageResponse> openSettingsPageCommand,
        IUseCase<OpenAboutPageRequest, OpenAboutPageResponse> openAboutPageCommand,
        IUseCase<OpenStorePageRequest, OpenStorePageResponse> openStorePageCommand,
        IUseCase<OpenFileRequest, OpenFileResponse> openFileCommand,
        IUseCase<ExitApplicationRequest, ExitApplicationResponse> exitApplicationCommand,
        IUseCase<OpenRecentCaptureRequest, OpenRecentCaptureResponse> openRecentCaptureCommand,
        IUseCase<GetRecentCapturesRequest, GetRecentCapturesResponse> getRecentCapturesQuery,
        IFeatureManager featureManager,
        IImageCaptureHandler imageCaptureHandler,
        IVideoCaptureHandler videoCaptureHandler,
        IFactoryServiceWithArgs<RecentCaptureViewModel, string> recentCaptureViewModelFactory)
    {
        _imageCaptureHandler = imageCaptureHandler;
        _videoCaptureHandler = videoCaptureHandler;
        _openRecentCaptureCommand = openRecentCaptureCommand;
        _getRecentCapturesQuery = getRecentCapturesQuery;
        _recentCaptureViewModelFactory = recentCaptureViewModelFactory;

        NewImageCaptureCommand = new RelayCommand(() => openSelectionOverlayCommand.ExecuteAsync(new OpenSelectionOverlayRequest(CaptureOptions.ImageDefault)).GetAwaiter().GetResult());
        NewVideoCaptureCommand = new RelayCommand(() => openSelectionOverlayCommand.ExecuteAsync(new OpenSelectionOverlayRequest(CaptureOptions.VideoDefault)).GetAwaiter().GetResult());
        OpenFileCommand = openFileCommand.ToAsyncRelayCommand(() => new OpenFileRequest());
        NavigateToSettingsCommand = openSettingsPageCommand.ToRelayCommand(() => new OpenSettingsPageRequest());
        ShowAboutAppCommand = openAboutPageCommand.ToRelayCommand(() => new OpenAboutPageRequest());
        ShowAddOnsCommand = openStorePageCommand.ToRelayCommand(() => new OpenStorePageRequest());
        ExitApplicationCommand = exitApplicationCommand.ToRelayCommand(() => new ExitApplicationRequest());
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
                _openRecentCaptureCommand.ExecuteAsync(new OpenRecentCaptureRequest(model.FilePath)).GetAwaiter().GetResult();
            }
        }
    }

    public void RefreshRecentCaptures()
    {
        // Execute async operation synchronously since this needs to be callable from non-async contexts
        // ConfigureAwait(false) helps avoid potential deadlocks
        var recentCaptures = _getRecentCapturesQuery.ExecuteAsync(new GetRecentCapturesRequest()).GetAwaiter().GetResult().Captures;

        _recentCaptures.Clear();
        foreach (var recentCapture in recentCaptures)
        {
            var recentCaptureViewModel = _recentCaptureViewModelFactory.Create(recentCapture.FilePath);
            _recentCaptures.Add(recentCaptureViewModel);
        }

        RecentCapturesUpdated?.Invoke(this, EventArgs.Empty);
    }
}
