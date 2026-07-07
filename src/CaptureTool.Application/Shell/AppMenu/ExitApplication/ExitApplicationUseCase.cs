using CaptureTool.Application.Abstractions.Shell.AppMenu.ExitApplication;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Shell.AppMenu.ExitApplication;

internal sealed class ExitApplicationUseCase : IExitApplicationUseCase
{
    private const string ActivityId = "ExitApplication";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IShutdownHandler _shutdownHandler;

    public ExitApplicationUseCase(IShutdownHandler shutdownHandler,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _shutdownHandler = shutdownHandler;
    }

    public bool CanExecute(ExitApplicationRequest request)
    {
        bool result = !_shutdownHandler.IsShuttingDown;
        return result;
    }

    public Task<UseCaseResponse<ExitApplicationResponse>> ExecuteAsync(ExitApplicationRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _shutdownHandler.Shutdown();
                return new ExitApplicationResponse();
            },
            cancellationToken: cancellationToken);
    }
}
