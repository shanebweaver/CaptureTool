using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Library.CaptureMemory;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Presentation.ViewModels;
using CaptureTool.Presentation.Features.CaptureMemory;
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
    private readonly ICaptureMemoryWorkflow? _workflow;
    private bool _workflowSubscribed;
    private bool _workflowBusy;
    private bool _starting;
    private readonly ICaptureAssetRemovalService? _assetRemovalService;
    private readonly ICaptureAnalysisSettingsConfirmationDialogService? _confirmationService;
    private readonly ILocalizationService? _localizationService;
    private CancellationTokenSource? _searchCancellation;
    private readonly CaptureMemoryStateRefreshLoop _stateRefresh;
    private long _policyReadGeneration;
    private Task _searchCompletion = Task.CompletedTask;
    private int _searchGeneration;
    private string _searchQuery = string.Empty;
    private readonly ICaptureMemorySearchChangeNotifier? _searchChangeNotifier;
    private SynchronizationContext? _searchUiContext;
    private bool _searchChangesSubscribed;

    public CaptureMemoryHomeViewModel(
        ICaptureMemoryFeatureAvailability? featureAvailability = null,
        ICaptureMemorySearchService? searchService = null,
        ICaptureMemoryResultResolver? resultResolver = null,
        IOpenCaptureMemoryResultUseCase? openResultUseCase = null,
        ICaptureMemoryWorkflow? workflow = null,
        ICaptureAssetRemovalService? assetRemovalService = null,
        ILocalizationService? localizationService = null,
        ICaptureAnalysisSettingsConfirmationDialogService? confirmationService = null)
    {
        _featureAvailability = featureAvailability;
        _searchService = searchService;
        _searchChangeNotifier = searchService as ICaptureMemorySearchChangeNotifier;
        _resultResolver = resultResolver;
        _openResultUseCase = openResultUseCase;
        _workflow = workflow;
        _assetRemovalService = assetRemovalService;
        _confirmationService = confirmationService;
        _localizationService = localizationService;
        _stateRefresh = new CaptureMemoryStateRefreshLoop(RefreshPolicyAsync);

        EnableCaptureMemoryCommand = new AsyncRelayCommand(
            EnableAsync,
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

    public IAsyncRelayCommand EnableCaptureMemoryCommand { get; }

    public bool IncludeExistingCaptures { get; set => Set(ref field, value); }

    public bool CanChangeSetupOptions => IsFeatureEnabled && !_workflowBusy && !_starting;

    public IRelayCommand ClearSearchCommand { get; }

    public IAsyncRelayCommand RetryCommand { get; }

    public IAsyncRelayCommand<CaptureMemorySearchResultViewModel> OpenResultCommand { get; }

    public IAsyncRelayCommand<CaptureMemorySearchResultViewModel> RemoveResultCommand { get; }

    public IAsyncRelayCommand<CaptureMemorySearchResultViewModel> DeleteResultCommand { get; }

    public ObservableCollection<CaptureMemorySearchResultViewModel> Results => _results;

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
                EnableCaptureMemoryCommand.NotifyCanExecuteChanged();
                RaisePropertyChanged(nameof(CanChangeSetupOptions));
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
        _searchUiContext = SynchronizationContext.Current;
        if (!_searchChangesSubscribed && _searchChangeNotifier != null)
        {
            _searchChangeNotifier.SearchIndexChanged += OnSearchIndexChanged;
            _searchChangesSubscribed = true;
        }

        if (!_workflowSubscribed && _workflow != null)
        {
            _workflow.Changed += OnWorkflowChanged;
            _workflowSubscribed = true;
        }
        await RefreshPolicyAsync(cancellationToken);
        if (IsFeatureEnabled)
        {
            _stateRefresh.Start();
        }
        _searchCompletion = QueueSearchAsync(SearchQuery, debounce: false);
    }

    public override void Dispose()
    {
        if (_searchChangesSubscribed && _searchChangeNotifier != null)
        {
            _searchChangeNotifier.SearchIndexChanged -= OnSearchIndexChanged;
            _searchChangesSubscribed = false;
        }
        if (_workflowSubscribed && _workflow != null)
        {
            _workflow.Changed -= OnWorkflowChanged;
            _workflowSubscribed = false;
        }
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = null;
        _stateRefresh.Dispose();
        _policyReadGeneration++;
        base.Dispose();
    }

    private bool CanEnable() => CanChangeSetupOptions && _workflow != null;

    private async Task EnableAsync()
    {
        if (_workflow == null) { return; }
        bool includeExisting = IncludeExistingCaptures;
        _starting = true;
        IsPreparing = true;
        HasSetupFailure = false;
        NotifyWorkflowState();
        try
        {
            CaptureMemoryOperation result = await _workflow.ExecuteAsync(
                new(CaptureMemoryOperationKind.Enable, includeExisting), CancellationToken.None);
            if (includeExisting && result.IsSchedulingComplete) { IncludeExistingCaptures = false; }
            HasSetupFailure = result.Status is not (CaptureMemoryOperationStatus.Succeeded or CaptureMemoryOperationStatus.Partial);
        }
        catch { HasSetupFailure = true; }
        finally
        {
            _starting = false;
            if (_workflowSubscribed) { await RefreshPolicyAsync(CancellationToken.None); }
            NotifyWorkflowState();
            _searchCompletion = QueueSearchAsync(SearchQuery, debounce: false);
        }
    }

    private async Task RefreshPolicyAsync(CancellationToken cancellationToken)
    {
        if (!IsFeatureEnabled || _workflow == null) { IsAuthorized = false; return; }
        long generation = ++_policyReadGeneration;
        try
        {
            CaptureMemoryWorkflowSnapshot snapshot = await _workflow.GetCurrentAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != _policyReadGeneration) { return; }
            bool wasSearchVisible = ShowSearch;
            IsAuthorized = snapshot.Policy.IsProcessingAuthorized;
            _workflowBusy = snapshot.IsBusy;
            CaptureMemoryOperation? operation = snapshot.Operation;
            IsPreparing = _starting || operation is { IsRunning: true, Request.Kind: CaptureMemoryOperationKind.Enable,
                Phase: CaptureMemoryOperationPhase.Accepted or CaptureMemoryOperationPhase.Authorizing or CaptureMemoryOperationPhase.PreparingModels };
            IsIndexing = operation is { IsRunning: true, Phase: CaptureMemoryOperationPhase.SchedulingCaptures };
            PreparationProgress = IndexProgress = snapshot.FractionComplete;
            if (operation?.Request.Kind == CaptureMemoryOperationKind.Enable)
            {
                HasLimitedModelCoverage = operation.HasLimitedModelCoverage;
                HasSetupFailure = operation.Status is CaptureMemoryOperationStatus.Failed or CaptureMemoryOperationStatus.Conflict or CaptureMemoryOperationStatus.Rejected;
            }
            NotifyWorkflowState();
            if (wasSearchVisible != ShowSearch) { _searchCompletion = QueueSearchAsync(SearchQuery, debounce: false); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            if (generation == _policyReadGeneration) { IsAuthorized = false; HasSetupFailure = true; }
        }
    }

    private void NotifyWorkflowState()
    {
        RaisePropertyChanged(nameof(CanChangeSetupOptions));
        EnableCaptureMemoryCommand.NotifyCanExecuteChanged();
    }

    private void OnWorkflowChanged(object? sender, EventArgs e)
    {
        void Refresh() { if (_workflowSubscribed) { _ = RefreshPolicyAsync(CancellationToken.None); } }
        if (_searchUiContext != null && SynchronizationContext.Current != _searchUiContext) { _searchUiContext.Post(_ => Refresh(), null); }
        else { Refresh(); }
    }

    private void OnSearchIndexChanged(object? sender, EventArgs e)
    {
        void RefreshSearch()
        {
            if (_searchChangesSubscribed)
            {
                _searchCompletion = QueueSearchAsync(SearchQuery, debounce: false);
            }
        }

        // Index updates originate in the analysis worker, but bound results belong to the UI.
        if (_searchUiContext != null && SynchronizationContext.Current != _searchUiContext)
        {
            _searchUiContext.Post(_ => RefreshSearch(), null);
        }
        else
        {
            RefreshSearch();
        }
    }

    private Task QueueSearchAsync(string query, bool debounce = true)
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
            _results.Clear();
            IsSearching = false;
            RaiseDisplayStateChanged();
            return Task.CompletedTask;
        }

        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        return SearchAsync(query, generation, cancellation.Token, debounce);
    }

    private async Task SearchAsync(string query, int generation, CancellationToken cancellationToken, bool debounce)
    {
        try
        {
            if (debounce)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
            }
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
        if (HasCorruptProjection && _workflow != null)
        {
            CaptureMemoryOperation rebuilt = await _workflow.ExecuteAsync(new(CaptureMemoryOperationKind.RebuildSearch), CancellationToken.None);
            if (rebuilt.Status != CaptureMemoryOperationStatus.Succeeded)
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
