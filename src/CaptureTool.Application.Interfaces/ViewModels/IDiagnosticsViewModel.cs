using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.ViewModels;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IDiagnosticsViewModel : IViewModel
{
    IAppCommand ClearLogsCommand { get; }
    IAsyncAppCommand<bool> UpdateLoggingEnablementCommand { get; }
    string Logs { get; }
    bool IsLoggingEnabled { get; }
}
