using CaptureTool.Infrastructure.Abstractions.Queries;

namespace CaptureTool.Application.Abstractions.Diagnostics;

public interface IDiagnosticsIsLoggingEnabledAppCommand : IAppQuery<bool>
{
}
