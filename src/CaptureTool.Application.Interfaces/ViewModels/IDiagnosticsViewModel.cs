using System.Windows.Input;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IDiagnosticsViewModel
{
    ICommand ClearLogsCommand { get; }
    ICommand UpdateLoggingEnablementCommand { get; }
    string Logs { get; }
    bool IsLoggingEnabled { get; }
}
