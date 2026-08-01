using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Audio.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.ClearRecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.RemoveRecentCapture;
using CaptureTool.Application.Abstractions.Metrics;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Factories;
using CaptureTool.Presentation.Features.RecentCaptures;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CaptureTool.Presentation.Features.Home;

public sealed partial class HomePageViewModel : AsyncLoadableViewModelBase
{
    private const int RecentCapturesPageSize = 24;

    private readonly IImageCaptureState _imageCaptureState;
    private readonly IVideoCaptureState _videoCaptureState;
    private readonly IAudioCaptureState _audioCaptureState;
    private readonly IAppMetricsService _appMetricsService;
    private readonly IStoreService _storeService;
    private readonly IClearRecentCapturesUseCase _clearRecentCapturesCommand;
    private readonly IRemoveRecentCaptureUseCase _removeRecentCaptureCommand;
    private readonly IGetRecentCapturesUseCase _getRecentCapturesQuery;
    private readonly IOpenRecentCaptureUseCase _openRecentCaptureCommand;
    private readonly IRecentCapturesChangeNotifier _recentCapturesChangeNotifier;
    private readonly IFactoryServiceWithArgs<RecentCaptureViewModel, string> _recentCaptureViewModelFactory;
    private readonly ObservableCollection<RecentCaptureViewModel> _recentCaptures = [];

    public event EventHandler? StoreReviewPromptRequested;

    public IRelayCommand NewImageCaptureCommand { get; }
    public IRelayCommand NewVideoCaptureCommand { get; }
    public IRelayCommand NewAudioCaptureCommand { get; }
    public IAsyncRelayCommand ClearRecentCapturesCommand { get; }
    public IAsyncRelayCommand<RecentCaptureViewModel> RemoveRecentCaptureCommand { get; }
    public IAsyncRelayCommand<RecentCaptureViewModel> OpenRecentCaptureCommand { get; }
    public IAsyncRelayCommand LoadMoreRecentCapturesCommand { get; }
    public IAsyncRelayCommand LeaveStoreReviewCommand { get; }
    public IAsyncRelayCommand RemindStoreReviewLaterCommand { get; }
    public IAsyncRelayCommand DisableStoreReviewRemindersCommand { get; }

    public ObservableCollection<RecentCaptureViewModel> RecentCaptures => _recentCaptures;

    public bool HasRecentCaptures => _recentCaptures.Count > 0;

    public bool IsRecentCapturesEmpty => HasLoadedRecentCaptures && !IsLoadingRecentCaptures && !HasRecentCaptures;

    public bool ShowRecentCapturesLoading => IsLoadingRecentCaptures && HasRecentCaptures;

