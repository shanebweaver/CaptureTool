using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Library.CaptureMemory;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.Analysis;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CaptureTool.Presentation.Features.Home;

public sealed class CaptureMemoryHomeViewModel : ViewModelBase
{
    private readonly ObservableCollection<CaptureMemorySearchResultViewModel> _results = [];
    private readonly ICaptureMemoryFeatureAvailability? _featureAvailability;
    private readonly ICaptureMemorySearchService? _searchService;
    private readonly ICaptureMemoryResultResolver? _resultResolver;
    private readonly IOpenCaptureMemoryResultUseCase? _openResultUseCase;
    private readonly ICaptureAnalysisPolicyService? _policyService;
    private readonly ICaptureAnalysisPolicyCommandService? _policyCommandService;
    private readonly IUserInitiatedAnalysisCapabilityPreparationService? _preparationService;
    private readonly ICaptureAnalysisBackfillService? _backfillService;
    private readonly ICaptureAnalysisMaintenanceService? _maintenanceService;
    private readonly ICaptureAssetRemovalService? _assetRemovalService;
    private readonly ICaptureAnalysisSettingsConfirmationDialogService? _confirmationService;
    private readonly ILocalizationService? _localizationService;
    private CancellationTokenSource? _searchCancellation;
    private CancellationTokenSource? _policyRefreshCancellation;
    private Task _backfillCompletion = Task.CompletedTask;
    private Task _searchCompletion = Task.CompletedTask;
    private int _searchGeneration;
    private string _searchQuery = string.Empty;

    public CaptureMemoryHomeViewModel(
        ICaptureMemoryFeatureAvailability? featureAvailability = null,
        ICaptureMemorySearchService? searchService = null,
        ICaptureMemoryResultResolver? resultResolver = null,
        IOpenCaptureMemoryResultUseCase? openResultUseCase = null,
        ICaptureAnalysisPolicyService? policyService = null,
        ICaptureAnalysisPolicyCommandService? policyCommandService = null,
        IUserInitiatedAnalysisCapabilityPreparationService? preparationService = null,
        ICaptureAnalysisMaintenanceService? maintenanceService = null,
        ICaptureAssetRemovalService? assetRemovalService = null,
        ILocalizationService? localizationService = null,
        ICaptureAnalysisSettingsConfirmationDialogService? confirmationService = null,
        ICaptureAnalysisBackfillService? backfillService = null)
    {
        _featureAvailability = featureAvailability;
        _searchService = searchService;
        _resultResolver = resultResolver;
        _openResultUseCase = openResultUseCase;
        _policyService = policyService;
        _policyCommandService = policyCommandService;
        _preparationService = preparationService;
        _backfillService = backfillService;
        _maintenanceService = maintenanceService;
        _assetRemovalService = assetRemovalService;
        _confirmationService = confirmationService;
        _localizationService = localizationService;

        EnableForFutureCommand = new AsyncRelayCommand(
            () => EnableAsync(includeExistingCaptures: false),
            CanEnable,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        EnableForExistingCommand = new AsyncRelayCommand(
            () => EnableAsync(includeExistingCaptures: true),
            CanEnable,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ClearSearchCommand = new RelayCommand(
            () => SearchQuery = string.Empty,
            () => !string.IsNullOrEmpty(SearchQuery));
        RetryCommand = new AsyncRelayCommand(RetryAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        OpenResultCommand = new AsyncRelayCommand<CaptureMemorySearchResultViewModel>(
            OpenResultAsync,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        RemoveResultCommand = new AsyncRelayCommand<CaptureMemorySearchResultViewModel>(
            RemoveResultAsync,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        DeleteResultCommand = new AsyncRelayCommand<CaptureMemorySearchResultViewModel>(
            DeleteResultAsync,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    public IAsyncRelayCommand EnableForFutureCommand { get; }

    public IAsyncRelayCommand EnableForExistingCommand { get; }

    public IRelayCommand ClearSearchCommand { get; }

    public IAsyncRelayCommand RetryCommand { get; }

    public IAsyncRelayCommand<CaptureMemorySearchResultViewModel> OpenResultCommand { get; }

    public IAsyncRelayCommand<CaptureMemorySearchResultViewModel> RemoveResultCommand { get; }

    public IAsyncRelayCommand<CaptureMemorySearchResultViewModel> DeleteResultCommand { get; }

    public ObservableCollection<CaptureMemorySearchResultViewModel> Results => _results;

    public Task BackfillCompletion => _backfillCompletion;

    public Task SearchCompletion => _searchCompletion;

    public bool IsFeatureEnabled => _featureAvailability?.IsCaptureMemorySearchEnabled == true;

    public bool ShowSetup => IsFeatureEnabled && (!IsAuthorized || IsPreparing || HasSetupFailure);

    public bool ShowSearch => IsFeatureEnabled && IsAuthorized && !IsPreparing;

    public bool ShowRecentGallery => string.IsNullOrWhiteSpace(SearchQuery);

    public bool ShowResults => ShowSearch && !ShowRecentGallery && Results.Count > 0;

    public bool ShowNoMatches => ShowSearch && !ShowRecentGallery &&
        !IsSearching && !HasSearchFailure && !HasCorruptProjection && Results.Count == 0;

    public bool ShowPartialResults => IsIndexing && Results.Count > 0;

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            value ??= string.Empty;
            if (!Set(ref _searchQuery, value))
            {
                return;
            }

            RaiseDisplayStateChanged();
            _searchCompletion = QueueSearchAsync(value);
            ClearSearchCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsAuthorized
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(ShowSetup));
                RaisePropertyChanged(nameof(ShowSearch));
                RaiseDisplayStateChanged();
            }
        }
    }

    public bool IsPreparing
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                EnableForFutureCommand.NotifyCanExecuteChanged();
                EnableForExistingCommand.NotifyCanExecuteChanged();
                RaisePropertyChanged(nameof(ShowSetup));
                RaisePropertyChanged(nameof(ShowSearch));
            }
        }
    }

