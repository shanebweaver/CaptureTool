using CaptureTool.Common.Commands;
using System.Windows.Input;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IDiagnosticsViewModel
{
    ICommand ClearLogsCommand { get; }
    IAsyncCommand<bool> UpdateLoggingEnablementCommand { get; }
    string Logs { get; }
    bool IsLoggingEnabled { get; }
}
