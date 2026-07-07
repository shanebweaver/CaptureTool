using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Tests;

internal sealed class TestUseCaseExecutor : IUseCaseExecutor
{
    public static IUseCaseExecutor Instance { get; } = new TestUseCaseExecutor();

    private TestUseCaseExecutor()
    {
    }

    public async Task<UseCaseResponse<TResponse>> ExecuteAsync<TResponse>(
        string activityId,
        Func<CancellationToken, Task<TResponse>> useCase,
        CancellationToken cancellationToken = default)
    {
        return UseCaseResponse<TResponse>.Success(await useCase(cancellationToken));
    }

    public Task<UseCaseResponse<TResponse>> ExecuteAsync<TResponse>(
        string activityId,
        Func<TResponse> useCase,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UseCaseResponse<TResponse>.Success(useCase()));
    }
}
