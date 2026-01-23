using CaptureTool.Common;
using CaptureTool.Infrastructure.Interfaces.Commands;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IDiagnosticsViewModel : IViewModel
{
    IAppCommand ClearLogsCommand { get; }
    IAsyncAppCommand<bool> UpdateLoggingEnablementCommand { get; }
    string Logs { get; }
    bool IsLoggingEnabled { get; }
}
