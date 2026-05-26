using CaptureTool.Application.Abstractions.Diagnostics;
using CaptureTool.Infrastructure.Abstractions.Logging;
using CaptureTool.Infrastructure.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class DiagnosticsViewModel : ViewModelBase
{
    private readonly IClearLogsAppCommand _clearLogsAppCommand;
    private readonly IUpdateLoggingStateAppCommand _updateLoggingStateAppCommand;
    private readonly ILogService _logService;

    public IRelayCommand ClearLogsCommand { get; }
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
        IClearLogsAppCommand clearLogsCommand,
        IUpdateLoggingStateAppCommand updateLoggingEnablementCommand,
        IGetIsLoggingEnabledAppQuery getIsLoggingEnabledQuery,
        IGetCurrentLogsAppQuery getCurrentLogsQuery,
        ILogService logService)
    {
        _clearLogsAppCommand = clearLogsCommand;
        _updateLoggingStateAppCommand = updateLoggingEnablementCommand;

        _logService = logService;
        _logService.LogAdded += OnLogAdded;

        ClearLogsCommand = new RelayCommand(ClearLogs);
        UpdateLoggingEnablementCommand = new AsyncRelayCommand<bool>(UpdateLoggingEnablementAsync);

        IsLoggingEnabled = getIsLoggingEnabledQuery.Execute();
        Logs = string.Join(Environment.NewLine, getCurrentLogsQuery.Execute().Select(log => log.ToString()));
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
        await _updateLoggingStateAppCommand.ExecuteAsync(newValue, CancellationToken.None);
    }

    private void ClearLogs()
    {
        Logs = string.Empty;
        _clearLogsAppCommand.Execute();
    }
}
