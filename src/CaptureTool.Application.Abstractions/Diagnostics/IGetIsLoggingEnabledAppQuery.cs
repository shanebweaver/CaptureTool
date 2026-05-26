using CaptureTool.Infrastructure.Abstractions.Queries;

namespace CaptureTool.Application.Abstractions.Diagnostics;

public interface IGetIsLoggingEnabledAppQuery : IAppQuery<bool>
{
}
