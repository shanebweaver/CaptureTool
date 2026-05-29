using CaptureTool.Application.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Shutdown;

namespace CaptureTool.Application.Features.Error.RestartApplication;

public sealed class RestartApplicationUseCase : IUseCase<RestartApplicationRequest, RestartApplicationResponse>, IConditional<RestartApplicationRequest>
{
    private readonly IShutdownHandler _shutdownHandler;

    public RestartApplicationUseCase(IShutdownHandler shutdownHandler)
    {
        _shutdownHandler = shutdownHandler;
    }

    public Task<bool> CanExecuteAsync(RestartApplicationRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!_shutdownHandler.IsShuttingDown);
    }

    public Task<RestartApplicationResponse> ExecuteAsync(RestartApplicationRequest request, CancellationToken cancellationToken = default)
    {
        _shutdownHandler.TryRestart();
        return Task.FromResult(new RestartApplicationResponse());
    }
}