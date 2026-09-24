using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Activity;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Domain.Analysis;
using System.Globalization;
using System.Collections.ObjectModel;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Presentation.Features.CaptureMemory;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.Settings;

/// <summary>Observes application-owned work. Leaving Settings never cancels that work.</summary>
public sealed class CaptureMemorySettingsViewModel : ViewModelBase
{
    private readonly ICaptureMemoryFeatureAvailability? _featureAvailability;
    private readonly ICaptureMemoryWorkflow? _workflow;
    private readonly ICaptureAnalysisSettingsConfirmationDialogService? _confirmationService;
    private readonly ILocalizationService? _localizationService;
    private readonly CaptureMemoryStateRefreshLoop _refresh;
    private SynchronizationContext? _uiContext;
    private bool _observing;
    private long _readGeneration;
    private int _pendingCommands;
    private CaptureMemoryOperation? _operation;
    private readonly ICaptureAnalysisActivityQueryService? _activity;
    private readonly IAnalysisCapabilityPreparationQueryService? _capabilityQuery;
    private DateTimeOffset _capabilitiesReadAt;
    private readonly ObservableCollection<CaptureMemoryCapabilityViewModel> _capabilities = [];

    public CaptureMemorySettingsViewModel(
        ICaptureMemoryFeatureAvailability? featureAvailability = null,
        ICaptureMemoryWorkflow? workflow = null,
        ICaptureAnalysisSettingsConfirmationDialogService? confirmationService = null,
        ILocalizationService? localizationService = null,
        ICaptureAnalysisActivityQueryService? activity = null,
        IAnalysisCapabilityPreparationQueryService? capabilityQuery = null)
    {
        _featureAvailability = featureAvailability;
        _workflow = workflow;
        _confirmationService = confirmationService;
        _localizationService = localizationService;
        _activity = activity;
        _capabilityQuery = capabilityQuery;
        Capabilities = new(_capabilities);
        _refresh = new(RefreshAsync);
        EnableCaptureMemoryCommand = new AsyncRelayCommand(
            EnableAsync,
            () => _workflow != null && ShowEnableAction && !IsBusy,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        StopAnalyzingNewCapturesCommand = Command(CaptureMemoryOperationKind.StopNewCaptures,
            () => CanMutate && IsAnalyzingNewCaptures, CaptureAnalysisSettingsAction.StopAnalyzingNewCaptures);
        ResumeAnalyzingNewCapturesCommand = Command(CaptureMemoryOperationKind.ResumeNewCaptures,
            () => CanMutate && !IsAnalyzingNewCaptures);
        TurnOffAndEraseCommand = Command(CaptureMemoryOperationKind.TurnOffAndErase,
            () => IsVisible && IsPolicyAvailable && (IsAuthorized || NeedsRecovery) &&
                _operation is not { IsRunning: true, Request.Kind: CaptureMemoryOperationKind.TurnOffAndErase },
            CaptureAnalysisSettingsAction.TurnOffAndErase);
        ClearMemoryCommand = Command(CaptureMemoryOperationKind.ClearMemory, () => CanMutate,
            CaptureAnalysisSettingsAction.ClearMemory);
        RebuildSearchIndexCommand = Command(CaptureMemoryOperationKind.RebuildSearch,
            () => IsVisible && !IsBusy, CaptureAnalysisSettingsAction.RebuildSearchIndex);
        ReanalyzeCapturesCommand = Command(CaptureMemoryOperationKind.Reanalyze,
            () => CanMutate && ReanalyzableCaptureCount > 0, CaptureAnalysisSettingsAction.ReanalyzeCaptures);
        IncludeExistingCapturesCommand = Command(CaptureMemoryOperationKind.IncludeExistingCaptures,
            () => CanMutate, CaptureAnalysisSettingsAction.AuthorizeExistingCaptureBackfill);
        CancelOperationCommand = new RelayCommand(() =>
        {
            if (_operation is { IsRunning: true } current) { _workflow?.Cancel(current.Id); }
        }, () => _operation is { IsRunning: true, Request.Kind: not CaptureMemoryOperationKind.TurnOffAndErase });
        RefreshCommand = new AsyncRelayCommand(() => { _capabilitiesReadAt = default; return RefreshAsync(CancellationToken.None); },
            () => IsVisible, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        PolicyStatusText = GetString("CaptureMemory_Settings_StatusUnavailable", "Capture Memory status is unavailable.");
    }

    public IAsyncRelayCommand EnableCaptureMemoryCommand { get; }
    public IAsyncRelayCommand StopAnalyzingNewCapturesCommand { get; }
    public IAsyncRelayCommand ResumeAnalyzingNewCapturesCommand { get; }
    public IAsyncRelayCommand TurnOffAndEraseCommand { get; }
    public IAsyncRelayCommand ClearMemoryCommand { get; }
    public IAsyncRelayCommand RebuildSearchIndexCommand { get; }
    public IAsyncRelayCommand ReanalyzeCapturesCommand { get; }
    public IAsyncRelayCommand IncludeExistingCapturesCommand { get; }
    public IRelayCommand CancelOperationCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public bool IsVisible => _featureAvailability?.IsCaptureMemorySearchEnabled == true;
    public bool IsAuthorized { get; private set => Set(ref field, value); }
    public bool IsPolicyAvailable { get; private set => Set(ref field, value); }
    public bool IsAnalyzingNewCaptures { get; private set => Set(ref field, value); }
    public int ActiveCaptureCount { get; private set => Set(ref field, value); }
    public int ReanalyzableCaptureCount { get; private set => Set(ref field, value); }
    public int ExcludedCaptureCount { get; private set => Set(ref field, value); }
    public bool IsBusy => _pendingCommands > 0 || _operation?.IsRunning == true;
    public bool ShowProgress => IsBusy;
    public bool CanChangeAnalysisState => IsVisible && IsPolicyAvailable && !IsBusy && _workflow != null;
    public bool ShowAuthorizedControls => IsAuthorized;
    public bool ShowEnableAction => IsVisible && IsPolicyAvailable && !IsAuthorized;
    public bool ShowStopAction => IsAuthorized && IsAnalyzingNewCaptures;
    public bool ShowResumeAction => IsAuthorized && !IsAnalyzingNewCaptures;
    public bool IsPreparingModels => _operation is { IsRunning: true, Phase: CaptureMemoryOperationPhase.PreparingModels };
    public bool IsSchedulingCaptures => _operation is { IsRunning: true, Phase: CaptureMemoryOperationPhase.SchedulingCaptures };
    public double OperationProgress { get; private set => Set(ref field, value); }
    public string PolicyStatusText { get; private set => Set(ref field, value); }
    public string OperationStatusText { get; private set => Set(ref field, value); } = string.Empty;
    public bool HasOperationStatus => !string.IsNullOrWhiteSpace(OperationStatusText);
    public bool HasOperationFailure { get; private set => Set(ref field, value); }
    public bool NeedsRecovery { get; private set => Set(ref field, value); }
    public string ActivityStatusText { get; private set => Set(ref field, value); } = string.Empty;
    public ReadOnlyObservableCollection<CaptureMemoryCapabilityViewModel> Capabilities { get; }
    public string ScopeText => string.Format(CultureInfo.CurrentCulture,
        GetString("CaptureMemory_Settings_Scope", "Captures included: {0} · Excluded: {1}"), ActiveCaptureCount, ExcludedCaptureCount);
    private bool CanMutate => IsVisible && IsPolicyAvailable && IsAuthorized && !IsBusy && _workflow != null;

    public string ReanalyzeAvailabilityText => !IsPolicyAvailable || _workflow == null
        ? GetString("CaptureMemory_Settings_ReanalyzeStatusUnavailable", "Capture Memory status is unavailable. It will refresh automatically.")
        : IsBusy ? GetString("CaptureMemory_Settings_ReanalyzeBusy", "Wait for the current Memory action to finish, or cancel it.")
        : !IsAuthorized ? GetString("CaptureMemory_Settings_ReanalyzeOff", "Turn on Capture Memory to reanalyze captures.")
        : ReanalyzableCaptureCount > 0 ? string.Empty
        : ExcludedCaptureCount > 0
            ? GetString("CaptureMemory_Settings_ReanalyzeExcluded", "No eligible captures. Excluded and forgotten captures stay excluded from reanalysis.")
            : GetString("CaptureMemory_Settings_ReanalyzeEmpty", "No captures are enrolled yet. Take a new capture to begin analysis.");
    public bool ShowReanalyzeAvailability => !string.IsNullOrEmpty(ReanalyzeAvailabilityText);

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (!IsVisible) { return; }
        _uiContext = SynchronizationContext.Current;
        if (!_observing && _workflow != null)
        {
            _observing = true;
            _workflow.Changed += OnWorkflowChanged;
            if (_activity != null) { _activity.ActivityChanged += OnActivityChanged; }
        }
        await RefreshAsync(cancellationToken);
        _refresh.Start();
    }

    public override void Dispose()
    {
        _observing = false;
        if (_workflow != null) { _workflow.Changed -= OnWorkflowChanged; }
        if (_activity != null) { _activity.ActivityChanged -= OnActivityChanged; }
        _refresh.Dispose();
        _readGeneration++;
        base.Dispose();
    }

    public async Task SetAnalyzingNewCapturesAsync(bool shouldAnalyze)
    {
        if (!CanChangeAnalysisState || (IsAuthorized && shouldAnalyze == IsAnalyzingNewCaptures)) { return; }
        if (shouldAnalyze && !IsAuthorized)
        {
            await EnableCaptureMemoryCommand.ExecuteAsync(null);
        }
        else
        {
            await (shouldAnalyze ? ResumeAnalyzingNewCapturesCommand : StopAnalyzingNewCapturesCommand).ExecuteAsync(null);
        }
        RaisePropertyChanged(nameof(IsAnalyzingNewCaptures));
    }

    private async Task EnableAsync()
    {
        if (_workflow == null || _confirmationService == null) { return; }
        try
        {
            CaptureMemoryEnableScope scope = await _confirmationService.ChooseEnableScopeAsync(CancellationToken.None);
            if (scope is not (CaptureMemoryEnableScope.NewCapturesOnly or CaptureMemoryEnableScope.IncludeExistingCaptures))
            {
                return;
            }

            await ExecuteOperationAsync(
                CaptureMemoryOperationKind.Enable,
                scope == CaptureMemoryEnableScope.IncludeExistingCaptures);
        }
        catch
        {
            HasOperationFailure = true;
            OperationStatusText = GetString("CaptureMemory_Settings_OperationUnavailable", "Capture Memory could not complete this action. Try again.");
            NotifyState();
        }
    }

    private IAsyncRelayCommand Command(CaptureMemoryOperationKind kind, Func<bool> canExecute,
        CaptureAnalysisSettingsAction? confirmation = null) => new AsyncRelayCommand(async () =>
    {
        if (_workflow == null) { return; }
        try
        {
            if (confirmation.HasValue && (_confirmationService == null ||
                await _confirmationService.ConfirmAsync(new(confirmation.Value), CancellationToken.None) !=
                    CaptureAnalysisConfirmationDecision.Confirmed)) { return; }
            await ExecuteOperationAsync(kind, includeExisting: false);
        }
        catch
        {
            HasOperationFailure = true;
            OperationStatusText = GetString("CaptureMemory_Settings_OperationUnavailable", "Capture Memory could not complete this action. Try again.");
        }
        finally { NotifyState(); }
    }, () => _workflow != null && canExecute(), AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);

    private async Task ExecuteOperationAsync(CaptureMemoryOperationKind kind, bool includeExisting)
    {
        if (_workflow == null) { return; }
        _pendingCommands++;
        NotifyState();
        try
        {
            CaptureMemoryOperation result = await _workflow.ExecuteAsync(new(kind, includeExisting), CancellationToken.None);
            if (_observing)
            {
                await RefreshAsync(CancellationToken.None);
                if (result.Status == CaptureMemoryOperationStatus.Conflict && result.Id != _operation?.Id)
                {
                    OperationStatusText = Describe(result);
                    HasOperationFailure = true;
                }
            }
        }
        finally
        {
            _pendingCommands--;
            NotifyState();
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        long generation = ++_readGeneration;
        try
        {
            if (_workflow == null) { return; }
            CaptureMemoryWorkflowSnapshot snapshot = await _workflow.GetCurrentAsync(cancellationToken);
            string activityText = await ReadActivityAsync(cancellationToken);
            IReadOnlyList<CaptureMemoryCapabilityViewModel> capabilities = await ReadCapabilitiesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != _readGeneration) { return; }
            _operation = snapshot.Operation;
            ActivityStatusText = activityText;
            if (!Capabilities.SequenceEqual(capabilities))
            {
                _capabilities.Clear();
                foreach (var capability in capabilities) { _capabilities.Add(capability); }
            }
            CaptureAnalysisPolicySnapshot policy = snapshot.Policy;
            IsPolicyAvailable = policy.Status != CaptureAnalysisPolicySnapshotStatus.Unavailable;
            IsAuthorized = policy.IsProcessingAuthorized;
            IsAnalyzingNewCaptures = IsAuthorized && policy.Policy?.IsFutureCaptureAdmissionEnabled == true;
            ActiveCaptureCount = policy.ActiveCaptureCount;
            ReanalyzableCaptureCount = policy.ReanalyzableCaptureCount;
            ExcludedCaptureCount = policy.ExcludedCaptureCount;
            OperationProgress = snapshot.FractionComplete;
            NeedsRecovery = _operation?.Status == CaptureMemoryOperationStatus.RecoveryRequired;
            HasOperationFailure = _operation?.Status is CaptureMemoryOperationStatus.Failed or CaptureMemoryOperationStatus.Conflict or CaptureMemoryOperationStatus.Rejected;
            OperationStatusText = _operation == null ? string.Empty : Describe(_operation);
            PolicyStatusText = policy.Status switch
            {
                CaptureAnalysisPolicySnapshotStatus.Unavailable => GetString("CaptureMemory_Settings_StatusUnavailable", "Capture Memory status is unavailable."),
                CaptureAnalysisPolicySnapshotStatus.ConsentMismatch or CaptureAnalysisPolicySnapshotStatus.ConsentReviewRequired =>
                    GetString("CaptureMemory_Settings_StatusReview", "Capture Memory is paused because its consent state needs review."),
                _ when !IsAuthorized => GetString("CaptureMemory_Settings_StatusOff", "Capture Memory is off. No capture analysis is authorized."),
                _ when IsAnalyzingNewCaptures => GetString("CaptureMemory_Settings_StatusOn", "Capture Memory is on for new captures. Analysis stays on this device and originals are not modified."),
                _ => GetString("CaptureMemory_Settings_StatusStopped", "Analysis of new captures is stopped. Existing app-managed metadata remains searchable."),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            if (generation != _readGeneration) { return; }
            IsPolicyAvailable = IsAuthorized = IsAnalyzingNewCaptures = false;
            PolicyStatusText = GetString("CaptureMemory_Settings_StatusUnavailable", "Capture Memory status is unavailable.");
            HasOperationFailure = true;
        }
        finally { if (generation == _readGeneration) { NotifyState(); } }
    }

    private void OnWorkflowChanged(object? sender, EventArgs args)
    {
        _capabilitiesReadAt = default;
        OnActivityChanged(sender, args);
    }

    private void OnActivityChanged(object? sender, EventArgs args)
    {
        void Refresh() { if (_observing) { _ = RefreshAsync(CancellationToken.None); } }
        if (_uiContext != null && SynchronizationContext.Current != _uiContext) { _uiContext.Post(_ => Refresh(), null); }
        else { Refresh(); }
    }

    private async Task<string> ReadActivityAsync(CancellationToken token)
    {
        if (_activity == null) { return string.Empty; }
        try
        {
            var activity = await _activity.GetCurrentAsync(token);
            var parts = new List<string>();
            if (activity.ModelPreparations.Count > 0) { parts.Add(GetString("Analysis_Settings_Preparing", "Preparing models")); }
            foreach (var (count, key, fallback) in new[] {
                (activity.RunningCaptureCount, "Analysis_Settings_RunningCount", "{0} analyzing"),
                (activity.QueuedCaptureCount, "Analysis_Settings_QueuedCount", "{0} queued"),
                (activity.WaitingCaptureCount, "Analysis_Settings_WaitingCount", "{0} waiting for models"),
                (activity.RetryCaptureCount, "Analysis_Settings_RetryCount", "{0} retrying"),
                (activity.FailedCaptureCount, "Analysis_Settings_FailedCount", "{0} failed") })
            {
                if (count > 0) { parts.Add(string.Format(CultureInfo.CurrentCulture, GetString(key, fallback), count)); }
            }
            return parts.Count > 0 ? string.Join(" · ", parts) : GetString("Analysis_Settings_Idle", "No analysis is queued.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { return GetString("Analysis_Settings_ActivityUnavailable", "Analysis progress is unavailable. It will refresh automatically."); }
    }

    private async Task<IReadOnlyList<CaptureMemoryCapabilityViewModel>> ReadCapabilitiesAsync(CancellationToken token)
    {
        if (_capabilityQuery == null || DateTimeOffset.UtcNow - _capabilitiesReadAt < TimeSpan.FromSeconds(30)) { return Capabilities; }
        var capabilities = new List<CaptureMemoryCapabilityViewModel>();
        foreach (var (capability, mediaKind, key, fallback) in new[] {
            (AnalysisCapabilities.OcrDocumentV1, CaptureMediaKind.Image, "Analysis_Settings_ImageText", "Text in images"),
            (AnalysisCapabilities.ImageDescriptionV1, CaptureMediaKind.Image, "Analysis_Settings_ImageDescription", "Image descriptions"),
            (AnalysisCapabilities.SpeechTranscriptV1, CaptureMediaKind.Audio, "Analysis_Settings_AudioTranscript", "Audio transcripts"),
            (AnalysisCapabilities.SpeechTranscriptV1, CaptureMediaKind.Video, "Analysis_Settings_VideoTranscript", "Video transcripts"),
            (AnalysisCapabilities.VideoOcrTrackV1, CaptureMediaKind.Video, "Analysis_Settings_ScreenText", "On-screen text in videos"),
            (AnalysisCapabilities.VideoDescriptionTrackV1, CaptureMediaKind.Video, "Analysis_Settings_VideoDescription", "Video descriptions") })
        {
            AnalysisCapabilityPreparationStatus status;
            try
            {
                var state = await _capabilityQuery.GetStateAsync(new(capability, mediaKind,
                    CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurpose, CaptureAnalysisPolicyDefaults.CreateLocalOnlyPolicy()), token);
                status = state.Status;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { status = AnalysisCapabilityPreparationStatus.Unknown; }
            string statusText = status switch
            {
                AnalysisCapabilityPreparationStatus.Ready => GetString("Analysis_Settings_Ready", "Ready"),
                AnalysisCapabilityPreparationStatus.PreparationRequired => GetString("Analysis_Settings_ModelNeeded", "Model preparation needed"),
                AnalysisCapabilityPreparationStatus.Preparing => GetString("Analysis_Settings_Preparing", "Preparing models"),
                AnalysisCapabilityPreparationStatus.Unsupported => GetString("Analysis_Settings_Unsupported", "Unavailable on this device"),
                AnalysisCapabilityPreparationStatus.Disabled => GetString("Analysis_Settings_Disabled", "Disabled"),
                AnalysisCapabilityPreparationStatus.Failed => GetString("Analysis_Settings_ModelFailed", "Model preparation failed"),
                AnalysisCapabilityPreparationStatus.Cancelled => GetString("Analysis_Settings_ModelCancelled", "Model preparation cancelled"),
                _ => GetString("Analysis_Settings_StatusUnavailable", "Status unavailable"),
            };
            capabilities.Add(new(GetString(key, fallback), statusText));
        }
        _capabilitiesReadAt = DateTimeOffset.UtcNow;
        return capabilities.AsReadOnly();
    }

    private string Describe(CaptureMemoryOperation operation)
    {
        if (operation.IsRunning)
        {
            return operation.Phase switch
            {
                CaptureMemoryOperationPhase.PreparingModels => GetString("CaptureMemory_Settings_ReanalyzePreparing", "Preparing AI models for analysis…"),
                CaptureMemoryOperationPhase.SchedulingCaptures => GetString("CaptureMemory_Settings_ReanalyzeScheduling", "Queuing authorized captures for analysis…"),
                _ => GetString("CaptureMemory_Settings_Updating", "Updating Capture Memory…"),
            };
        }
        return operation.Status switch
        {
            CaptureMemoryOperationStatus.Cancelled => GetString("CaptureMemory_Settings_Cancelled", "The operation was cancelled. Completed safety changes remain in effect."),
            CaptureMemoryOperationStatus.RecoveryRequired => GetString("CaptureMemory_Settings_RecoveryRequired", "The safety change is saved, but app-managed cleanup is incomplete. Restart or retry after files are available."),
            CaptureMemoryOperationStatus.Conflict => GetString("CaptureMemory_Settings_Conflict", "Capture Memory changed elsewhere. Its current state has been refreshed; try again."),
            CaptureMemoryOperationStatus.Rejected => GetString("CaptureMemory_Settings_Rejected", "This action is no longer available for the current Capture Memory state."),
            CaptureMemoryOperationStatus.Failed => GetString("CaptureMemory_Settings_OperationUnavailable", "Capture Memory could not complete this action. Try again."),
            CaptureMemoryOperationStatus.Partial => GetString(
                "CaptureMemory_Settings_ReanalyzePartial",
                "Capture analysis was queued. Results will appear as analysis finishes."),
            _ => operation.Request.Kind switch
            {
                CaptureMemoryOperationKind.TurnOffAndErase => GetString("CaptureMemory_Settings_EraseSucceeded", "Capture Memory is off and its app-managed metadata was erased. Original captures were not deleted."),
                CaptureMemoryOperationKind.ClearMemory => GetString("CaptureMemory_Settings_ClearSucceeded", "Capture Memory metadata and search data were cleared. Your setting for analyzing new captures was not changed."),
                CaptureMemoryOperationKind.StopNewCaptures => GetString("CaptureMemory_Settings_StopSucceeded", "New captures will not be analyzed. Existing Memory metadata remains searchable."),
                CaptureMemoryOperationKind.ResumeNewCaptures => GetString("CaptureMemory_Settings_ResumeSucceeded", "Capture Memory will analyze new captures on this device. Original captures are not modified."),
                CaptureMemoryOperationKind.RebuildSearch => GetString("CaptureMemory_Settings_RebuildSucceeded", "The app-managed search index was rebuilt without running AI models or reading capture sources."),
                CaptureMemoryOperationKind.Enable => GetString("CaptureMemory_Settings_EnableSucceeded", "Capture Memory is on for new captures. Original captures were not modified."),
                CaptureMemoryOperationKind.IncludeExistingCaptures => GetString("Analysis_Settings_ExistingQueued", "Eligible existing captures were queued. Results will appear as analysis finishes."),
                _ => GetString("CaptureMemory_Settings_ReanalyzeSucceeded", "Capture reanalysis was queued. Original captures were not modified."),
            },
        };
    }

    private void NotifyState()
    {
        foreach (string name in new[] { nameof(IsBusy), nameof(ShowProgress),
            nameof(CanChangeAnalysisState), nameof(ShowAuthorizedControls), nameof(ShowEnableAction), nameof(ShowStopAction),
            nameof(ShowResumeAction), nameof(IsPreparingModels), nameof(IsSchedulingCaptures), nameof(HasOperationStatus),
            nameof(ReanalyzeAvailabilityText), nameof(ShowReanalyzeAvailability), nameof(ScopeText) }) { RaisePropertyChanged(name); }
        foreach (IAsyncRelayCommand command in new[] { EnableCaptureMemoryCommand, StopAnalyzingNewCapturesCommand,
            ResumeAnalyzingNewCapturesCommand, TurnOffAndEraseCommand, ClearMemoryCommand, RebuildSearchIndexCommand,
            ReanalyzeCapturesCommand, IncludeExistingCapturesCommand, RefreshCommand }) { command.NotifyCanExecuteChanged(); }
        CancelOperationCommand.NotifyCanExecuteChanged();
    }

    private string GetString(string key, string fallback)
    {
        string? value = _localizationService?.GetString(key);
        return string.IsNullOrWhiteSpace(value) || value == key ? fallback : value;
    }
}

public sealed record CaptureMemoryCapabilityViewModel(string Name, string StatusText);