    public double PreparationProgress { get; private set => Set(ref field, value); }

    public bool IsSearching
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaiseDisplayStateChanged();
            }
        }
    }

    public bool IsIndexing
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(ShowPartialResults));
            }
        }
    }

    public double IndexProgress { get; private set => Set(ref field, value); }

    public bool HasLimitedModelCoverage { get; private set => Set(ref field, value); }

    public bool HasSetupFailure
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(ShowSetup));
            }
        }
    }

    public bool HasSearchFailure
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaiseDisplayStateChanged();
            }
        }
    }

    public bool HasCorruptProjection
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaiseDisplayStateChanged();
            }
        }
    }

    public bool HasSourceMissingResults { get; private set => Set(ref field, value); }

    public bool HasFailedResults { get; private set => Set(ref field, value); }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        await RefreshPolicyAsync(cancellationToken);
        if (IsIndexing && _backfillService != null && _backfillCompletion.IsCompleted)
        {
            _backfillCompletion = RunBackfillAsync();
        }
    }

    public override void Dispose()
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _policyRefreshCancellation?.Cancel();
        base.Dispose();
    }

    private bool CanEnable() => IsFeatureEnabled && !IsPreparing;

    private async Task EnableAsync(bool includeExistingCaptures)
    {
        if (_policyService == null ||
            _policyCommandService == null ||
            _preparationService == null ||
            includeExistingCaptures && _backfillService == null)
        {
            HasSetupFailure = true;
            return;
        }

        IsPreparing = true;
        PreparationProgress = 0;
        HasSetupFailure = false;
        HasLimitedModelCoverage = false;

        try
        {
            CaptureAnalysisPolicySnapshot current = await _policyService.GetCurrentAsync(CancellationToken.None);
            var consent = new CaptureAnalysisConsentResponse(
                CaptureAnalysisPolicyDefaults.CreateConsentDisclosure(),
                CaptureAnalysisConsentDecision.GrantedForFutureCaptures);
            CaptureAnalysisPolicyChangeResult consentChange = await _policyCommandService.ApplyConsentDecisionAsync(
                consent,
                current.ControlDocumentRevision,
                CancellationToken.None);
            if (consentChange.Status != CaptureAnalysisPolicyChangeStatus.Succeeded ||
                consentChange.Policy.Policy?.ProcessingPolicy is not { } processingPolicy)
            {
                HasSetupFailure = true;
                return;
            }

            IsAuthorized = consentChange.Policy.IsProcessingAuthorized;
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
                    RecipeCapability = capability,
                }))
                .ToArray();
            for (int index = 0; index < preparations.Length; index++)
            {
                RecipeCapability recipeCapability = preparations[index].RecipeCapability;
                CaptureMediaKind mediaKind = preparations[index].MediaKind;
                double capabilityStart = (double)index / preparations.Length;
                double capabilityShare = 1d / preparations.Length;
                var progress = new Progress<AnalysisCapabilityPreparationProgress>(value =>
                    PreparationProgress = capabilityStart + (value.FractionComplete * capabilityShare));
                var request = new AnalysisCapabilityPreparationRequest(
                    recipeCapability.Capability,
                    mediaKind,
                    CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurpose,
                    processingPolicy);
                AnalysisCapabilityPreparationState prepared = await _preparationService.PrepareAsync(
                    request,
                    progress,
                    CancellationToken.None);

                if (prepared.Status != AnalysisCapabilityPreparationStatus.Ready)
                {
                    // Analyzer inventory is media-specific. An unavailable audio, video, or
                    // optional description model must not prevent supported captures (notably
                    // images with legacy OCR fallback) from being enrolled and searched.
                    HasLimitedModelCoverage = true;
                }
            }

            PreparationProgress = 1;
            CaptureAnalysisPolicySnapshot resultingPolicy = consentChange.Policy;
            if (includeExistingCaptures)
            {
                CaptureAnalysisPolicyChangeResult backfill = await _policyCommandService.AuthorizeExistingCaptureBackfillAsync(
                    resultingPolicy.ControlDocumentRevision,
                    CancellationToken.None);
                if (backfill.Status != CaptureAnalysisPolicyChangeStatus.Succeeded)
                {
                    HasSetupFailure = true;
                    return;
                }

                resultingPolicy = backfill.Policy;
            }

            ApplyPolicy(resultingPolicy);
            if (includeExistingCaptures)
            {
                _backfillCompletion = RunBackfillAsync();
            }
        }
        catch (OperationCanceledException)
        {
            HasSetupFailure = true;
        }
        catch
        {
            HasSetupFailure = true;
        }
        finally
        {
            IsPreparing = false;
        }
    }

    private async Task RunBackfillAsync()
    {
        try
        {
            var progress = new Progress<CaptureAnalysisBackfillProgress>(value =>
            {
                IndexProgress = value.Fraction;
                IsIndexing = value.Checkpoint < value.UpperSequence;
            });
            CaptureAnalysisBackfillRunResult result = await _backfillService!.RunAsync(
                progress,
                CancellationToken.None);
            if (result.Status is not (
                CaptureAnalysisBackfillRunStatus.Completed or
                CaptureAnalysisBackfillRunStatus.AlreadyCompleted))
            {
                HasSetupFailure = true;
            }
        }
        catch (OperationCanceledException)
        {
            HasSetupFailure = true;
        }
        catch
        {
            HasSetupFailure = true;
        }
        finally
        {
            await RefreshPolicyAsync(CancellationToken.None);
        }
    }

    private async Task RefreshPolicyAsync(CancellationToken cancellationToken)
    {
        if (!IsFeatureEnabled || _policyService == null)
        {
            IsAuthorized = false;
            IsIndexing = false;
            IndexProgress = 0;
            return;
        }

        try
        {
            ApplyPolicy(await _policyService.GetCurrentAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            IsAuthorized = false;
            HasSetupFailure = true;
        }
    }

    private void ApplyPolicy(CaptureAnalysisPolicySnapshot snapshot)
    {
        IsAuthorized = snapshot.IsProcessingAuthorized;
        UpdateIndexState(snapshot.Policy);
        if (IsIndexing && _policyRefreshCancellation == null)
        {
            _policyRefreshCancellation = new CancellationTokenSource();
            _ = PollPolicyAsync(_policyRefreshCancellation);
        }
    }

    private void UpdateIndexState(CaptureAnalysisPolicy? policy)
    {
        IsIndexing = policy?.BackfillState is
            CaptureAnalysisBackfillState.Authorized or CaptureAnalysisBackfillState.InProgress;
        IndexProgress = policy?.BackfillUpperSequence > 0
            ? Math.Clamp((double)policy.BackfillCheckpoint / policy.BackfillUpperSequence, 0, 1)
            : policy?.BackfillState == CaptureAnalysisBackfillState.Completed ? 1 : 0;
    }

    private async Task PollPolicyAsync(CancellationTokenSource cancellation)
    {
        try
        {
            while (IsIndexing && _policyService != null)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellation.Token);
                CaptureAnalysisPolicySnapshot snapshot = await _policyService.GetCurrentAsync(cancellation.Token);
                IsAuthorized = snapshot.IsProcessingAuthorized;
                UpdateIndexState(snapshot.Policy);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch
        {
            // Index progress is advisory; partial search remains available.
        }
        finally
        {
            if (ReferenceEquals(_policyRefreshCancellation, cancellation))
            {
                _policyRefreshCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private Task QueueSearchAsync(string query)
    {
        int generation = Interlocked.Increment(ref _searchGeneration);
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = null;

        if (string.IsNullOrWhiteSpace(query))
        {
            _results.Clear();
            IsSearching = false;
            HasSearchFailure = false;
            HasCorruptProjection = false;
            HasSourceMissingResults = false;
            HasFailedResults = false;
            RaiseDisplayStateChanged();
            return Task.CompletedTask;
        }

        if (!ShowSearch || _searchService == null || _resultResolver == null)
        {
            return Task.CompletedTask;
        }

        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        return SearchAsync(query, generation, cancellation.Token);
    }

    private async Task SearchAsync(string query, int generation, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
            IsSearching = true;
            HasSearchFailure = false;
            HasCorruptProjection = false;
            HasFailedResults = false;
            IReadOnlyList<CaptureMemorySearchResult> results = await _searchService!.SearchAsync(
                new CaptureMemorySearchRequest(query, 50),
                cancellationToken);
            var resolved = new List<CaptureMemorySearchResultViewModel>(results.Count);
            bool hasSourceMissing = false;
            bool hasFailedResults = false;
            foreach (CaptureMemorySearchResult result in results)
            {
                CaptureMemoryResultLocation location = await _resultResolver!.ResolveAsync(
                    result.CaptureId,
                    cancellationToken);
                if (location.Status == CaptureMemoryResultLocationStatus.Forgotten)
                {
                    continue;
                }

                var model = new CaptureMemorySearchResultViewModel(result, location, _localizationService);
                hasSourceMissing |= model.IsSourceMissing;
                hasFailedResults |= model.IsResolutionFailed;
                resolved.Add(model);
            }

            if (generation != _searchGeneration || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _results.Clear();
            foreach (CaptureMemorySearchResultViewModel result in resolved)
            {
                _results.Add(result);
            }

            HasSourceMissingResults = hasSourceMissing;
            HasFailedResults = hasFailedResults;
            RaiseDisplayStateChanged();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (InvalidDataException)
        {
            if (generation == _searchGeneration)
            {
                _results.Clear();
                HasCorruptProjection = true;
                RaiseDisplayStateChanged();
            }
        }
        catch
        {
            if (generation == _searchGeneration)
            {
                _results.Clear();
                HasSearchFailure = true;
                RaiseDisplayStateChanged();
            }
        }
        finally
        {
            if (generation == _searchGeneration)
            {
                IsSearching = false;
            }
        }
    }

    private async Task RetryAsync()
    {
        if (HasCorruptProjection && _maintenanceService != null)
        {
            CaptureAnalysisMaintenanceResult rebuilt = await _maintenanceService.RebuildSearchIndexAsync(CancellationToken.None);
            if (rebuilt.Status != CaptureAnalysisMaintenanceStatus.Succeeded)
            {
                HasSearchFailure = true;
                return;
            }
        }

        string query = SearchQuery;
        SearchQuery = string.Empty;
        SearchQuery = query;
        await SearchCompletion;
    }

    private async Task OpenResultAsync(CaptureMemorySearchResultViewModel? model)
    {
        if (model == null || _openResultUseCase == null)
        {
            return;
        }

        UseCaseResponse<OpenCaptureMemoryResultResponse> response = await _openResultUseCase.ExecuteAsync(
            new OpenCaptureMemoryResultRequest(model.CaptureId),
            CancellationToken.None);
        if (response.Value?.Status is OpenCaptureMemoryResultStatus.SourceMissing or OpenCaptureMemoryResultStatus.Forgotten)
        {
            model.MarkSourceMissing();
            HasSourceMissingResults = true;
        }
        else if (response.Value?.Status != OpenCaptureMemoryResultStatus.Opened)
        {
            HasSearchFailure = true;
        }
    }

    private Task RemoveResultAsync(CaptureMemorySearchResultViewModel? model)
    {
        return RemoveResultAsync(
            model,
            CaptureAssetRemovalKind.ForgetHistory,
            CaptureAnalysisSettingsAction.RemoveFromMemory);
    }

    private Task DeleteResultAsync(CaptureMemorySearchResultViewModel? model)
    {
        if (model?.CanDeleteCapture != true)
        {
            return Task.CompletedTask;
        }

        return RemoveResultAsync(
            model,
            CaptureAssetRemovalKind.DeleteRetainedSource,
            CaptureAnalysisSettingsAction.DeleteCapture);
    }

    private async Task RemoveResultAsync(
        CaptureMemorySearchResultViewModel? model,
        CaptureAssetRemovalKind kind,
        CaptureAnalysisSettingsAction confirmationAction)
    {
        if (model == null || _assetRemovalService == null || _confirmationService == null)
        {
            HasSearchFailure = true;
            return;
        }

        CaptureAnalysisConfirmationDecision decision = await _confirmationService.ConfirmAsync(
            new CaptureAnalysisSettingsConfirmationRequest(confirmationAction),
            CancellationToken.None);
        if (decision != CaptureAnalysisConfirmationDecision.Confirmed)
        {
            return;
        }

        CaptureAssetRemovalResult removed = await _assetRemovalService.RemoveAsync(
            new CaptureAssetRemovalRequest(
                model.CaptureId,
                kind,
                isConfirmed: kind == CaptureAssetRemovalKind.DeleteRetainedSource),
            CancellationToken.None);
        if (removed.Status is CaptureAssetRemovalStatus.Succeeded or
            CaptureAssetRemovalStatus.AlreadyRemoved or
            CaptureAssetRemovalStatus.Incomplete)
        {
            _results.Remove(model);
            RaiseDisplayStateChanged();
            HasSearchFailure = removed.Status == CaptureAssetRemovalStatus.Incomplete;
        }
        else
        {
            HasSearchFailure = true;
        }
    }

    private void RaiseDisplayStateChanged()
    {
        RaisePropertyChanged(nameof(ShowRecentGallery));
        RaisePropertyChanged(nameof(ShowResults));
        RaisePropertyChanged(nameof(ShowNoMatches));
        RaisePropertyChanged(nameof(ShowPartialResults));
    }
}
