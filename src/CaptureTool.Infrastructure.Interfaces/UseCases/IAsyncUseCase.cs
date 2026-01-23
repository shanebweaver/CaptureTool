namespace CaptureTool.Infrastructure.Interfaces.UseCases;

public interface IAsyncUseCase
{
    bool CanExecute();
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}