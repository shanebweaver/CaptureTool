using CaptureTool.Application.Abstractions.Features.AppMenu.ExitApplication;
using CaptureTool.Application.Abstractions.Shutdown;

namespace CaptureTool.Application.Features.AppMenu.ExitApplication;

public sealed class ExitApplicationUseCase : IExitApplicationUseCase
{
    private readonly IShutdownHandler _shutdownHandler;

    public ExitApplicationUseCase(IShutdownHandler shutdownHandler)
    {
        _shutdownHandler = shutdownHandler;
    }

    public bool CanExecute(ExitApplicationRequest request)
    {
        bool result = !_shutdownHandler.IsShuttingDown;
        return result;
    }

    public Task<ExitApplicationResponse> ExecuteAsync(ExitApplicationRequest request, CancellationToken cancellationToken = default)
    {
        _shutdownHandler.Shutdown();
        return Task.FromResult(new ExitApplicationResponse());
    }
}
