using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Domain.Analysis;
using CaptureTool.Presentation.ViewModels;
using CaptureTool.Presentation.Features.CaptureMemory;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.Settings;

public sealed class CaptureMemorySettingsViewModel : ViewModelBase
{
    private readonly ICaptureMemoryFeatureAvailability? _featureAvailability;
    private readonly ICaptureAnalysisPolicyService? _policyService;
    private readonly ICaptureAnalysisPolicyCommandService? _policyCommandService;
    private readonly ICaptureAnalysisMaintenanceService? _maintenanceService;
    private readonly ICaptureAnalysisSettingsConfirmationDialogService? _confirmationService;
    private readonly ILocalizationService? _localizationService;
    private readonly IUserInitiatedAnalysisCapabilityPreparationService? _preparationService;
    private readonly ICaptureAnalysisBackfillService? _backfillService;
    private CancellationTokenSource? _operationCancellation;
    private readonly CaptureMemoryStateRefreshLoop _stateRefresh;
    private long _policyReadGeneration;

    public CaptureMemorySettingsViewModel(
        ICaptureMemoryFeatureAvailability? featureAvailability = null,
        ICaptureAnalysisPolicyService? policyService = null,
        ICaptureAnalysisPolicyCommandService? policyCommandService = null,
        ICaptureAnalysisMaintenanceService? maintenanceService = null,
        ICaptureAnalysisSettingsConfirmationDialogService? confirmationService = null,
        ILocalizationService? localizationService = null,
        IUserInitiatedAnalysisCapabilityPreparationService? preparationService = null,
        ICaptureAnalysisBackfillService? backfillService = null)
    {
        _featureAvailability = featureAvailability;
        _policyService = policyService;
        _policyCommandService = policyCommandService;
        _maintenanceService = maintenanceService;
        _confirmationService = confirmationService;
        _localizationService = localizationService;
        _preparationService = preparationService;
        _backfillService = backfillService;
        _stateRefresh = new CaptureMemoryStateRefreshLoop(RefreshAsync);

        EnableCaptureMemoryCommand = new AsyncRelayCommand(
            EnableCaptureMemoryAsync,
            () => CanEnableCaptureMemory,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);

        StopAnalyzingNewCapturesCommand = new AsyncRelayCommand(
            StopAnalyzingNewCapturesAsync,
            () => CanMutatePolicy && IsAnalyzingNewCaptures,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ResumeAnalyzingNewCapturesCommand = new AsyncRelayCommand(
            ResumeAnalyzingNewCapturesAsync,
            () => CanMutatePolicy && !IsAnalyzingNewCaptures,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        TurnOffAndEraseCommand = new AsyncRelayCommand(
            TurnOffAndEraseAsync,
            () => CanMutatePolicy,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ClearMemoryCommand = new AsyncRelayCommand(
            ClearMemoryAsync,
            () => CanRunAuthorizedMaintenance,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        RebuildSearchIndexCommand = new AsyncRelayCommand(
            RebuildSearchIndexAsync,
            () => IsVisible && !IsBusy && _maintenanceService != null,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ReanalyzeCapturesCommand = new AsyncRelayCommand(
            ReanalyzeCapturesAsync,
            () => CanRunAuthorizedMaintenance && ReanalyzableCaptureCount > 0,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        CancelOperationCommand = new RelayCommand(
            CancelOperation,
            () => IsBusy && _operationCancellation != null);
        RefreshCommand = new AsyncRelayCommand(
            () => RefreshAsync(CancellationToken.None),
            () => IsVisible && !IsBusy,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);

        PolicyStatusText = GetString(
            "CaptureMemory_Settings_StatusUnavailable",
            "Capture Memory status is unavailable.");
        OperationStatusText = string.Empty;
    }

    public IAsyncRelayCommand StopAnalyzingNewCapturesCommand { get; }

    public IAsyncRelayCommand EnableCaptureMemoryCommand { get; }

    public bool IncludeExistingCaptures { get; set => Set(ref field, value); }

    public bool CanChangeSetupOptions => !IsBusy;

    public IAsyncRelayCommand ResumeAnalyzingNewCapturesCommand { get; }

    public IAsyncRelayCommand TurnOffAndEraseCommand { get; }

    public IAsyncRelayCommand ClearMemoryCommand { get; }

    public IAsyncRelayCommand RebuildSearchIndexCommand { get; }

    public IAsyncRelayCommand ReanalyzeCapturesCommand { get; }

    public IRelayCommand CancelOperationCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public bool IsVisible => _featureAvailability?.IsCaptureMemorySearchEnabled == true;

    public bool IsAuthorized
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(ShowAuthorizedControls));
                RaisePropertyChanged(nameof(ShowEnableAction));
                RaisePropertyChanged(nameof(CanChangeAnalysisState));
                RaiseCommandStates();
            }
        }
    }

    public bool ShowAuthorizedControls => IsAuthorized;

    public bool ShowEnableAction => IsVisible && IsPolicyAvailable && !IsAuthorized;

    public bool IsPolicyAvailable
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(ShowEnableAction));
                RaiseCommandStates();
            }
        }
    }

    public bool CanChangeAnalysisState => CanMutatePolicy;

    public bool IsAnalyzingNewCaptures
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(ShowStopAction));
                RaisePropertyChanged(nameof(ShowResumeAction));
                RaisePropertyChanged(nameof(CanChangeAnalysisState));
                RaiseCommandStates();
            }
        }
    }

    public bool ShowStopAction => IsAuthorized && IsAnalyzingNewCaptures;

    public bool ShowResumeAction => IsAuthorized && !IsAnalyzingNewCaptures;

    public int ActiveCaptureCount
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public int ReanalyzableCaptureCount
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool IsBusy
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(ShowProgress));
                RaisePropertyChanged(nameof(CanChangeSetupOptions));
                RaisePropertyChanged(nameof(ShowEnableAction));
                RaisePropertyChanged(nameof(CanChangeAnalysisState));
                RaiseCommandStates();
            }
        }
    }

    public bool ShowProgress => IsBusy;

    public bool IsPreparingModels { get; private set => Set(ref field, value); }

    public bool IsSchedulingCaptures { get; private set => Set(ref field, value); }

    public double OperationProgress { get; private set => Set(ref field, value); }

    public string PolicyStatusText { get; private set => Set(ref field, value); }

    public string OperationStatusText
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(HasOperationStatus));
            }
        }
    }

    public bool HasOperationStatus => !string.IsNullOrWhiteSpace(OperationStatusText);

    public bool HasOperationFailure { get; private set => Set(ref field, value); }

    public bool NeedsRecovery { get; private set => Set(ref field, value); }

    public bool IsModelUnavailable { get; private set => Set(ref field, value); }

    public int ExcludedCaptureCount { get; private set => Set(ref field, value); }

    public string ReanalyzeAvailabilityText => !IsPolicyAvailable || _maintenanceService == null
        ? GetString("CaptureMemory_Settings_ReanalyzeStatusUnavailable", "Capture Memory status is unavailable. It will refresh automatically.")
        : IsBusy
            ? GetString("CaptureMemory_Settings_ReanalyzeBusy", "Wait for the current Memory action to finish, or cancel it.")
            : !IsAuthorized
                ? GetString("CaptureMemory_Settings_ReanalyzeOff", "Turn on Capture Memory to reanalyze captures.")
                : ReanalyzableCaptureCount > 0
                    ? string.Empty
                    : ExcludedCaptureCount > 0
                        ? GetString("CaptureMemory_Settings_ReanalyzeExcluded", "No eligible captures. Excluded and forgotten captures stay excluded from reanalysis.")
                        : GetString("CaptureMemory_Settings_ReanalyzeEmpty", "No captures are enrolled yet. Take a new capture, or enable Memory with Include existing captures.");

    public bool ShowReanalyzeAvailability => !string.IsNullOrEmpty(ReanalyzeAvailabilityText);

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (!IsVisible)
        {
            return;
        }

        await RefreshAsync(cancellationToken);
        _stateRefresh.Start();
    }

    public override void Dispose()
    {
        _stateRefresh.Dispose();
        _policyReadGeneration++;
        _operationCancellation?.Cancel();
        // The running operation owns/disposes its token in EndOperation.
        base.Dispose();
    }

    private bool CanMutatePolicy =>
        IsVisible && IsAuthorized && !IsBusy && _policyCommandService != null;

    private bool CanEnableCaptureMemory =>
        ShowEnableAction && !IsBusy && _policyService != null &&
        _policyCommandService != null && _preparationService != null;

    private bool CanRunAuthorizedMaintenance =>
        IsVisible && IsAuthorized && !IsBusy && _maintenanceService != null;

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (!IsVisible || _policyService == null)
        {
            ApplyUnavailablePolicy();
            return;
        }

        long generation = _policyReadGeneration;
        try
        {
            CaptureAnalysisPolicySnapshot snapshot = await _policyService.GetCurrentAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation == _policyReadGeneration)
            {
                ApplyPolicy(snapshot);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            if (generation == _policyReadGeneration)
            {
                ApplyUnavailablePolicy();
            }
        }
    }

    private async Task EnableCaptureMemoryAsync()
    {
        // The checkbox authorizes this enable operation, not a persistent library scan.
        bool includeExistingCaptures = IncludeExistingCaptures;
        if (includeExistingCaptures && _backfillService == null)
        {
            ApplyUnavailableOperation();
            return;
        }

        CancellationToken token = BeginOperation(
            "CaptureMemory_Settings_EnablePreparing",
            "Turning on Capture Memory and preparing on-device AI models…");
        try
        {
            CaptureAnalysisPolicySnapshot current = await _policyService!
                .GetCurrentAsync(token);
            var consent = new CaptureAnalysisConsentResponse(
                CaptureAnalysisPolicyDefaults.CreateConsentDisclosure(),
                CaptureAnalysisConsentDecision.GrantedForFutureCaptures);
            CaptureAnalysisPolicyChangeResult result = await _policyCommandService!
                .ApplyConsentDecisionAsync(
                    consent,
                    current.ControlDocumentRevision,
                    token);
            ApplyPolicy(result.Policy);
            if (result.Status != CaptureAnalysisPolicyChangeStatus.Succeeded ||
                result.Policy.Policy?.ProcessingPolicy is not { } processingPolicy)
            {
                ApplyPolicyChangeResult(
                    result,
                    "CaptureMemory_Settings_EnableSucceeded",
                    "Capture Memory is on for new captures.");
                return;
            }

            bool hasLimitedModelCoverage = await PrepareAuthorizedModelsAsync(
                processingPolicy,
                token);
            if (includeExistingCaptures)
            {
                // Preparation may take time; use the current revision and let the policy
                // service reject authorization if consent changed while models prepared.
                CaptureAnalysisPolicySnapshot latest = await _policyService.GetCurrentAsync(token);
                CaptureAnalysisPolicyChangeResult backfillAuthorization = await _policyCommandService
                    .AuthorizeExistingCaptureBackfillAsync(latest.ControlDocumentRevision, token);
                ApplyPolicy(backfillAuthorization.Policy);
                if (backfillAuthorization.Status != CaptureAnalysisPolicyChangeStatus.Succeeded)
                {
                    ApplyPolicyChangeResult(
                        backfillAuthorization,
                        "CaptureMemory_Settings_EnableSucceeded",
                        "Capture Memory is on for new captures.");
                    return;
                }

                IsPreparingModels = false;
                IsSchedulingCaptures = true;
                OperationProgress = 0;
                OperationStatusText = GetString(
                    "CaptureMemory_Settings_EnableExistingScheduling",
                    "Queuing existing captures for analysis…");
                CancellationTokenSource? operation = _operationCancellation;
                // Backfill reports from a worker thread. Marshal updates to the UI context
                // and ignore delayed callbacks after this operation finishes or is replaced.
                var progress = new Progress<CaptureAnalysisBackfillProgress>(value =>
                {
                    if (ReferenceEquals(operation, _operationCancellation) && IsSchedulingCaptures)
                    {
                        OperationProgress = value.Fraction;
                    }
                });
                CaptureAnalysisBackfillRunResult backfill = await _backfillService!
                    .RunAsync(progress, token);
                if (backfill.Status == CaptureAnalysisBackfillRunStatus.Cancelled)
                {
                    ApplyCancelled();
                    return;
                }

                if (backfill.Status is not (CaptureAnalysisBackfillRunStatus.Completed or
                    CaptureAnalysisBackfillRunStatus.AlreadyCompleted))
                {
                    HasOperationFailure = true;
                    OperationStatusText = GetString(
                        "CaptureMemory_Settings_EnableExistingIncomplete",
                        "Capture Memory is on, but not all existing captures could be queued. Return to Home to resume. Original captures were not modified.");
                    return;
                }
            }

            OperationProgress = 1;
            OperationStatusText = hasLimitedModelCoverage
                ? GetString(
                    "CaptureMemory_Settings_EnableLimited",
                    "Capture Memory is on. Some AI models are not ready yet, so supported captures will be analyzed with available on-device capabilities.")
                : GetString(
                    "CaptureMemory_Settings_EnableSucceeded",
                    "Capture Memory is on for new captures. On-device AI models are ready.");
            if (includeExistingCaptures)
            {
                OperationStatusText += " " + GetString(
                    "CaptureMemory_Settings_EnableExistingSucceeded",
                    "Existing captures were queued for analysis. Results will appear as analysis finishes.");
                IncludeExistingCaptures = false;
            }
        }
        catch (OperationCanceledException)
        {
            ApplyCancelled();
        }
        catch
        {
            ApplyUnavailableOperation();
        }
        finally
        {
            EndOperation();
            await RefreshAfterOperationAsync();
        }
    }

    private async Task<bool> PrepareAuthorizedModelsAsync(
        AnalysisProcessingPolicy processingPolicy,
        CancellationToken cancellationToken)
    {
        CaptureAnalysisRecipe[] recipes =
        [
            CaptureAnalysisRecipeDefaults.CreateCaptureMemoryImageRecipe(),
            CaptureAnalysisRecipeDefaults.CreateCaptureMemoryAudioRecipe(),
            CaptureAnalysisRecipeDefaults.CreateCaptureMemoryVideoRecipe(),
        ];
        var preparations = recipes
            .SelectMany(recipe => recipe.Capabilities.Select(capability => new
            {
                recipe.MediaKind,
                Capability = capability.Capability,
            }))
            .ToArray();
        bool hasLimitedCoverage = false;
        IsPreparingModels = true;
        for (int index = 0; index < preparations.Length; index++)
        {
            double capabilityStart = (double)index / preparations.Length;
            double capabilityShare = 1d / preparations.Length;
            var progress = CreateOperationProgress<AnalysisCapabilityPreparationProgress>(value =>
                OperationProgress = capabilityStart +
                    (value.FractionComplete * capabilityShare));
            AnalysisCapabilityPreparationState prepared = await _preparationService!
                .PrepareAsync(
                    new AnalysisCapabilityPreparationRequest(
                        preparations[index].Capability,
                        preparations[index].MediaKind,
                        CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurpose,
                        processingPolicy),
                    progress,
                    cancellationToken);
            OperationProgress = capabilityStart + capabilityShare;
            hasLimitedCoverage |=
                prepared.Status != AnalysisCapabilityPreparationStatus.Ready;
        }

        return hasLimitedCoverage;
    }

    private async Task StopAnalyzingNewCapturesAsync()
    {
        if (!await ConfirmAsync(CaptureAnalysisSettingsAction.StopAnalyzingNewCaptures))
        {
            return;
        }

        await RunPolicyMutationAsync(
            (revision, token) => _policyCommandService!.StopFutureCapturesAsync(revision, token),
            "CaptureMemory_Settings_StopSucceeded",
            "New captures will not be analyzed. Existing Memory metadata remains searchable.");
    }

    private Task ResumeAnalyzingNewCapturesAsync()
    {
        return RunPolicyMutationAsync(
            (revision, token) => _policyCommandService!.ResumeFutureCaptureAdmissionAsync(revision, token),
            "CaptureMemory_Settings_ResumeSucceeded",
            "Capture Memory will analyze new captures on this device. Original captures are not modified.");
    }

    public async Task SetAnalyzingNewCapturesAsync(bool shouldAnalyze)
    {
        if (!CanChangeAnalysisState || shouldAnalyze == IsAnalyzingNewCaptures)
        {
            return;
        }

        if (shouldAnalyze)
        {
            await ResumeAnalyzingNewCapturesAsync();
        }
        else
        {
            await StopAnalyzingNewCapturesAsync();
        }

        // A cancelled confirmation leaves the policy unchanged. Re-notify the one-way UI
        // binding so the switch returns to the durable policy state.
        RaisePropertyChanged(nameof(IsAnalyzingNewCaptures));
    }

    private async Task TurnOffAndEraseAsync()
    {
        if (!await ConfirmAsync(CaptureAnalysisSettingsAction.TurnOffAndErase))
        {
            return;
        }

        await RunPolicyMutationAsync(
            (revision, token) => _policyCommandService!.RevokeAsync(revision, token),
            "CaptureMemory_Settings_EraseSucceeded",
            "Capture Memory is off and its app-managed metadata was erased. Original captures were not deleted.");
    }

    private async Task ClearMemoryAsync()
    {
        if (!await ConfirmAsync(CaptureAnalysisSettingsAction.ClearMemory))
        {
            return;
        }

        await RunMaintenanceAsync(
            token => _maintenanceService!.ClearMemoryAsync(token),
            "CaptureMemory_Settings_ClearSucceeded",
            "Capture Memory metadata and search data were cleared. Analysis of new captures remains on.");
    }

    private async Task RebuildSearchIndexAsync()
    {
        if (!await ConfirmAsync(CaptureAnalysisSettingsAction.RebuildSearchIndex))
        {
            return;
        }

        await RunMaintenanceAsync(
            token => _maintenanceService!.RebuildSearchIndexAsync(token),
            "CaptureMemory_Settings_RebuildSucceeded",
            "The app-managed search index was rebuilt without running AI models or reading capture sources.");
    }

    private async Task ReanalyzeCapturesAsync()
    {
        if (!await ConfirmAsync(CaptureAnalysisSettingsAction.ReanalyzeCaptures))
        {
            return;
        }

        CancellationToken token = BeginOperation(
            "CaptureMemory_Settings_ReanalyzePreparing",
            "Preparing AI models for reanalysis…");
        try
        {
            var progress = CreateOperationProgress<CaptureAnalysisMaintenanceProgress>(ReportProgress);
            CaptureAnalysisMaintenanceResult result = await _maintenanceService!.ReanalyzeCapturesAsync(
                new CaptureAnalysisReanalysisRequest(
                    CaptureAnalysisReanalysisScope.AllEnrolledCaptures),
                progress,
                token);
            ApplyMaintenanceResult(
                result,
                "CaptureMemory_Settings_ReanalyzeSucceeded",
                "Capture reanalysis was queued. Original captures were not modified.",
                reanalysis: true);
        }
        catch (OperationCanceledException)
        {
            ApplyCancelled();
        }
        catch
        {
            ApplyUnavailableOperation();
        }
        finally
        {
            EndOperation();
            await RefreshAfterOperationAsync();
        }
    }

    private async Task RunPolicyMutationAsync(
        Func<long, CancellationToken, ValueTask<CaptureAnalysisPolicyChangeResult>> mutation,
        string successKey,
        string successFallback)
    {
        CancellationToken token = BeginOperation(
            "CaptureMemory_Settings_Updating",
            "Updating Capture Memory settings…");
        try
        {
            CaptureAnalysisPolicySnapshot current = await _policyService!.GetCurrentAsync(token);
            CaptureAnalysisPolicyChangeResult result = await mutation(
                current.ControlDocumentRevision,
                token);
            ApplyPolicy(result.Policy);
            ApplyPolicyChangeResult(result, successKey, successFallback);
        }
        catch (OperationCanceledException)
        {
            ApplyCancelled();
        }
        catch
        {
            ApplyUnavailableOperation();
        }
        finally
        {
            EndOperation();
            await RefreshAfterOperationAsync();
        }
    }

    private async Task RunMaintenanceAsync(
        Func<CancellationToken, ValueTask<CaptureAnalysisMaintenanceResult>> operation,
        string successKey,
        string successFallback)
    {
        CancellationToken token = BeginOperation(
            "CaptureMemory_Settings_Updating",
            "Updating Capture Memory…");
        try
        {
            CaptureAnalysisMaintenanceResult result = await operation(token);
            ApplyMaintenanceResult(result, successKey, successFallback, reanalysis: false);
        }
        catch (OperationCanceledException)
        {
            ApplyCancelled();
        }
        catch
        {
            ApplyUnavailableOperation();
        }
        finally
        {
            EndOperation();
            await RefreshAfterOperationAsync();
        }
    }

    private async Task<bool> ConfirmAsync(CaptureAnalysisSettingsAction action)
    {
        if (_confirmationService == null)
        {
            ApplyUnavailableOperation();
            return false;
        }

        CaptureAnalysisConfirmationDecision decision = await _confirmationService.ConfirmAsync(
            new CaptureAnalysisSettingsConfirmationRequest(action),
            CancellationToken.None);
        return decision == CaptureAnalysisConfirmationDecision.Confirmed;
    }

    private CancellationToken BeginOperation(string statusKey, string statusFallback)
    {
        _policyReadGeneration++;
        _operationCancellation?.Dispose();
        _operationCancellation = new CancellationTokenSource();
        HasOperationFailure = false;
        NeedsRecovery = false;
        IsModelUnavailable = false;
        IsPreparingModels = false;
        IsSchedulingCaptures = false;
        OperationProgress = 0;
        OperationStatusText = GetString(statusKey, statusFallback);
        IsBusy = true;
        return _operationCancellation.Token;
    }

    private void EndOperation()
    {
        _policyReadGeneration++;
        IsBusy = false;
        IsPreparingModels = false;
        IsSchedulingCaptures = false;
        _operationCancellation?.Dispose();
        _operationCancellation = null;
        RaiseCommandStates();
    }

    private void CancelOperation()
    {
        _operationCancellation?.Cancel();
    }

    private void ReportProgress(CaptureAnalysisMaintenanceProgress progress)
    {
        OperationProgress = progress.FractionComplete;
        IsPreparingModels = progress.Phase == CaptureAnalysisMaintenancePhase.PreparingModels;
        IsSchedulingCaptures = progress.Phase == CaptureAnalysisMaintenancePhase.SchedulingCaptures;
        OperationStatusText = progress.Phase == CaptureAnalysisMaintenancePhase.PreparingModels
            ? GetString(
                "CaptureMemory_Settings_ReanalyzePreparing",
                "Preparing AI models for reanalysis…")
            : GetString(
                "CaptureMemory_Settings_ReanalyzeScheduling",
                "Queuing authorized captures for reanalysis…");
    }

    private void ApplyPolicy(CaptureAnalysisPolicySnapshot snapshot)
    {
        _policyReadGeneration++;
        IsPolicyAvailable = snapshot.Status == CaptureAnalysisPolicySnapshotStatus.Available;
        IsAuthorized = snapshot.IsProcessingAuthorized;
        IsAnalyzingNewCaptures = snapshot.IsProcessingAuthorized &&
            snapshot.Policy?.IsFutureCaptureAdmissionEnabled == true;
        ActiveCaptureCount = snapshot.ActiveCaptureCount;
        ReanalyzableCaptureCount = snapshot.ReanalyzableCaptureCount;
        ExcludedCaptureCount = snapshot.ExcludedCaptureCount;
        RaiseCommandStates();

        PolicyStatusText = snapshot.Status switch
        {
            CaptureAnalysisPolicySnapshotStatus.Unavailable => GetString(
                "CaptureMemory_Settings_StatusUnavailable",
                "Capture Memory status is unavailable."),
            CaptureAnalysisPolicySnapshotStatus.ConsentMismatch or
                CaptureAnalysisPolicySnapshotStatus.ConsentReviewRequired => GetString(
                    "CaptureMemory_Settings_StatusReview",
                    "Capture Memory is paused because its consent state needs review."),
            _ when !snapshot.IsProcessingAuthorized => GetString(
                "CaptureMemory_Settings_StatusOff",
                "Capture Memory is off. No capture analysis is authorized."),
            _ when snapshot.Policy?.IsFutureCaptureAdmissionEnabled == true => GetString(
                "CaptureMemory_Settings_StatusOn",
                "Capture Memory is on for new captures. Analysis stays on this device and originals are not modified."),
            _ => GetString(
                "CaptureMemory_Settings_StatusStopped",
                "Analysis of new captures is stopped. Existing app-managed metadata remains searchable."),
        };
    }

    private void ApplyUnavailablePolicy()
    {
        _policyReadGeneration++;
        IsPolicyAvailable = false;
        IsAuthorized = false;
        IsAnalyzingNewCaptures = false;
        ActiveCaptureCount = 0;
        ReanalyzableCaptureCount = 0;
        ExcludedCaptureCount = 0;
        PolicyStatusText = GetString(
            "CaptureMemory_Settings_StatusUnavailable",
            "Capture Memory status is unavailable.");
        HasOperationFailure = true;
    }

    private void ApplyPolicyChangeResult(
        CaptureAnalysisPolicyChangeResult result,
        string successKey,
        string successFallback)
    {
        switch (result.Status)
        {
            case CaptureAnalysisPolicyChangeStatus.Succeeded:
                OperationStatusText = GetString(successKey, successFallback);
                break;
            case CaptureAnalysisPolicyChangeStatus.ReconciliationRequired:
                NeedsRecovery = true;
                OperationStatusText = GetString(
                    "CaptureMemory_Settings_RecoveryRequired",
                    "The safety change is saved, but app-managed cleanup is incomplete. Restart or retry after files are available.");
                break;
            case CaptureAnalysisPolicyChangeStatus.Conflict:
                HasOperationFailure = true;
                OperationStatusText = GetString(
                    "CaptureMemory_Settings_Conflict",
                    "Capture Memory changed elsewhere. Its current state has been refreshed; try again.");
                break;
            default:
                ApplyUnavailableOperation();
                break;
        }
    }

    private void ApplyMaintenanceResult(
        CaptureAnalysisMaintenanceResult result,
        string successKey,
        string successFallback,
        bool reanalysis)
    {
        switch (result.Status)
        {
            case CaptureAnalysisMaintenanceStatus.Succeeded:
                OperationProgress = 1;
                OperationStatusText = GetString(successKey, successFallback);
                break;
            case CaptureAnalysisMaintenanceStatus.Incomplete when reanalysis:
                HasOperationFailure = true;
                IsModelUnavailable = true;
                OperationStatusText = result.AffectedCaptureCount > 0
                    ? GetString("CaptureMemory_Settings_ReanalyzePartial",
                        "Available analysis was queued. Some models or capture sources were unavailable; results will appear as supported analysis finishes.")
                    : GetString(
                        "CaptureMemory_Settings_ReanalyzeIncomplete",
                        "A required AI model or capture source was unavailable. Nothing was changed in the original captures; retry when it is available.");
                break;
            case CaptureAnalysisMaintenanceStatus.Incomplete:
                NeedsRecovery = true;
                OperationStatusText = GetString(
                    "CaptureMemory_Settings_RecoveryRequired",
                    "The safety change is saved, but app-managed cleanup is incomplete. Restart or retry after files are available.");
                break;
            case CaptureAnalysisMaintenanceStatus.Conflict:
                HasOperationFailure = true;
                OperationStatusText = GetString(
                    "CaptureMemory_Settings_Conflict",
                    "Capture Memory changed elsewhere. Its current state has been refreshed; try again.");
                break;
            case CaptureAnalysisMaintenanceStatus.Rejected:
                HasOperationFailure = true;
                OperationStatusText = GetString(
                    "CaptureMemory_Settings_Rejected",
                    "This action is no longer available for the current Capture Memory state.");
                break;
            default:
                HasOperationFailure = true;
                IsModelUnavailable = reanalysis;
                OperationStatusText = reanalysis
                    ? GetString(
                        "CaptureMemory_Settings_ReanalyzeUnavailable",
                        "Capture Memory or a required AI model is unavailable. Original captures were not changed.")
                    : GetString(
                        "CaptureMemory_Settings_OperationUnavailable",
                        "Capture Memory could not complete this action. Try again.");
                break;
        }
    }

    private void ApplyCancelled()
    {
        OperationStatusText = GetString(
            "CaptureMemory_Settings_Cancelled",
            "The operation was cancelled. Completed safety changes remain in effect.");
    }

    private void ApplyUnavailableOperation()
    {
        HasOperationFailure = true;
        OperationStatusText = GetString(
            "CaptureMemory_Settings_OperationUnavailable",
            "Capture Memory could not complete this action. Try again.");
    }

    private async Task RefreshAfterOperationAsync()
    {
        try
        {
            await RefreshAsync(CancellationToken.None);
        }
        catch
        {
            ApplyUnavailablePolicy();
        }
    }

    private void RaiseCommandStates()
    {
        RaisePropertyChanged(nameof(ReanalyzeAvailabilityText));
        RaisePropertyChanged(nameof(ShowReanalyzeAvailability));
        EnableCaptureMemoryCommand.NotifyCanExecuteChanged();
        StopAnalyzingNewCapturesCommand.NotifyCanExecuteChanged();
        ResumeAnalyzingNewCapturesCommand.NotifyCanExecuteChanged();
        TurnOffAndEraseCommand.NotifyCanExecuteChanged();
        ClearMemoryCommand.NotifyCanExecuteChanged();
        RebuildSearchIndexCommand.NotifyCanExecuteChanged();
        ReanalyzeCapturesCommand.NotifyCanExecuteChanged();
        CancelOperationCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
    }

    private string GetString(string key, string fallback)
    {
        string? value = _localizationService?.GetString(key);
        return string.IsNullOrWhiteSpace(value) || value == key ? fallback : value;
    }

    private IProgress<T> CreateOperationProgress<T>(Action<T> report)
    {
        CancellationTokenSource? operation = _operationCancellation;
        // Progress captures the UI context. Delayed callbacks cannot overwrite completion,
        // cancellation, or a newer operation's state.
        return new Progress<T>(value =>
        {
            if (ReferenceEquals(operation, _operationCancellation) &&
                operation?.IsCancellationRequested == false && IsBusy)
            {
                report(value);
            }
        });
    }
}