    public bool HasLoadedRecentCaptures
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaiseRecentCaptureStateChanged();
            }
        }
    }

    public bool HasMoreRecentCaptures
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                LoadMoreRecentCapturesCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsLoadingRecentCaptures
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                LoadMoreRecentCapturesCommand.NotifyCanExecuteChanged();
                ClearRecentCapturesCommand.NotifyCanExecuteChanged();
                RaisePropertyChanged(nameof(IsRecentCapturesEmpty));
                RaisePropertyChanged(nameof(ShowRecentCapturesLoading));
            }
        }
    }

    public HomePageViewModel(
        IOpenSelectionOverlayUseCase openSelectionOverlayCommand,
        IOpenAudioCapturePageUseCase openAudioCapturePageCommand,
        IAppMetricsService appMetricsService,
        IStoreService storeService,
        IClearRecentCapturesUseCase clearRecentCapturesCommand,
        IRemoveRecentCaptureUseCase removeRecentCaptureCommand,
        IGetRecentCapturesUseCase getRecentCapturesQuery,
        IOpenRecentCaptureUseCase openRecentCaptureCommand,
        IRecentCapturesChangeNotifier recentCapturesChangeNotifier,
        IImageCaptureState imageCaptureState,
        IVideoCaptureState videoCaptureState,
        IAudioCaptureState audioCaptureState,
        IFactoryServiceWithArgs<RecentCaptureViewModel, string> recentCaptureViewModelFactory,
        ITelemetryService? telemetryService = null)
    {
        _imageCaptureState = imageCaptureState;
        _videoCaptureState = videoCaptureState;
        _audioCaptureState = audioCaptureState;
        _appMetricsService = appMetricsService;
        _storeService = storeService;
        _clearRecentCapturesCommand = clearRecentCapturesCommand;
        _removeRecentCaptureCommand = removeRecentCaptureCommand;
        _getRecentCapturesQuery = getRecentCapturesQuery;
        _openRecentCaptureCommand = openRecentCaptureCommand;
        _recentCapturesChangeNotifier = recentCapturesChangeNotifier;
        _recentCaptureViewModelFactory = recentCaptureViewModelFactory;

        NewImageCaptureCommand = TelemetryCommandFactory.Async(
            "new_image_capture",
            async () => await openSelectionOverlayCommand.ExecuteAsync(
                new OpenSelectionOverlayRequest(CaptureOptions.ImageDefault),
                CancellationToken.None),
            telemetryService,
            "home");
        NewVideoCaptureCommand = TelemetryCommandFactory.Async(
            "new_video_capture",
            async () => await openSelectionOverlayCommand.ExecuteAsync(
                new OpenSelectionOverlayRequest(CaptureOptions.VideoDefault),
                CancellationToken.None),
            telemetryService,
            "home");
        NewAudioCaptureCommand = TelemetryCommandFactory.Async(
            "new_audio_capture",
            async () => await openAudioCapturePageCommand.ExecuteAsync(
                new OpenAudioCapturePageRequest(),
                CancellationToken.None),
            telemetryService,
            "home");
        ClearRecentCapturesCommand = TelemetryCommandFactory.Async(
            "clear_recent_captures",
            ClearRecentCapturesAsync,
            telemetryService,
            "home",
            () => !IsLoadingRecentCaptures);
        RemoveRecentCaptureCommand = TelemetryCommandFactory.Async<RecentCaptureViewModel>(
            "remove_recent_capture",
            RemoveRecentCaptureAsync,
            telemetryService,
            "home");
        OpenRecentCaptureCommand = TelemetryCommandFactory.Async<RecentCaptureViewModel>(
            "open_recent_capture",
            OpenRecentCaptureAsync,
            telemetryService,
            "home");
        LoadMoreRecentCapturesCommand = new AsyncRelayCommand(LoadMoreRecentCapturesAsync, CanLoadMoreRecentCaptures, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        LeaveStoreReviewCommand = new AsyncRelayCommand(LeaveStoreReviewAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        RemindStoreReviewLaterCommand = new AsyncRelayCommand(RemindStoreReviewLaterAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        DisableStoreReviewRemindersCommand = new AsyncRelayCommand(DisableStoreReviewRemindersAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    public override async Task LoadAsync(CancellationToken cancellationToken)
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        _imageCaptureState.NewImageCaptured += OnNewImageCaptured;
        _videoCaptureState.NewVideoCaptured += OnNewVideoCaptured;
        _audioCaptureState.NewAudioCaptured += OnNewAudioCaptured;
        _recentCapturesChangeNotifier.RecentCapturesChanged += OnRecentCapturesChanged;

        if (_appMetricsService.ShouldShowStoreReviewReminder())
        {
            StoreReviewPromptRequested?.Invoke(this, EventArgs.Empty);
        }

        await RefreshRecentCapturesAsync(cancellationToken);
        await base.LoadAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _imageCaptureState.NewImageCaptured -= OnNewImageCaptured;
        _videoCaptureState.NewVideoCaptured -= OnNewVideoCaptured;
        _audioCaptureState.NewAudioCaptured -= OnNewAudioCaptured;
        _recentCapturesChangeNotifier.RecentCapturesChanged -= OnRecentCapturesChanged;
        base.Dispose();
    }

    private void OnNewImageCaptured(object? sender, ImageFile e)
    {
        _ = RefreshRecentCapturesAsync(CancellationToken.None);
    }

    private void OnNewVideoCaptured(object? sender, VideoFile e)
    {
        _ = RefreshRecentCapturesAsync(CancellationToken.None);
    }

    private void OnNewAudioCaptured(object? sender, AudioFile e)
    {
        _ = RefreshRecentCapturesAsync(CancellationToken.None);
    }

    private void OnRecentCapturesChanged(object? sender, EventArgs e)
    {
        _ = RefreshRecentCapturesAsync(CancellationToken.None);
    }

    private Task LoadMoreRecentCapturesAsync()
    {
        return LoadRecentCapturesPageAsync(reset: false, CancellationToken.None);
    }

    private bool CanLoadMoreRecentCaptures()
    {
        return HasMoreRecentCaptures && !IsLoadingRecentCaptures;
    }

    private async Task OpenRecentCaptureAsync(RecentCaptureViewModel? model)
    {
        if (model == null)
        {
            return;
        }

        var response = await _openRecentCaptureCommand.ExecuteAsync(new OpenRecentCaptureRequest(model.FilePath), CancellationToken.None);
        if (response.Value?.Opened != true)
        {
            await RefreshRecentCapturesAsync(CancellationToken.None);
        }
    }

    private async Task ClearRecentCapturesAsync()
    {
        var response = await _clearRecentCapturesCommand.ExecuteAsync(
            new ClearRecentCapturesRequest(),
            CancellationToken.None);

        if (response.Value?.Cleared == true)
        {
            await RefreshRecentCapturesAsync(CancellationToken.None);
        }
    }

    private async Task RemoveRecentCaptureAsync(RecentCaptureViewModel? model)
    {
        if (model == null)
        {
            return;
        }

        var response = await _removeRecentCaptureCommand.ExecuteAsync(
            new RemoveRecentCaptureRequest(model.FilePath),
            CancellationToken.None);

        if (response.Value?.Removed == true)
        {
            _recentCaptures.Remove(model);
            RaiseRecentCaptureStateChanged();
        }
        else if (response.Value is not null)
        {
            await RefreshRecentCapturesAsync(CancellationToken.None);
        }
    }

    private Task RefreshRecentCapturesAsync(CancellationToken cancellationToken)
    {
        return LoadRecentCapturesPageAsync(reset: true, cancellationToken);
    }

    private async Task LoadRecentCapturesPageAsync(bool reset, CancellationToken cancellationToken)
    {
        if (IsLoadingRecentCaptures || (!reset && !HasMoreRecentCaptures))
        {
            return;
        }

        IsLoadingRecentCaptures = true;

        try
        {
            if (reset)
            {
                _recentCaptures.Clear();
                HasMoreRecentCaptures = true;
                RaiseRecentCaptureStateChanged();
            }

            var response = await _getRecentCapturesQuery.ExecuteAsync(
                new GetRecentCapturesRequest(_recentCaptures.Count, RecentCapturesPageSize),
                cancellationToken);

            var recentCaptures = response.Value?.Captures ?? [];
            foreach (var recentCapture in recentCaptures)
            {
                _recentCaptures.Add(_recentCaptureViewModelFactory.Create(recentCapture.FilePath));
            }

            HasMoreRecentCaptures = response.Value?.HasMore == true;
            HasLoadedRecentCaptures = true;
        }
        finally
        {
            IsLoadingRecentCaptures = false;
            RaiseRecentCaptureStateChanged();
        }
    }

    private void RaiseRecentCaptureStateChanged()
    {
        RaisePropertyChanged(nameof(RecentCaptures));
        RaisePropertyChanged(nameof(HasRecentCaptures));
        RaisePropertyChanged(nameof(IsRecentCapturesEmpty));
        RaisePropertyChanged(nameof(ShowRecentCapturesLoading));
        ClearRecentCapturesCommand.NotifyCanExecuteChanged();
    }

    private async Task LeaveStoreReviewAsync()
    {
        bool launched = await _storeService.LaunchAppReviewAsync(CancellationToken.None);
        if (launched)
        {
            await _appMetricsService.SetStoreReviewRemindersEnabledAsync(false, CancellationToken.None);
        }
        else
        {
            await _appMetricsService.RemindAboutStoreReviewLaterAsync(CancellationToken.None);
        }
    }

    private async Task RemindStoreReviewLaterAsync()
    {
        await _appMetricsService.RemindAboutStoreReviewLaterAsync(CancellationToken.None);
    }

    private async Task DisableStoreReviewRemindersAsync()
    {
        await _appMetricsService.SetStoreReviewRemindersEnabledAsync(false, CancellationToken.None);
    }
}
