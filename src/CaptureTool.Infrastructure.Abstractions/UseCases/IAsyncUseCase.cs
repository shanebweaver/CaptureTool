namespace CaptureTool.Infrastructure.Abstractions.UseCases;

public interface IAsyncUseCase
{
    bool CanExecute();
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}