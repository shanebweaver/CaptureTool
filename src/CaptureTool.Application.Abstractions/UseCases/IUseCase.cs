namespace CaptureTool.Application.Abstractions.UseCases;

public interface IUseCase<TRequest, TResponse>
{
    Task<TResponse> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default);
}
