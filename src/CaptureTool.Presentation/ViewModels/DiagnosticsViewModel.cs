using CaptureTool.Application.Abstractions;
using CaptureTool.Application.Features.Diagnostics.ClearLogs;
using CaptureTool.Application.Features.Diagnostics.GetCurrentLogs;
using CaptureTool.Application.Features.Diagnostics.GetIsLoggingEnabled;
using CaptureTool.Application.Features.Diagnostics.UpdateLoggingState;
using CaptureTool.Infrastructure.Abstractions.Logging;
using CaptureTool.Infrastructure.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class DiagnosticsViewModel : ViewModelBase
{
    private readonly IUseCase<ClearLogsRequest, ClearLogsResponse> _clearLogsCommand;
    private readonly IUseCase<UpdateLoggingStateRequest, UpdateLoggingStateResponse> _updateLoggingStateCommand;
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
        IUseCase<ClearLogsRequest, ClearLogsResponse> clearLogsCommand,
        IUseCase<UpdateLoggingStateRequest, UpdateLoggingStateResponse> updateLoggingEnablementCommand,
        IUseCase<GetIsLoggingEnabledRequest, GetIsLoggingEnabledResponse> getIsLoggingEnabledQuery,
        IUseCase<GetCurrentLogsRequest, GetCurrentLogsResponse> getCurrentLogsQuery,
        ILogService logService)
    {
        _clearLogsCommand = clearLogsCommand;
        _updateLoggingStateCommand = updateLoggingEnablementCommand;

        _logService = logService;
        _logService.LogAdded += OnLogAdded;

        ClearLogsCommand = new RelayCommand(ClearLogs);
        UpdateLoggingEnablementCommand = new AsyncRelayCommand<bool>(UpdateLoggingEnablementAsync);

        IsLoggingEnabled = getIsLoggingEnabledQuery.ExecuteAsync(new GetIsLoggingEnabledRequest()).GetAwaiter().GetResult().IsEnabled;
        Logs = string.Join(Environment.NewLine, getCurrentLogsQuery.ExecuteAsync(new GetCurrentLogsRequest()).GetAwaiter().GetResult().Logs.Select(log => log.ToString()));
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
    }

    private void ClearLogs()
    {
        Logs = string.Empty;
        _clearLogsCommand.ExecuteAsync(new ClearLogsRequest()).GetAwaiter().GetResult();
    }
}
