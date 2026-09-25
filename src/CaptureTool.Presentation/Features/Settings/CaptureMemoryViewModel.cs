using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Domain.Analysis;
using CaptureTool.Presentation.Notifications;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.Settings;

/// <summary>One UI observer shared by the settings page and shell. No page owns analysis work.</summary>
public sealed class CaptureMemoryViewModel : ViewModelBase
{
    private readonly ICaptureMemoryService _memory;
    private readonly ICaptureNamingService? _naming;
    private readonly ITaskEnvironment _ui;
    private readonly ILocalizationService _localization;
    private readonly IAppNotificationService _notifications;
    private string? _reportedFailure;
    private string? _pendingFailure;
    private string? _reportedOperationFailure;
    private string? _pendingOperationFailure;
    private int _operationRecovered;
    private int _updateQueued;
    private bool _disposed;

    public bool NamingEnabled { get; private set => Set(ref field, value); }
    public bool CanSetNaming { get; private set => Set(ref field, value); }
    public IAsyncRelayCommand<bool> SetNamingCommand { get; }
    public bool ScanningEnabled { get; private set => Set(ref field, value); }
    public bool ConsentGranted { get; private set => Set(ref field, value); }
    public bool CanScan { get; private set => Set(ref field, value); }
    public bool CanDelete { get; private set => Set(ref field, value); }
    public bool IsAnalysisActive { get; private set => Set(ref field, value); }
    public string ProgressText { get; private set => Set(ref field, value); } = string.Empty;
    public IAsyncRelayCommand<bool> SetScanningCommand { get; }
    public IAsyncRelayCommand<bool> SetConsentCommand { get; }
    public IAsyncRelayCommand ScanCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }

    public CaptureMemoryViewModel(ICaptureMemoryService memory, ITaskEnvironment ui,
        ILocalizationService localization, IAppNotificationService notifications, ICaptureNamingService? naming = null)
    {
        _memory = memory;
        _naming = naming;
        _ui = ui;
        _localization = localization;
        _notifications = notifications;
        SetNamingCommand = new AsyncRelayCommand<bool>(value => RunAsync(async () =>
        {
            if (_naming != null && !await _naming.SetEnabledAsync(value))
                _notifications.ShowError(_localization.GetString("CaptureNaming_SaveFailed"));
        }));
        SetScanningCommand = new AsyncRelayCommand<bool>(value => RunAsync(() => memory.SetScanningAsync(value)));
        SetConsentCommand = new AsyncRelayCommand<bool>(value => RunAsync(() => memory.SetConsentAsync(value)));
        ScanCommand = new AsyncRelayCommand(() => RunAsync(() => memory.ScanExistingAsync()), () => CanScan);
        DeleteCommand = new AsyncRelayCommand(() => RunAsync(() => memory.DeleteMetadataAsync()), () => CanDelete);
        _memory.StateChanged += QueueUpdate;
        if (_naming != null) _naming.Changed += QueueUpdate;
        QueueUpdate();
    }
    public Task RefreshAsync() => RunAsync(() => _memory.RefreshAsync());
    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        finally { QueueUpdate(); }
    }
    private void QueueUpdate()
    {
        if (_disposed) return;
        // Preserve a terminal failure even if the next capture starts before the UI dispatches.
        CaptureMemoryState state = _memory.State;
        if (GetAnalysisFailure(state) is { } failure) Interlocked.Exchange(ref _pendingFailure, failure);
        if (state.FailureCode is { } operationFailure) Interlocked.Exchange(ref _pendingOperationFailure, operationFailure);
        else Interlocked.Exchange(ref _operationRecovered, 1);
        if (Interlocked.Exchange(ref _updateQueued, 1) != 0) return;
        if (!_ui.TryExecute(() =>
        {
            Interlocked.Exchange(ref _updateQueued, 0);
            if (!_disposed) ApplyState();
        })) Interlocked.Exchange(ref _updateQueued, 0);
    }
    private void ApplyState()
    {
        CaptureMemoryState state = _memory.State;
        ScanningEnabled = state.PolicyAvailable && state.Policy.ScanningEnabled;
        ConsentGranted = state.ConsentAvailable && state.Policy.ConsentGranted;
        NamingEnabled = _naming?.IsEnabled == true;
        CanSetNaming = _naming?.IsAvailable == true && (NamingEnabled || state.PolicyAvailable && state.Policy.IsAllowed);
        CanScan = state.CanScan;
        CanDelete = state.CanDelete;
        IsAnalysisActive = state.IsLoading;
        ProgressText = state.IsLoading ? _localization.GetString(state.Activity.Activity == AnalysisActivity.Preparing
            ? "CaptureMemory_Preparing" : "CaptureMemory_Analyzing") : string.Empty;
        ScanCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        if (Interlocked.Exchange(ref _operationRecovered, 0) != 0) _reportedOperationFailure = null;
        string? pendingOperation = Interlocked.Exchange(ref _pendingOperationFailure, null);
        string? operationFailure = state.FailureCode ?? pendingOperation;
        ShowFailure(operationFailure, ref _reportedOperationFailure);
        _reportedOperationFailure = state.FailureCode;
        string? pendingFailure = Interlocked.Exchange(ref _pendingFailure, null);
        string? failure = GetAnalysisFailure(state) ?? pendingFailure;
        if (operationFailure == null) ShowFailure(failure, ref _reportedFailure);
        // Starting the next queued capture is not recovery from the preceding failure.
        if (GetAnalysisFailure(state) == null && state.Activity.Activity == AnalysisActivity.Idle &&
            state.Activity.LastRunStatus == AnalysisRunStatus.Completed && !state.Activity.LastRunHadFailures)
            _reportedFailure = null;
    }
    private void ShowFailure(string? failure, ref string? reported)
    {
        if (failure != null && failure != reported)
        {
            string key = failure switch
            {
                "policy-save" or "policy-unavailable" => "CaptureMemory_Error_Policy",
                "capture-registration" => "CaptureMemory_Error_Registration",
                "provider-unavailable" => "CaptureMemory_Error_Provider",
                "cleanup-pending" => "CaptureMemory_Error_Cleanup",
                "analysis-failed" => "CaptureMemory_Error_Analysis",
                _ => "CaptureMemory_Error_Storage"
            };
            _notifications.ShowError(_localization.GetString(key));
            reported = failure;
        }
    }
    private static string? GetAnalysisFailure(CaptureMemoryState state) =>
        (state.Activity.Activity == AnalysisActivity.ProviderUnavailable ? "provider-unavailable" :
        state.Activity.LastRunStatus == AnalysisRunStatus.Failed || state.Activity.LastRunHadFailures ? "analysis-failed" : null);
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _memory.StateChanged -= QueueUpdate;
        if (_naming != null) _naming.Changed -= QueueUpdate;
        base.Dispose();
    }
}
