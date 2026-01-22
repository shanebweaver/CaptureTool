using CaptureTool.Common.Commands;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IDiagnosticsViewModel
{
    RelayCommand ClearLogsCommand { get; }
    AsyncRelayCommand<bool> UpdateLoggingEnablementCommand { get; }
    string Logs { get; }
    bool IsLoggingEnabled { get; }
}
