namespace CaptureTool.Infrastructure.Abstractions.UseCases;

public interface IUseCase
{
    bool CanExecute();
    void Execute();
}