using CaptureTool.Application.Abstractions.Shell.Error.RestartApplication;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Shell.Error.RestartApplication;

internal sealed class RestartApplicationUseCase : IRestartApplicationUseCase
{
    private const string ActivityId = "RestartApplication";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IShutdownHandler _shutdownHandler;

    public RestartApplicationUseCase(IShutdownHandler shutdownHandler,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _shutdownHandler = shutdownHandler;
    }

    public bool CanExecute(RestartApplicationRequest request)
    {
        return !_shutdownHandler.IsShuttingDown;
    }

    public Task<UseCaseResponse<RestartApplicationResponse>> ExecuteAsync(RestartApplicationRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                bool succeeded = _shutdownHandler.TryRestart();
                return new RestartApplicationResponse(succeeded);
            },
            cancellationToken: cancellationToken);
    }
}
