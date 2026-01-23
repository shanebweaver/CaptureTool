namespace CaptureTool.Infrastructure.Interfaces.UseCases;

public interface IAsyncUseCase<T>
{
    bool CanExecute(T parameter);
    Task ExecuteAsync(T parameter, CancellationToken cancellationToken = default);
}