using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Audio.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Abstractions.Settings.OpenSettingsPage;
using CaptureTool.Application.Abstractions.Shell.About.OpenAboutPage;
using CaptureTool.Application.Abstractions.Shell.AppMenu.ExitApplication;
using CaptureTool.Application.Abstractions.Shell.AppMenu.OpenFile;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.Store.OpenStorePage;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Factories;
using CaptureTool.Presentation.Features.RecentCaptures;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CaptureTool.Presentation.Shell;

public sealed partial class AppMenuViewModel : LoadableViewModelBase
{
    private readonly IImageCaptureState _imageCaptureState;
    private readonly IVideoCaptureState _videoCaptureState;
    private readonly IAudioCaptureState _audioCaptureState;
    private readonly IOpenFileUseCase _openFileCommand;
    private readonly IOpenRecentCaptureUseCase _openRecentCaptureCommand;
    private readonly IGetRecentCapturesUseCase _getRecentCapturesQuery;
    private readonly IOpenSelectionOverlayUseCase _openSelectionOverlayCommand;
    private readonly IOpenAudioCapturePageUseCase _openAudioCapturePageCommand;
    private readonly IOpenStorePageUseCase _openStorePageCommand;
    private readonly IExitApplicationUseCase _exitApplicationCommand;
    private readonly IEditSessionGuard _editSessionGuard;
    private readonly IFactoryServiceWithArgs<RecentCaptureViewModel, string> _recentCaptureViewModelFactory;
    private readonly IRecentCapturesChangeNotifier _recentCapturesChangeNotifier;
    private int _recentCapturesRefreshVersion;

    public event EventHandler? RecentCapturesUpdated;

