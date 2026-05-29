namespace CaptureTool.Application.Abstractions;

public interface IUseCase<TRequest, TResponse>
{
    Task<TResponse> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default);
}
