using CaptureTool.Application.Abstractions.Diagnostics.ClearLogs;
using CaptureTool.Application.Abstractions.Diagnostics.ExportLogs;
using CaptureTool.Application.Abstractions.Diagnostics.GetCurrentLogs;
using CaptureTool.Application.Abstractions.Diagnostics.GetIsLoggingEnabled;
using CaptureTool.Application.Abstractions.Diagnostics.UpdateLoggingState;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Presentation.Shared.Commands;
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

        _logService = logService;
        _telemetryService = telemetryService;
        _logService.LogAdded += OnLogAdded;

        ClearLogsCommand = TelemetryCommandFactory.Async("diagnostics.clear_logs", ClearLogsAsync, telemetryService, "diagnostics");
        ExportLogsCommand = TelemetryCommandFactory.Async("diagnostics.export_logs", ExportLogsAsync, telemetryService, "diagnostics");
        UpdateLoggingEnablementCommand = TelemetryCommandFactory.Async<bool>("diagnostics.update_logging_enablement", UpdateLoggingEnablementAsync, telemetryService, "diagnostics");

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
        await _updateLoggingStateCommand.ExecuteAsync(new UpdateLoggingStateRequest(newValue), CancellationToken.None);
        TrackDiagnosticsAction("diagnostics.update_logging_enablement");
    }

    private async Task ClearLogsAsync()
    {
        Logs = string.Empty;
        await _clearLogsCommand.ExecuteAsync(new ClearLogsRequest(), CancellationToken.None);
        TrackDiagnosticsAction("diagnostics.clear_logs");
    }

    private async Task ExportLogsAsync()
    {
        await _exportLogsCommand.ExecuteAsync(new ExportLogsRequest(), CancellationToken.None);
        TrackDiagnosticsAction("diagnostics.export_logs");
    }

    private void TrackDiagnosticsAction(string commandId)
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.DiagnosticsActionInvoked,
            new Dictionary<string, object?>
            {
                [TelemetryAttributes.CommandId] = commandId,
                [TelemetryAttributes.Surface] = "diagnostics"
            });
    }
}
