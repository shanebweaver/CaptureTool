using CaptureTool.Application.Abstractions.Diagnostics.ClearLogs;
using CaptureTool.Application.Abstractions.Diagnostics.ExportLogs;
using CaptureTool.Application.Abstractions.Diagnostics.GetCurrentLogs;
using CaptureTool.Application.Abstractions.Diagnostics.GetIsLoggingEnabled;
using CaptureTool.Application.Abstractions.Diagnostics.UpdateLoggingState;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.Diagnostics;

public sealed partial class DiagnosticsViewModel : ViewModelBase
{
    private readonly IClearLogsUseCase _clearLogsCommand;
    private readonly IExportLogsUseCase _exportLogsCommand;
    private readonly IUpdateLoggingStateUseCase _updateLoggingStateCommand;
    private readonly IGetIsLoggingEnabledUseCase _getIsLoggingEnabledQuery;
    private readonly IGetCurrentLogsUseCase _getCurrentLogsQuery;
    private readonly ILogService _logService;
    private readonly ITelemetryService? _telemetryService;

    public IAsyncRelayCommand ClearLogsCommand { get; }
    public IAsyncRelayCommand ExportLogsCommand { get; }
    public IAsyncRelayCommand<bool> UpdateLoggingEnablementCommand { get; }

    public string Logs
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsLoggingEnabled
    {
        get;
        private set => Set(ref field, value);
    }

    public DiagnosticsViewModel(
        IClearLogsUseCase clearLogsCommand,
        IExportLogsUseCase exportLogsCommand,
        IUpdateLoggingStateUseCase updateLoggingEnablementCommand,
        IGetIsLoggingEnabledUseCase getIsLoggingEnabledQuery,
        IGetCurrentLogsUseCase getCurrentLogsQuery,
        ILogService logService,
        ITelemetryService? telemetryService = null)
    {
        _clearLogsCommand = clearLogsCommand;
        _exportLogsCommand = exportLogsCommand;
        _updateLoggingStateCommand = updateLoggingEnablementCommand;
        _getIsLoggingEnabledQuery = getIsLoggingEnabledQuery;
        _getCurrentLogsQuery = getCurrentLogsQuery;
        _telemetryService = telemetryService;

        _logService = logService;
        _logService.LogAdded += OnLogAdded;

        ClearLogsCommand = new AsyncRelayCommand(ClearLogsAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ExportLogsCommand = new AsyncRelayCommand(ExportLogsAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UpdateLoggingEnablementCommand = new AsyncRelayCommand<bool>(UpdateLoggingEnablementAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);

        IsLoggingEnabled = false;
        Logs = string.Empty;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        IsLoggingEnabled = (await _getIsLoggingEnabledQuery.ExecuteAsync(new GetIsLoggingEnabledRequest(), CancellationToken.None)).Value?.IsEnabled == true;
        Logs = string.Join(Environment.NewLine, ((await _getCurrentLogsQuery.ExecuteAsync(new GetCurrentLogsRequest(), CancellationToken.None)).Value?.Logs ?? []).Select(log => log.ToString()));
    }

    ~DiagnosticsViewModel()
    {
        _logService.LogAdded -= OnLogAdded;
    }

    private void OnLogAdded(object? sender, ILogEntry e)
    {
        Logs += e.ToString() + Environment.NewLine;
    }

    private async Task UpdateLoggingEnablementAsync(bool newValue)
    {
        IsLoggingEnabled = newValue;
        var response = await _updateLoggingStateCommand.ExecuteAsync(
            new UpdateLoggingStateRequest(newValue),
            CancellationToken.None);
        TrackDiagnosticsAction(
            "update_logging",
            response?.Result ?? UseCaseResult.Failed,
            newValue);
    }

    private async Task ClearLogsAsync()
    {
        Logs = string.Empty;
        var response = await _clearLogsCommand.ExecuteAsync(new ClearLogsRequest(), CancellationToken.None);
        TrackDiagnosticsAction("clear_logs", response?.Result ?? UseCaseResult.Failed);
    }

    private async Task ExportLogsAsync()
    {
        var response = await _exportLogsCommand.ExecuteAsync(new ExportLogsRequest(), CancellationToken.None);
        TrackDiagnosticsAction("export_logs", response?.Result ?? UseCaseResult.Failed);
    }

    private void TrackDiagnosticsAction(string action, UseCaseResult result, bool? enabled = null)
    {
        Dictionary<string, object?> properties = new()
        {
            [TelemetryProperties.Action] = action,
            [TelemetryProperties.Outcome] = result switch
            {
                UseCaseResult.Succeeded => TelemetryOutcomes.Succeeded,
                UseCaseResult.Cancelled => TelemetryOutcomes.Canceled,
                _ => TelemetryOutcomes.Failed
            }
        };

        if (enabled.HasValue)
        {
            properties[TelemetryProperties.Enabled] = enabled.Value;
        }

        _telemetryService?.TrackEvent(TelemetryEvents.DiagnosticsAction, properties);
    }
}
