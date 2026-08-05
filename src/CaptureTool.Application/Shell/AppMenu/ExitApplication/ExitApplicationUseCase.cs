using CaptureTool.Application.Abstractions.Navigation;
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
    private readonly INavigationCoordinator _navigationCoordinator;

    public ExitApplicationUseCase(
        IShutdownHandler shutdownHandler,
        INavigationCoordinator navigationCoordinator,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _shutdownHandler = shutdownHandler;
        _navigationCoordinator = navigationCoordinator;
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
            useCase: async token =>
            {
                bool exited = await _navigationCoordinator.ExecuteTransitionAsync(
                    _ =>
                    {
                        _shutdownHandler.Shutdown();
                        return Task.FromResult(true);
                    },
                    token);
                return new ExitApplicationResponse(exited);
            },
            cancellationToken: cancellationToken);
    }
}
