using CaptureTool.Application.Implementations.ViewModels.Helpers;
using CaptureTool.Application.Interfaces.UseCases.Diagnostics;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Infrastructure.Implementations.ViewModels;
using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.Logging;
using CaptureTool.Infrastructure.Interfaces.Telemetry;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class DiagnosticsViewModel : ViewModelBase, IDiagnosticsViewModel
{
    public readonly struct ActivityIds
    {
        public static readonly string ClearLogs = "ClearLogs";
        public static readonly string UpdateLoggingEnablement = "UpdateLoggingEnablement";
    }

    private const string TelemetryContext = "Diagnostics";

    private readonly IDiagnosticsUseCases _diagnosticsActions;
    private readonly ILogService _logService;
    private readonly ITelemetryService _telemetryService;

    public IAppCommand ClearLogsCommand { get; }
    public IAsyncAppCommand<bool> UpdateLoggingEnablementCommand { get; }

    public string Logs
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool IsLoggingEnabled
    {
        get => field;
        private set => Set(ref field, value);
    }

    public DiagnosticsViewModel(
        IDiagnosticsUseCases diagnosticsActions,
        ILogService logService,
        ITelemetryService telemetryService)
    {
        _diagnosticsActions = diagnosticsActions;
        _logService = logService;
        _telemetryService = telemetryService;

        _logService.LogAdded += OnLogAdded;

        TelemetryAppCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        ClearLogsCommand = commandFactory.Create(ActivityIds.ClearLogs, ClearLogs);
        UpdateLoggingEnablementCommand = commandFactory.CreateAsync<bool>(ActivityIds.UpdateLoggingEnablement, UpdateLoggingEnablementAsync);

        IsLoggingEnabled = _diagnosticsActions.IsLoggingEnabled();
        Logs = string.Join(Environment.NewLine, _diagnosticsActions.GetCurrentLogs().Select(log => log.ToString()));
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
        await _diagnosticsActions.UpdateLoggingStateAsync(newValue, CancellationToken.None);
    }

    private void ClearLogs()
    {
        Logs = string.Empty;
        _diagnosticsActions.ClearLogs();
    }
}
