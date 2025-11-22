using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.Settings;
using CaptureTool.Services.Interfaces.Logging;
using CaptureTool.Services.Interfaces.Settings;

namespace CaptureTool.ViewModels;

public sealed partial class DiagnosticsViewModel : ViewModelBase
{
    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;

    public RelayCommand ClearLogsCommand { get; }

    public string Logs
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool IsLoggingEnabled
    {
        get => field;
        set
        {
            Set(ref field, value);
            OnIsLoggingEnabledChanged();
        }
    }

    public DiagnosticsViewModel(
        ILogService logService,
        ISettingsService settingsService)
    {
        _logService = logService;
        _settingsService = settingsService;

        _logService.LogAdded += OnLogAdded;

        ClearLogsCommand = new(ClearLogs); 
        IsLoggingEnabled = _logService.IsEnabled;
        Logs = string.Join(Environment.NewLine, _logService.GetLogs().Select(log => log.ToString()));
    }

    ~DiagnosticsViewModel()
    {
        _logService.LogAdded -= OnLogAdded;
    }

    private void OnLogAdded(object? sender, ILogEntry e)
    {
        Logs += e.ToString() + Environment.NewLine;
    }

    private void OnIsLoggingEnabledChanged()
    {
        if (IsLoggingEnabled)
        {
            _logService.Enable();
        }
        else
        {
            _logService.Disable();
        }

        _settingsService.Set(CaptureToolSettings.VerboseLogging, IsLoggingEnabled);
        _ = _settingsService.TrySaveAsync(CancellationToken.None);
    }

    private void ClearLogs()
    {
        Logs = string.Empty;
        _logService.ClearLogs();
    }
}
