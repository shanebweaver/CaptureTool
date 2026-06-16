using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.About.OpenAboutPage;
using CaptureTool.Application.Abstractions.Features.AppMenu.ExitApplication;
using CaptureTool.Application.Abstractions.Features.AppMenu.OpenFile;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Features.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Abstractions.Features.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Abstractions.Features.Settings.OpenSettingsPage;
using CaptureTool.Application.Abstractions.Features.Store;
using CaptureTool.Application.Abstractions.Features.Store.OpenStorePage;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;
using CaptureTool.Presentation.Factories;
using CaptureTool.Presentation.Features.RecentCaptures;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CaptureTool.Presentation.Shell;

public sealed partial class AppMenuViewModel : LoadableViewModelBase
{
    private readonly IImageCaptureHandler _imageCaptureHandler;
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly IOpenFileUseCase _openFileCommand;
    private readonly IOpenRecentCaptureUseCase _openRecentCaptureCommand;
    private readonly IGetRecentCapturesUseCase _getRecentCapturesQuery;
    private readonly IFactoryServiceWithArgs<RecentCaptureViewModel, string> _recentCaptureViewModelFactory;

    public event EventHandler? RecentCapturesUpdated;

    public IRelayCommand NewImageCaptureCommand { get; }
    public IRelayCommand NewVideoCaptureCommand { get; }
    public IAsyncRelayCommand OpenFileCommand { get; }
    public IRelayCommand NavigateToSettingsCommand { get; }
    public IRelayCommand ShowAboutAppCommand { get; }
    public IRelayCommand ShowAddOnsCommand { get; }
    public IRelayCommand ExitApplicationCommand { get; }
    public IAsyncRelayCommand RefreshRecentCapturesCommand { get; }
    public IAsyncRelayCommand<RecentCaptureViewModel> OpenRecentCaptureCommand { get; }

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
        IOpenSelectionOverlayUseCase openSelectionOverlayCommand,
        IOpenSettingsPageUseCase openSettingsPageCommand,
        IOpenAboutPageUseCase openAboutPageCommand,
        IOpenStorePageUseCase openStorePageCommand,
        IOpenFileUseCase openFileCommand,
        IExitApplicationUseCase exitApplicationCommand,
        IOpenRecentCaptureUseCase openRecentCaptureCommand,
        IGetRecentCapturesUseCase getRecentCapturesQuery,
        IStoreFeatureAvailability storeFeatureAvailability,
        IImageCaptureHandler imageCaptureHandler,
        IVideoCaptureHandler videoCaptureHandler,
        IFactoryServiceWithArgs<RecentCaptureViewModel, string> recentCaptureViewModelFactory)
    {
        _imageCaptureHandler = imageCaptureHandler;
        _videoCaptureHandler = videoCaptureHandler;
        _openFileCommand = openFileCommand;
        _openRecentCaptureCommand = openRecentCaptureCommand;
        _getRecentCapturesQuery = getRecentCapturesQuery;
        _recentCaptureViewModelFactory = recentCaptureViewModelFactory;

        NewImageCaptureCommand = openSelectionOverlayCommand.ToRelayCommand(() => new OpenSelectionOverlayRequest(CaptureOptions.ImageDefault));
        NewVideoCaptureCommand = openSelectionOverlayCommand.ToRelayCommand(() => new OpenSelectionOverlayRequest(CaptureOptions.VideoDefault));
        OpenFileCommand = new AsyncRelayCommand(OpenFileAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        NavigateToSettingsCommand = openSettingsPageCommand.ToRelayCommand(() => new OpenSettingsPageRequest());
        ShowAboutAppCommand = openAboutPageCommand.ToRelayCommand(() => new OpenAboutPageRequest());
        ShowAddOnsCommand = openStorePageCommand.ToRelayCommand(() => new OpenStorePageRequest());
        ExitApplicationCommand = exitApplicationCommand.ToRelayCommand(() => new ExitApplicationRequest());
        RefreshRecentCapturesCommand = new AsyncRelayCommand(RefreshRecentCapturesAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        OpenRecentCaptureCommand = new AsyncRelayCommand<RecentCaptureViewModel>(OpenRecentCaptureAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);

        ShowAddOnsOption = storeFeatureAvailability.IsStoreEnabled;
        RecentCaptures = [];
    }

    public override void Load()
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        _ = RefreshRecentCapturesAsync();
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
        _ = RefreshRecentCapturesAsync();
    }

    private void OnNewVideoCaptured(object? sender, IVideoFile e)
    {
        _ = RefreshRecentCapturesAsync();
    }

    private async Task OpenFileAsync()
    {
        var response = await _openFileCommand.ExecuteAsync(new OpenFileRequest(), CancellationToken.None);
        if (response.Value?.Opened == true)
        {
            await RefreshRecentCapturesAsync();
        }
    }

    private async Task OpenRecentCaptureAsync(RecentCaptureViewModel? model)
    {
        if (model == null)
        {
            return;
        }

        if (!File.Exists(model.FilePath))
        {
            await RefreshRecentCapturesAsync();
            return;
        }

        var response = await _openRecentCaptureCommand.ExecuteAsync(new OpenRecentCaptureRequest(model.FilePath), CancellationToken.None);
        if (response.Value?.Opened != true)
        {
            await RefreshRecentCapturesAsync();
        }
    }

    public async Task RefreshRecentCapturesAsync()
    {
        var recentCaptures = (await _getRecentCapturesQuery.ExecuteAsync(new GetRecentCapturesRequest(), CancellationToken.None)).Value?.Captures ?? [];

        _recentCaptures.Clear();
        foreach (var recentCapture in recentCaptures)
        {
            var recentCaptureViewModel = _recentCaptureViewModelFactory.Create(recentCapture.FilePath);
            _recentCaptures.Add(recentCaptureViewModel);
        }

        RecentCapturesUpdated?.Invoke(this, EventArgs.Empty);
    }
}
