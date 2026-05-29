namespace CaptureTool.Application.Abstractions;

public interface IConditional<TRequest>
{
    Task<bool> CanExecuteAsync(TRequest request, CancellationToken cancellationToken = default);
}