    public IAsyncRelayCommand NewImageCaptureCommand { get; }
    public IAsyncRelayCommand NewVideoCaptureCommand { get; }
    public IAsyncRelayCommand NewAudioCaptureCommand { get; }
    public IAsyncRelayCommand OpenFileCommand { get; }
    public IRelayCommand NavigateToSettingsCommand { get; }
    public IRelayCommand ShowAboutAppCommand { get; }
    public IAsyncRelayCommand ShowAddOnsCommand { get; }
    public IAsyncRelayCommand ExitApplicationCommand { get; }
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
        IOpenAudioCapturePageUseCase openAudioCapturePageCommand,
        IOpenSettingsPageUseCase openSettingsPageCommand,
        IOpenAboutPageUseCase openAboutPageCommand,
        IOpenStorePageUseCase openStorePageCommand,
        IOpenFileUseCase openFileCommand,
        IExitApplicationUseCase exitApplicationCommand,
        IOpenRecentCaptureUseCase openRecentCaptureCommand,
        IGetRecentCapturesUseCase getRecentCapturesQuery,
        IStoreFeatureAvailability storeFeatureAvailability,
        IImageCaptureState imageCaptureState,
        IVideoCaptureState videoCaptureState,
        IAudioCaptureState audioCaptureState,
        IFactoryServiceWithArgs<RecentCaptureViewModel, string> recentCaptureViewModelFactory,
        IRecentCapturesChangeNotifier recentCapturesChangeNotifier,
        IEditSessionGuard? editSessionGuard = null,
        ITelemetryService? telemetryService = null)
    {
        _imageCaptureState = imageCaptureState;
        _videoCaptureState = videoCaptureState;
        _audioCaptureState = audioCaptureState;
        _openFileCommand = openFileCommand;
        _openRecentCaptureCommand = openRecentCaptureCommand;
        _getRecentCapturesQuery = getRecentCapturesQuery;
        _openSelectionOverlayCommand = openSelectionOverlayCommand;
        _openAudioCapturePageCommand = openAudioCapturePageCommand;
        _openStorePageCommand = openStorePageCommand;
        _exitApplicationCommand = exitApplicationCommand;
        _editSessionGuard = editSessionGuard ?? new AllowEditSessionGuard();
        _recentCaptureViewModelFactory = recentCaptureViewModelFactory;
        _recentCapturesChangeNotifier = recentCapturesChangeNotifier;

        NewImageCaptureCommand = TelemetryCommandFactory.Async(
            "new_image_capture",
            () => OpenSelectionOverlayAsync(CaptureOptions.ImageDefault),
            telemetryService,
            "app_menu");
        NewVideoCaptureCommand = TelemetryCommandFactory.Async(
            "new_video_capture",
            () => OpenSelectionOverlayAsync(CaptureOptions.VideoDefault),
            telemetryService,
            "app_menu");
        NewAudioCaptureCommand = TelemetryCommandFactory.Async(
            "new_audio_capture",
            OpenAudioCapturePageAsync,
            telemetryService,
            "app_menu");
        OpenFileCommand = TelemetryCommandFactory.Async(
            "open_file",
            OpenFileAsync,
            telemetryService,
            "app_menu");
        NavigateToSettingsCommand = TelemetryCommandFactory.Async(
            "open_settings",
            async () => await openSettingsPageCommand.ExecuteAsync(
                new OpenSettingsPageRequest(),
                CancellationToken.None),
            telemetryService,
            "app_menu");
        ShowAboutAppCommand = TelemetryCommandFactory.Async(
            "open_about",
            async () => await openAboutPageCommand.ExecuteAsync(
                new OpenAboutPageRequest(),
                CancellationToken.None),
            telemetryService,
            "app_menu");
        ShowAddOnsCommand = TelemetryCommandFactory.Async(
            "open_add_ons",
            OpenStorePageAsync,
            telemetryService,
            "app_menu");
        ExitApplicationCommand = TelemetryCommandFactory.Async(
            "exit_application",
            ExitApplicationAsync,
            telemetryService,
            "app_menu");
        RefreshRecentCapturesCommand = new AsyncRelayCommand(RefreshRecentCapturesAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        OpenRecentCaptureCommand = TelemetryCommandFactory.Async<RecentCaptureViewModel>(
            "open_recent_capture",
            OpenRecentCaptureAsync,
            telemetryService,
            "app_menu");

        ShowAddOnsOption = storeFeatureAvailability.IsStoreEnabled;
        RecentCaptures = [];
    }

    public override void Load()
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        _ = RefreshRecentCapturesAsync();
        _imageCaptureState.NewImageCaptured += OnNewImageCaptured;
        _videoCaptureState.NewVideoCaptured += OnNewVideoCaptured;
        _audioCaptureState.NewAudioCaptured += OnNewAudioCaptured;
        _recentCapturesChangeNotifier.RecentCapturesChanged += OnRecentCapturesChanged;

        base.Load();
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
        _ = RefreshRecentCapturesAsync();
    }

    private void OnNewVideoCaptured(object? sender, VideoFile e)
    {
        _ = RefreshRecentCapturesAsync();
    }

    private void OnNewAudioCaptured(object? sender, AudioFile e)
    {
        _ = RefreshRecentCapturesAsync();
    }

    private void OnRecentCapturesChanged(object? sender, EventArgs e)
    {
        _ = RefreshRecentCapturesAsync();
    }

    private async Task OpenFileAsync()
    {
        if (!await _editSessionGuard.CanLeaveCurrentSessionAsync(CancellationToken.None))
        {
            return;
        }

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

        if (!await _editSessionGuard.CanLeaveCurrentSessionAsync(CancellationToken.None))
        {
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
        int refreshVersion = Interlocked.Increment(ref _recentCapturesRefreshVersion);
        var recentCaptures = (await _getRecentCapturesQuery.ExecuteAsync(new GetRecentCapturesRequest(), CancellationToken.None)).Value?.Captures ?? [];
        if (refreshVersion != Volatile.Read(ref _recentCapturesRefreshVersion))
        {
            return;
        }

        _recentCaptures.Clear();
        foreach (var recentCapture in recentCaptures)
        {
            var recentCaptureViewModel = _recentCaptureViewModelFactory.Create(recentCapture.FilePath);
            _recentCaptures.Add(recentCaptureViewModel);
        }

        RecentCapturesUpdated?.Invoke(this, EventArgs.Empty);
    }

    private async Task OpenSelectionOverlayAsync(CaptureOptions captureOptions)
    {
        if (!await _editSessionGuard.CanLeaveCurrentSessionAsync(CancellationToken.None))
        {
            return;
        }

        await _openSelectionOverlayCommand.ExecuteAsync(new OpenSelectionOverlayRequest(captureOptions), CancellationToken.None);
    }

    private async Task OpenAudioCapturePageAsync()
    {
        if (!await _editSessionGuard.CanLeaveCurrentSessionAsync(CancellationToken.None))
        {
            return;
        }

        await _openAudioCapturePageCommand.ExecuteAsync(new OpenAudioCapturePageRequest(), CancellationToken.None);
    }

    private async Task OpenStorePageAsync()
    {
        if (!await _editSessionGuard.CanLeaveCurrentSessionAsync(CancellationToken.None))
        {
            return;
        }

        await _openStorePageCommand.ExecuteAsync(new OpenStorePageRequest(), CancellationToken.None);
    }

    private async Task ExitApplicationAsync()
    {
        if (!await _editSessionGuard.CanLeaveCurrentSessionAsync(CancellationToken.None))
        {
            return;
        }

        await _exitApplicationCommand.ExecuteAsync(new ExitApplicationRequest(), CancellationToken.None);
    }

    private sealed class AllowEditSessionGuard : IEditSessionGuard
    {
        public Task<bool> CanLeaveCurrentSessionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}
