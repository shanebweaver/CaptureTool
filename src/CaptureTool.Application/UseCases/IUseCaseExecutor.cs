using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.UseCases;

public interface IUseCaseExecutor
{
    Task<UseCaseResponse<TResponse>> ExecuteAsync<TResponse>(
        string activityId,
        Func<CancellationToken, Task<TResponse>> useCase,
        CancellationToken cancellationToken = default);

    Task<UseCaseResponse<TResponse>> ExecuteAsync<TResponse>(
        string activityId,
        Func<TResponse> useCase,
        CancellationToken cancellationToken = default);
}
