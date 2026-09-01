using CaptureTool.Application.Abstractions.Analysis.Activity;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings.OpenSettingsPage;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.Analysis;
using CaptureTool.Presentation.Notifications;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CaptureTool.Presentation.Shell;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly IAppNotificationService _notificationService;
    private readonly ICaptureAnalysisActivityQueryService? _activityQueryService;
    private readonly ILocalizationService? _localizationService;
    private readonly IOpenSettingsPageUseCase? _openSettingsPageUseCase;
    private int _activityRefreshInProgress;

    public event EventHandler? BackgroundActivityRefreshRequested;

    public IRelayCommand DismissNotificationCommand { get; }

    public IAsyncRelayCommand OpenBackgroundActivitySettingsCommand { get; }

    public ObservableCollection<BackgroundActivityItemViewModel> BackgroundActivities
    {
        get;
    } = [];

    public AppTheme CurrentAppTheme
    {
        get;
        private set => Set(ref field, value);
    }

    public AppTheme DefaultAppTheme
    {
        get;
        private set => Set(ref field, value);
    }

    public AppNotification? CurrentNotification => _notificationService.CurrentNotification;

    public bool HasNotification => _notificationService.HasNotification;

    public string NotificationMessage => CurrentNotification?.Message ?? string.Empty;

    public bool IsCurrentNotificationError => CurrentNotification?.Kind == AppNotificationKind.Error;

    public bool IsCurrentNotificationInfo => CurrentNotification?.Kind == AppNotificationKind.Info;

    public bool CanMonitorBackgroundActivity => _activityQueryService != null;

    public bool HasBackgroundActivity
    {
        get;
        private set => Set(ref field, value);
    }

    public bool HasActiveBackgroundActivity
    {
        get;
        private set => Set(ref field, value);
    }

    public bool HasBackgroundActivityAttention
    {
        get;
        private set => Set(ref field, value);
    }

    public string BackgroundActivitySummary
    {
        get;
        private set => Set(ref field, value);
    } = string.Empty;

    public bool IsPrimaryActivityDeterminate
    {
        get;
        private set => Set(ref field, value);
    }

    public double PrimaryActivityProgress
    {
        get;
        private set => Set(ref field, value);
    }

    private INavigationRequest? _currentRequest;
    private bool _disposed;

    public MainWindowViewModel(
        IThemeService themeService,
        IAppNotificationService notificationService,
        ICaptureAnalysisActivityQueryService? activityQueryService = null,
        ILocalizationService? localizationService = null,
        IOpenSettingsPageUseCase? openSettingsPageUseCase = null)
    {
        _themeService = themeService;
        _notificationService = notificationService;
        _activityQueryService = activityQueryService;
        _localizationService = localizationService;
        _openSettingsPageUseCase = openSettingsPageUseCase;
        _themeService.CurrentThemeChanged += OnCurrentThemeChanged;
        _notificationService.PropertyChanged += OnNotificationServicePropertyChanged;
        if (_activityQueryService != null)
        {
            _activityQueryService.ActivityChanged += OnBackgroundActivityChanged;
        }
        DefaultAppTheme = _themeService.DefaultTheme;
        CurrentAppTheme = _themeService.CurrentTheme;
        DismissNotificationCommand = new RelayCommand(_notificationService.DismissCurrent);
        OpenBackgroundActivitySettingsCommand = new AsyncRelayCommand(
            OpenBackgroundActivitySettingsAsync,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    public async Task RefreshBackgroundActivityAsync(
        CancellationToken cancellationToken = default)
    {
        if (_activityQueryService == null ||
            Interlocked.Exchange(ref _activityRefreshInProgress, 1) != 0)
        {
            return;
        }

        try
        {
            CaptureAnalysisActivitySnapshot snapshot = await _activityQueryService
                .GetCurrentAsync(cancellationToken);
            ApplyBackgroundActivity(snapshot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            Interlocked.Exchange(ref _activityRefreshInProgress, 0);
        }
    }

    private void OnCurrentThemeChanged(object? sender, AppTheme newTheme)
    {
        CurrentAppTheme = newTheme;
    }

    private void OnNotificationServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IAppNotificationService.CurrentNotification) or
            nameof(IAppNotificationService.HasNotification) or
            nameof(IAppNotificationService.NotificationCount))
        {
            RaisePropertyChanged(nameof(CurrentNotification));
            RaisePropertyChanged(nameof(HasNotification));
            RaisePropertyChanged(nameof(NotificationMessage));
            RaisePropertyChanged(nameof(IsCurrentNotificationError));
            RaisePropertyChanged(nameof(IsCurrentNotificationInfo));
        }
    }

    private void OnBackgroundActivityChanged(object? sender, EventArgs e)
    {
        BackgroundActivityRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    public bool IsCurrentNavigationRequest(INavigationRequest request)
    {
        return _currentRequest?.Route == request.Route &&
            _currentRequest?.Parameter == request.Parameter;
    }

    public void CommitNavigationRequest(INavigationRequest request)
    {
        _currentRequest = request;
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _themeService.CurrentThemeChanged -= OnCurrentThemeChanged;
            _notificationService.PropertyChanged -= OnNotificationServicePropertyChanged;
            if (_activityQueryService != null)
            {
                _activityQueryService.ActivityChanged -= OnBackgroundActivityChanged;
            }
            _currentRequest = null;
        }
        finally
        {
            _disposed = true;
            base.Dispose();
        }
    }

    private void ApplyBackgroundActivity(CaptureAnalysisActivitySnapshot snapshot)
    {
        var items = new List<BackgroundActivityItemViewModel>();
        foreach (CaptureAnalysisModelPreparationActivity preparation in
            snapshot.ModelPreparations)
        {
            bool isDeterminate = preparation.HasReportedProgress;
            items.Add(new BackgroundActivityItemViewModel(
                GetModelPreparationTitle(preparation.Capability),
                isDeterminate
                    ? string.Format(
                        CultureInfo.CurrentCulture,
                        GetString(
                            "BackgroundActivity_ModelPreparationDetail",
                            "{0}% complete on this device"),
                        Math.Round(preparation.FractionComplete * 100))
                    : GetBackgroundWorkDetail(),
                isActive: true,
                isAttention: false,
                isDeterminate,
                preparation.FractionComplete));
        }

        if (snapshot.IsBackfillInProgress)
        {
            bool isDeterminate = snapshot.BackfillFractionComplete > 0;
            items.Add(new BackgroundActivityItemViewModel(
                GetString(
                    "BackgroundActivity_BackfillTitle",
                    "Preparing existing captures"),
                isDeterminate
                    ? string.Format(
                        CultureInfo.CurrentCulture,
                        GetString(
                            "BackgroundActivity_BackfillDetail",
                            "{0}% scheduled for search"),
                        Math.Round(snapshot.BackfillFractionComplete * 100))
                    : GetBackgroundWorkDetail(),
                isActive: true,
                isAttention: false,
                isDeterminate,
                snapshot.BackfillFractionComplete));
        }

        if (snapshot.RunningCaptureCount > 0 || snapshot.QueuedCaptureCount > 0)
        {
            string title = snapshot.RunningCaptureCount > 0
                ? GetString(
                    "BackgroundActivity_AnalysisTitle",
                    "Analyzing captures")
                : GetString(
                    "BackgroundActivity_QueuedTitle",
                    "Analysis queued");
            string detail = string.Format(
                CultureInfo.CurrentCulture,
                GetString(
                    "BackgroundActivity_AnalysisDetail",
                    "{0} active · {1} waiting"),
                snapshot.RunningCaptureCount,
                snapshot.QueuedCaptureCount);
            items.Add(new BackgroundActivityItemViewModel(
                title,
                detail,
                isActive: true,
                isAttention: false));
        }

        if (snapshot.WaitingCaptureCount > 0)
        {
            items.Add(new BackgroundActivityItemViewModel(
                GetString(
                    "BackgroundActivity_WaitingTitle",
                    "Waiting for AI readiness"),
                FormatCaptureCount(
                    "BackgroundActivity_WaitingDetail",
                    "{0} capture(s) are waiting for a model or another analysis step",
                    snapshot.WaitingCaptureCount),
                isActive: false,
                isAttention: false));
        }

        if (snapshot.RetryCaptureCount > 0)
        {
            items.Add(new BackgroundActivityItemViewModel(
                GetString(
                    "BackgroundActivity_RetryTitle",
                    "Analysis will retry"),
                FormatCaptureCount(
                    "BackgroundActivity_RetryDetail",
                    "{0} capture(s) are scheduled to retry",
                    snapshot.RetryCaptureCount),
                isActive: false,
                isAttention: false));
        }

        if (snapshot.FailedCaptureCount > 0)
        {
            items.Add(new BackgroundActivityItemViewModel(
                GetString(
                    "BackgroundActivity_FailedTitle",
                    "Captures need attention"),
                FormatCaptureCount(
                    "BackgroundActivity_FailedDetail",
                    "Analysis could not complete for {0} capture(s)",
                    snapshot.FailedCaptureCount),
                isActive: false,
                isAttention: true));
        }

        if (snapshot.NeedsMemoryRecovery)
        {
            items.Add(new BackgroundActivityItemViewModel(
                GetString(
                    "CaptureMemory_Settings_Recovery.Title",
                    "Cleanup needs attention"),
                GetString(
                    "CaptureMemory_Settings_RecoveryRequired",
                    "The safety change is saved, but app-managed cleanup is incomplete. Restart or retry after files are available."),
                isActive: false,
                isAttention: true));
        }
        else if (snapshot.HasMemoryOperationFailure)
        {
            items.Add(new BackgroundActivityItemViewModel(
                GetString(
                    "CaptureMemory_Settings_Failure.Title",
                    "Capture Memory action failed"),
                GetString(
                    "CaptureMemory_Settings_OperationUnavailable",
                    "Capture Memory could not complete this action. Try again."),
                isActive: false,
                isAttention: true));
        }

        if (!BackgroundActivities.SequenceEqual(items))
        {
            BackgroundActivities.Clear();
            foreach (BackgroundActivityItemViewModel item in items)
            {
                BackgroundActivities.Add(item);
            }
        }

        HasBackgroundActivity = items.Count > 0;
        HasActiveBackgroundActivity = items.Any(item => item.IsActive);
        HasBackgroundActivityAttention = items.Any(item => item.IsAttention);
        (BackgroundActivitySummary, IsPrimaryActivityDeterminate, PrimaryActivityProgress) =
            CreatePrimaryStatus(snapshot);
    }

    private (string Summary, bool IsDeterminate, double Progress) CreatePrimaryStatus(
        CaptureAnalysisActivitySnapshot snapshot)
    {
        if (snapshot.ModelPreparations.Count == 1)
        {
            CaptureAnalysisModelPreparationActivity preparation =
                snapshot.ModelPreparations[0];
            if (!preparation.HasReportedProgress)
            {
                return (
                    GetModelPreparationTitle(preparation.Capability),
                    false,
                    0);
            }

            return (
                string.Format(
                    CultureInfo.CurrentCulture,
                    GetString(
                        "BackgroundActivity_ModelPreparationSummary",
                        "Preparing AI model · {0}%"),
                    Math.Round(preparation.FractionComplete * 100)),
                true,
                preparation.FractionComplete);
        }

        if (snapshot.ModelPreparations.Count > 1)
        {
            return (
                string.Format(
                    CultureInfo.CurrentCulture,
                    GetString(
                        "BackgroundActivity_MultipleModelsSummary",
                        "Preparing {0} AI models"),
                    snapshot.ModelPreparations.Count),
                false,
                0);
        }

        if (snapshot.IsBackfillInProgress)
        {
            if (snapshot.BackfillFractionComplete == 0)
            {
                return (
                    GetString(
                        "BackgroundActivity_BackfillTitle",
                        "Preparing existing captures"),
                    false,
                    0);
            }

            return (
                string.Format(
                    CultureInfo.CurrentCulture,
                    GetString(
                        "BackgroundActivity_BackfillSummary",
                        "Preparing existing captures · {0}%"),
                    Math.Round(snapshot.BackfillFractionComplete * 100)),
                true,
                snapshot.BackfillFractionComplete);
        }

        if (snapshot.RunningCaptureCount > 0)
        {
            return (
                string.Format(
                    CultureInfo.CurrentCulture,
                    GetString(
                        "BackgroundActivity_AnalysisSummary",
                        "Analyzing captures · {0} active, {1} waiting"),
                    snapshot.RunningCaptureCount,
                    snapshot.QueuedCaptureCount),
                false,
                0);
        }

        if (snapshot.QueuedCaptureCount > 0)
        {
            return (
                FormatCaptureCount(
                    "BackgroundActivity_QueuedSummary",
                    "Analysis queued · {0} capture(s)",
                    snapshot.QueuedCaptureCount),
                false,
                0);
        }

        if (snapshot.WaitingCaptureCount > 0)
        {
            return (
                FormatCaptureCount(
                    "BackgroundActivity_WaitingSummary",
                    "Waiting for AI · {0} capture(s)",
                    snapshot.WaitingCaptureCount),
                false,
                0);
        }

        if (snapshot.RetryCaptureCount > 0)
        {
            return (
                FormatCaptureCount(
                    "BackgroundActivity_RetrySummary",
                    "Analysis will retry · {0} capture(s)",
                    snapshot.RetryCaptureCount),
                false,
                0);
        }

        if (snapshot.FailedCaptureCount > 0)
        {
            return (
                FormatCaptureCount(
                    "BackgroundActivity_FailedSummary",
                    "Analysis needs attention · {0} capture(s)",
                    snapshot.FailedCaptureCount),
                false,
                0);
        }

        if (snapshot.NeedsMemoryRecovery)
        {
            return (
                GetString(
                    "CaptureMemory_Settings_Recovery.Title",
                    "Cleanup needs attention"),
                false,
                0);
        }

        if (snapshot.HasMemoryOperationFailure)
        {
            return (
                GetString(
                    "CaptureMemory_Settings_Failure.Title",
                    "Capture Memory action failed"),
                false,
                0);
        }

        return (string.Empty, false, 0);
    }

    private string GetModelPreparationTitle(CapabilityDefinition capability)
    {
        if (capability.Id == AnalysisCapabilities.SpeechTranscriptV1.Id)
        {
            return GetString(
                "BackgroundActivity_SpeechModelTitle",
                "Preparing speech recognition");
        }

        if (capability.Id == AnalysisCapabilities.ImageDescriptionV1.Id ||
            capability.Id == AnalysisCapabilities.VideoDescriptionTrackV1.Id)
        {
            return GetString(
                "BackgroundActivity_VisualModelTitle",
                "Preparing visual understanding");
        }

        return GetString(
            "BackgroundActivity_GenericModelTitle",
            "Preparing an AI model");
    }

    private string FormatCaptureCount(string key, string fallback, int count)
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            GetString(key, fallback),
            count);
    }

    private string GetBackgroundWorkDetail()
    {
        return GetString(
            "BackgroundActivity_Subtitle.Text",
            "Capture Tool keeps working while you use the app.");
    }

    private string GetString(string key, string fallback)
    {
        string? localized = _localizationService?.GetString(key);
        return string.IsNullOrWhiteSpace(localized) || localized == key
            ? fallback
            : localized;
    }

    private async Task OpenBackgroundActivitySettingsAsync()
    {
        if (_openSettingsPageUseCase != null)
        {
            _ = await _openSettingsPageUseCase.ExecuteAsync(
                new OpenSettingsPageRequest(),
                CancellationToken.None);
        }
    }
}
