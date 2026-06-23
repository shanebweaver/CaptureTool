using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.OpenAudioCapturePage;
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
    private readonly IAudioCaptureHandler _audioCaptureHandler;
    private readonly IOpenFileUseCase _openFileCommand;
    private readonly IOpenRecentCaptureUseCase _openRecentCaptureCommand;
    private readonly IGetRecentCapturesUseCase _getRecentCapturesQuery;
    private readonly IOpenSelectionOverlayUseCase _openSelectionOverlayCommand;
    private readonly IOpenAudioCapturePageUseCase _openAudioCapturePageCommand;
    private readonly IOpenStorePageUseCase _openStorePageCommand;
    private readonly IExitApplicationUseCase _exitApplicationCommand;
    private readonly IEditSessionGuard _editSessionGuard;
    private readonly IFactoryServiceWithArgs<RecentCaptureViewModel, string> _recentCaptureViewModelFactory;

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
    public bool IsAudioCaptureEnabled { get; }

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
        IAudioCaptureFeatureAvailability audioCaptureFeatureAvailability,
        IStoreFeatureAvailability storeFeatureAvailability,
        IImageCaptureHandler imageCaptureHandler,
        IVideoCaptureHandler videoCaptureHandler,
        IAudioCaptureHandler audioCaptureHandler,
        IFactoryServiceWithArgs<RecentCaptureViewModel, string> recentCaptureViewModelFactory,
        IEditSessionGuard? editSessionGuard = null)
    {
        _imageCaptureHandler = imageCaptureHandler;
        _videoCaptureHandler = videoCaptureHandler;
        _audioCaptureHandler = audioCaptureHandler;
        _openFileCommand = openFileCommand;
        _openRecentCaptureCommand = openRecentCaptureCommand;
        _getRecentCapturesQuery = getRecentCapturesQuery;
        _openSelectionOverlayCommand = openSelectionOverlayCommand;
        _openAudioCapturePageCommand = openAudioCapturePageCommand;
        _openStorePageCommand = openStorePageCommand;
        _exitApplicationCommand = exitApplicationCommand;
        _editSessionGuard = editSessionGuard ?? new AllowEditSessionGuard();
        _recentCaptureViewModelFactory = recentCaptureViewModelFactory;

        NewImageCaptureCommand = new AsyncRelayCommand(() => OpenSelectionOverlayAsync(CaptureOptions.ImageDefault), AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        NewVideoCaptureCommand = new AsyncRelayCommand(() => OpenSelectionOverlayAsync(CaptureOptions.VideoDefault), AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        NewAudioCaptureCommand = new AsyncRelayCommand(OpenAudioCapturePageAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        OpenFileCommand = new AsyncRelayCommand(OpenFileAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        NavigateToSettingsCommand = openSettingsPageCommand.ToRelayCommand(() => new OpenSettingsPageRequest());
        ShowAboutAppCommand = openAboutPageCommand.ToRelayCommand(() => new OpenAboutPageRequest());
        ShowAddOnsCommand = new AsyncRelayCommand(OpenStorePageAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ExitApplicationCommand = new AsyncRelayCommand(ExitApplicationAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        RefreshRecentCapturesCommand = new AsyncRelayCommand(RefreshRecentCapturesAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        OpenRecentCaptureCommand = new AsyncRelayCommand<RecentCaptureViewModel>(OpenRecentCaptureAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);

        ShowAddOnsOption = storeFeatureAvailability.IsStoreEnabled;
        IsAudioCaptureEnabled = audioCaptureFeatureAvailability.IsAudioCaptureEnabled;
        RecentCaptures = [];
    }

    public override void Load()
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        _ = RefreshRecentCapturesAsync();
        _imageCaptureHandler.NewImageCaptured += OnNewImageCaptured;
        _videoCaptureHandler.NewVideoCaptured += OnNewVideoCaptured;
        _audioCaptureHandler.NewAudioCaptured += OnNewAudioCaptured;

        base.Load();
    }

    public override void Dispose()
    {
        _imageCaptureHandler.NewImageCaptured -= OnNewImageCaptured;
        _videoCaptureHandler.NewVideoCaptured -= OnNewVideoCaptured;
        _audioCaptureHandler.NewAudioCaptured -= OnNewAudioCaptured;
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

    private void OnNewAudioCaptured(object? sender, IAudioFile e)
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
        var recentCaptures = (await _getRecentCapturesQuery.ExecuteAsync(new GetRecentCapturesRequest(), CancellationToken.None)).Value?.Captures ?? [];

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
