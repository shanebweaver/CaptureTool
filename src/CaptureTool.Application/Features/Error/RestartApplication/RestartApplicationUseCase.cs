using CaptureTool.Application.Abstractions.Features.Error.RestartApplication;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.Error.RestartApplication;

public sealed class RestartApplicationUseCase : IRestartApplicationUseCase
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
                _shutdownHandler.TryRestart();
                return new RestartApplicationResponse();
            },
            cancellationToken: cancellationToken);
    }
}
