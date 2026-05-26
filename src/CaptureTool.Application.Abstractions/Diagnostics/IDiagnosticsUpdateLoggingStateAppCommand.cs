using CaptureTool.Infrastructure.Abstractions.Commands;

namespace CaptureTool.Application.Abstractions.Diagnostics;

public interface IDiagnosticsUpdateLoggingStateAppCommand : IAppCommand<bool>
{
}