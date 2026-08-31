using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Policy;
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

    public CaptureMemorySettingsViewModel(
        ICaptureMemoryFeatureAvailability? featureAvailability = null,
        ICaptureMemoryWorkflow? workflow = null,
        ICaptureAnalysisSettingsConfirmationDialogService? confirmationService = null,
        ILocalizationService? localizationService = null)
    {
        _featureAvailability = featureAvailability;
        _workflow = workflow;
        _confirmationService = confirmationService;
        _localizationService = localizationService;
        _refresh = new(RefreshAsync);
        EnableCaptureMemoryCommand = Command(CaptureMemoryOperationKind.Enable, () => ShowEnableAction && !IsBusy);
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
        CancelOperationCommand = new RelayCommand(() =>
        {
            if (_operation is { IsRunning: true } current) { _workflow?.Cancel(current.Id); }
        }, () => _operation is { IsRunning: true, Request.Kind: not CaptureMemoryOperationKind.TurnOffAndErase });
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(CancellationToken.None),
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
    public IRelayCommand CancelOperationCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public bool IncludeExistingCaptures { get; set => Set(ref field, value); }
    public bool IsVisible => _featureAvailability?.IsCaptureMemorySearchEnabled == true;
    public bool IsAuthorized { get; private set => Set(ref field, value); }
    public bool IsPolicyAvailable { get; private set => Set(ref field, value); }
    public bool IsAnalyzingNewCaptures { get; private set => Set(ref field, value); }
    public int ActiveCaptureCount { get; private set => Set(ref field, value); }
    public int ReanalyzableCaptureCount { get; private set => Set(ref field, value); }
    public int ExcludedCaptureCount { get; private set => Set(ref field, value); }
    public bool IsBusy => _pendingCommands > 0 || _operation?.IsRunning == true;
    public bool ShowProgress => IsBusy;
    public bool CanChangeSetupOptions => !IsBusy;
    public bool CanChangeAnalysisState => CanMutate;
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
    public bool IsModelUnavailable { get; private set => Set(ref field, value); }
    private bool CanMutate => IsVisible && IsPolicyAvailable && IsAuthorized && !IsBusy && _workflow != null;

    public string ReanalyzeAvailabilityText => !IsPolicyAvailable || _workflow == null
        ? GetString("CaptureMemory_Settings_ReanalyzeStatusUnavailable", "Capture Memory status is unavailable. It will refresh automatically.")
        : IsBusy ? GetString("CaptureMemory_Settings_ReanalyzeBusy", "Wait for the current Memory action to finish, or cancel it.")
        : !IsAuthorized ? GetString("CaptureMemory_Settings_ReanalyzeOff", "Turn on Capture Memory to reanalyze captures.")
        : ReanalyzableCaptureCount > 0 ? string.Empty
        : ExcludedCaptureCount > 0
            ? GetString("CaptureMemory_Settings_ReanalyzeExcluded", "No eligible captures. Excluded and forgotten captures stay excluded from reanalysis.")
            : GetString("CaptureMemory_Settings_ReanalyzeEmpty", "No captures are enrolled yet. Take a new capture, or enable Memory with Include existing captures.");
    public bool ShowReanalyzeAvailability => !string.IsNullOrEmpty(ReanalyzeAvailabilityText);

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (!IsVisible) { return; }
        _uiContext = SynchronizationContext.Current;
        if (!_observing && _workflow != null)
        {
            _observing = true;
            _workflow.Changed += OnWorkflowChanged;
        }
        await RefreshAsync(cancellationToken);
        _refresh.Start();
    }

    public override void Dispose()
    {
        _observing = false;
        if (_workflow != null) { _workflow.Changed -= OnWorkflowChanged; }
        _refresh.Dispose();
        _readGeneration++;
        base.Dispose();
    }

    public async Task SetAnalyzingNewCapturesAsync(bool shouldAnalyze)
    {
        if (!CanChangeAnalysisState || shouldAnalyze == IsAnalyzingNewCaptures) { return; }
        await (shouldAnalyze ? ResumeAnalyzingNewCapturesCommand : StopAnalyzingNewCapturesCommand).ExecuteAsync(null);
        RaisePropertyChanged(nameof(IsAnalyzingNewCaptures));
    }

    private IAsyncRelayCommand Command(CaptureMemoryOperationKind kind, Func<bool> canExecute,
        CaptureAnalysisSettingsAction? confirmation = null) => new AsyncRelayCommand(async () =>
    {
        if (_workflow == null) { return; }
        // Snapshot the checkbox before any asynchronous confirmation.
        bool includeExisting = kind == CaptureMemoryOperationKind.Enable && IncludeExistingCaptures;
        try
        {
            if (confirmation.HasValue && (_confirmationService == null ||
                await _confirmationService.ConfirmAsync(new(confirmation.Value), CancellationToken.None) !=
                    CaptureAnalysisConfirmationDecision.Confirmed)) { return; }
            _pendingCommands++;
            NotifyState();
            try
            {
                CaptureMemoryOperation result = await _workflow.ExecuteAsync(new(kind, includeExisting), CancellationToken.None);
                if (includeExisting && result.IsSchedulingComplete) { IncludeExistingCaptures = false; }
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
            finally { _pendingCommands--; }
        }
        catch
        {
            HasOperationFailure = true;
            OperationStatusText = GetString("CaptureMemory_Settings_OperationUnavailable", "Capture Memory could not complete this action. Try again.");
        }
        finally { NotifyState(); }
    }, () => _workflow != null && canExecute(), AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        long generation = ++_readGeneration;
        try
        {
            if (_workflow == null) { return; }
            CaptureMemoryWorkflowSnapshot snapshot = await _workflow.GetCurrentAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != _readGeneration) { return; }
            _operation = snapshot.Operation;
            CaptureAnalysisPolicySnapshot policy = snapshot.Policy;
            IsPolicyAvailable = policy.Status != CaptureAnalysisPolicySnapshotStatus.Unavailable;
            IsAuthorized = policy.IsProcessingAuthorized;
            IsAnalyzingNewCaptures = IsAuthorized && policy.Policy?.IsFutureCaptureAdmissionEnabled == true;
            ActiveCaptureCount = policy.ActiveCaptureCount;
            ReanalyzableCaptureCount = policy.ReanalyzableCaptureCount;
            ExcludedCaptureCount = policy.ExcludedCaptureCount;
            OperationProgress = snapshot.FractionComplete;
            NeedsRecovery = _operation?.Status == CaptureMemoryOperationStatus.RecoveryRequired;
            IsModelUnavailable = _operation?.HasLimitedModelCoverage == true || _operation?.Status == CaptureMemoryOperationStatus.Partial;
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
        void Refresh() { if (_observing) { _ = RefreshAsync(CancellationToken.None); } }
        if (_uiContext != null && SynchronizationContext.Current != _uiContext) { _uiContext.Post(_ => Refresh(), null); }
        else { Refresh(); }
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
            CaptureMemoryOperationStatus.Partial => GetString("CaptureMemory_Settings_ReanalyzePartial", "Available analysis was queued. Some models or capture sources were unavailable; results will appear as supported analysis finishes."),
            _ => operation.Request.Kind switch
            {
                CaptureMemoryOperationKind.TurnOffAndErase => GetString("CaptureMemory_Settings_EraseSucceeded", "Capture Memory is off and its app-managed metadata was erased. Original captures were not deleted."),
                CaptureMemoryOperationKind.ClearMemory => GetString("CaptureMemory_Settings_ClearSucceeded", "Capture Memory metadata and search data were cleared. Analysis of new captures remains on."),
                CaptureMemoryOperationKind.StopNewCaptures => GetString("CaptureMemory_Settings_StopSucceeded", "New captures will not be analyzed. Existing Memory metadata remains searchable."),
                CaptureMemoryOperationKind.ResumeNewCaptures => GetString("CaptureMemory_Settings_ResumeSucceeded", "Capture Memory will analyze new captures on this device. Original captures are not modified."),
                CaptureMemoryOperationKind.RebuildSearch => GetString("CaptureMemory_Settings_RebuildSucceeded", "The app-managed search index was rebuilt without running AI models or reading capture sources."),
                CaptureMemoryOperationKind.Enable => GetString("CaptureMemory_Settings_EnableSucceeded", "Capture Memory is on for new captures. Original captures were not modified."),
                _ => GetString("CaptureMemory_Settings_ReanalyzeSucceeded", "Capture reanalysis was queued. Original captures were not modified."),
            },
        };
    }

    private void NotifyState()
    {
        foreach (string name in new[] { nameof(IsBusy), nameof(ShowProgress), nameof(CanChangeSetupOptions),
            nameof(CanChangeAnalysisState), nameof(ShowAuthorizedControls), nameof(ShowEnableAction), nameof(ShowStopAction),
            nameof(ShowResumeAction), nameof(IsPreparingModels), nameof(IsSchedulingCaptures), nameof(HasOperationStatus),
            nameof(ReanalyzeAvailabilityText), nameof(ShowReanalyzeAvailability) }) { RaisePropertyChanged(name); }
        foreach (IAsyncRelayCommand command in new[] { EnableCaptureMemoryCommand, StopAnalyzingNewCapturesCommand,
            ResumeAnalyzingNewCapturesCommand, TurnOffAndEraseCommand, ClearMemoryCommand, RebuildSearchIndexCommand,
            ReanalyzeCapturesCommand, RefreshCommand }) { command.NotifyCanExecuteChanged(); }
        CancelOperationCommand.NotifyCanExecuteChanged();
    }

    private string GetString(string key, string fallback)
    {
        string? value = _localizationService?.GetString(key);
        return string.IsNullOrWhiteSpace(value) || value == key ? fallback : value;
    }
}
