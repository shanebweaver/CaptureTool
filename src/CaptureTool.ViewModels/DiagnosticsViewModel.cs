using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Diagnostics;
using CaptureTool.Infrastructure.Interfaces.Logging;

namespace CaptureTool.ViewModels;

public sealed partial class DiagnosticsViewModel : ViewModelBase
{
    private readonly IDiagnosticsActions _diagnosticsActions;
    private readonly ILogService _logService;

    public RelayCommand ClearLogsCommand { get; }
    public AsyncRelayCommand<bool> UpdateLoggingEnablementCommand { get; }

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
        IDiagnosticsActions diagnosticsActions,
        ILogService logService)
    {
        _diagnosticsActions = diagnosticsActions;
        _logService = logService;

        _logService.LogAdded += OnLogAdded;

        ClearLogsCommand = new(ClearLogs); 
        UpdateLoggingEnablementCommand = new(UpdateLoggingEnablementAsync);

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
