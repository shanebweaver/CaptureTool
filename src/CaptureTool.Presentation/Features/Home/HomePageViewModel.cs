using CaptureTool.Application.Abstractions.Capture.Audio.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Metrics;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.Home;

public sealed partial class HomePageViewModel : AsyncLoadableViewModelBase
{
    private readonly IAppMetricsService _appMetricsService;
    private readonly IStoreService _storeService;

    public event EventHandler? StoreReviewPromptRequested;

    public IRelayCommand NewImageCaptureCommand { get; }
    public IRelayCommand NewVideoCaptureCommand { get; }
    public IRelayCommand NewAudioCaptureCommand { get; }
    public IAsyncRelayCommand LeaveStoreReviewCommand { get; }
    public IAsyncRelayCommand RemindStoreReviewLaterCommand { get; }
    public IAsyncRelayCommand DisableStoreReviewRemindersCommand { get; }

    public HomePageViewModel(
        IOpenSelectionOverlayUseCase openSelectionOverlayCommand,
        IOpenAudioCapturePageUseCase openAudioCapturePageCommand,
        IAppMetricsService appMetricsService,
        IStoreService storeService)
    {
        _appMetricsService = appMetricsService;
        _storeService = storeService;

        NewImageCaptureCommand = openSelectionOverlayCommand.ToRelayCommand(() => new OpenSelectionOverlayRequest(CaptureOptions.ImageDefault));
        NewVideoCaptureCommand = openSelectionOverlayCommand.ToRelayCommand(() => new OpenSelectionOverlayRequest(CaptureOptions.VideoDefault));
        NewAudioCaptureCommand = openAudioCapturePageCommand.ToRelayCommand(() => new OpenAudioCapturePageRequest());
        LeaveStoreReviewCommand = new AsyncRelayCommand(LeaveStoreReviewAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        RemindStoreReviewLaterCommand = new AsyncRelayCommand(RemindStoreReviewLaterAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        DisableStoreReviewRemindersCommand = new AsyncRelayCommand(DisableStoreReviewRemindersAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    public override async Task LoadAsync(CancellationToken cancellationToken)
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        if (_appMetricsService.ShouldShowStoreReviewReminder())
        {
            StoreReviewPromptRequested?.Invoke(this, EventArgs.Empty);
        }

        await base.LoadAsync(cancellationToken);
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
