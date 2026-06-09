using CaptureTool.Application.Abstractions.Features.Error.RestartApplication;
using CaptureTool.Application.Abstractions.Shutdown;

namespace CaptureTool.Application.Features.Error.RestartApplication;

public sealed class RestartApplicationUseCase : IRestartApplicationUseCase
{
    private readonly IShutdownHandler _shutdownHandler;

    public RestartApplicationUseCase(IShutdownHandler shutdownHandler)
    {
        _shutdownHandler = shutdownHandler;
    }

    public bool CanExecute(RestartApplicationRequest request)
    {
        return !_shutdownHandler.IsShuttingDown;
    }

    public Task<RestartApplicationResponse> ExecuteAsync(RestartApplicationRequest request, CancellationToken cancellationToken = default)
    {
        _shutdownHandler.TryRestart();
        return Task.FromResult(new RestartApplicationResponse());
    }
}