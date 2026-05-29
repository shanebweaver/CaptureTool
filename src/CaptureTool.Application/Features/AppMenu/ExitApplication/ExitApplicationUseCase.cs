using CaptureTool.Application.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Shutdown;

namespace CaptureTool.Application.Features.AppMenu.ExitApplication;

public sealed class ExitApplicationUseCase : IUseCase<ExitApplicationRequest, ExitApplicationResponse>, IConditional<ExitApplicationRequest>
{
    private readonly IShutdownHandler _shutdownHandler;

    public ExitApplicationUseCase(IShutdownHandler shutdownHandler)
    {
        _shutdownHandler = shutdownHandler;
    }

    public Task<bool> CanExecuteAsync(ExitApplicationRequest request, CancellationToken cancellationToken = default)
    {
        bool result = !_shutdownHandler.IsShuttingDown;
        return Task.FromResult(result);
    }

    public Task<ExitApplicationResponse> ExecuteAsync(ExitApplicationRequest request, CancellationToken cancellationToken = default)
    {
        _shutdownHandler.Shutdown();
        return Task.FromResult(new ExitApplicationResponse());
    }
}
