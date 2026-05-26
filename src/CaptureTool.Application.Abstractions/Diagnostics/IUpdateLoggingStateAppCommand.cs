using CaptureTool.Infrastructure.Abstractions.Commands;

namespace CaptureTool.Application.Abstractions.Diagnostics;

public interface IUpdateLoggingStateAppCommand : IAsyncAppCommand<bool>
{
}