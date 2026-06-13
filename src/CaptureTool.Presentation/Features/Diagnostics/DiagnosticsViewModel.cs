using CaptureTool.Application.Abstractions.Features.Diagnostics.ClearLogs;
using CaptureTool.Application.Abstractions.Features.Diagnostics.ExportLogs;
using CaptureTool.Application.Abstractions.Features.Diagnostics.GetCurrentLogs;
using CaptureTool.Application.Abstractions.Features.Diagnostics.GetIsLoggingEnabled;
using CaptureTool.Application.Abstractions.Features.Diagnostics.UpdateLoggingState;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Telemetry;
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
    private readonly ITelemetryService _telemetryService;

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
        ITelemetryService telemetryService)
    {
        _clearLogsCommand = clearLogsCommand;
        _exportLogsCommand = exportLogsCommand;
        _updateLoggingStateCommand = updateLoggingEnablementCommand;
        _getIsLoggingEnabledQuery = getIsLoggingEnabledQuery;
        _getCurrentLogsQuery = getCurrentLogsQuery;

        _logService = logService;
        _telemetryService = telemetryService;
        _logService.LogAdded += OnLogAdded;

        ClearLogsCommand = new AsyncRelayCommand(ClearLogsAsync);
        ExportLogsCommand = new AsyncRelayCommand(ExportLogsAsync);
        UpdateLoggingEnablementCommand = new AsyncRelayCommand<bool>(UpdateLoggingEnablementAsync);

        IsLoggingEnabled = false;
        Logs = string.Empty;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            IsLoggingEnabled = (await _getIsLoggingEnabledQuery.ExecuteAsync(new GetIsLoggingEnabledRequest(), CancellationToken.None)).IsEnabled;
            Logs = string.Join(Environment.NewLine, (await _getCurrentLogsQuery.ExecuteAsync(new GetCurrentLogsRequest(), CancellationToken.None)).Logs.Select(log => log.ToString()));
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(DiagnosticsViewModel), exception);
            IsLoggingEnabled = false;
        }
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
        try
        {
            IsLoggingEnabled = newValue;
            await _updateLoggingStateCommand.ExecuteAsync(new UpdateLoggingStateRequest(newValue), CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(nameof(UpdateLoggingEnablementAsync), exception.Message);
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(UpdateLoggingEnablementAsync), exception);
        }
    }

    private async Task ClearLogsAsync()
    {
        try
        {
            Logs = string.Empty;
            await _clearLogsCommand.ExecuteAsync(new ClearLogsRequest(), CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(nameof(ClearLogsAsync), exception.Message);
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(ClearLogsAsync), exception);
        }
    }

    private async Task ExportLogsAsync()
    {
        try
        {
            await _exportLogsCommand.ExecuteAsync(new ExportLogsRequest(), CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(nameof(ExportLogsAsync), exception.Message);
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(ExportLogsAsync), exception);
        }
    }
}
