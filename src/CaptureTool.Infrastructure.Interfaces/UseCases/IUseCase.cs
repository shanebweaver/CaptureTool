namespace CaptureTool.Infrastructure.Interfaces.UseCases;

public interface IUseCase
{
    bool CanExecute();
    void Execute();
}