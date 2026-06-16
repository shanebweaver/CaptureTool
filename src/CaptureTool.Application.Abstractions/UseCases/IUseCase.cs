namespace CaptureTool.Application.Abstractions.UseCases;

public interface IUseCase<TRequest, TResponse>
{
    Task<UseCaseResponse<TResponse>> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default);
}
