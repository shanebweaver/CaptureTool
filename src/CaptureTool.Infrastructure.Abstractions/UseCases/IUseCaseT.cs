namespace CaptureTool.Infrastructure.Abstractions.UseCases;

public interface IUseCase<T>
{
    bool CanExecute(T parameter);
    void Execute(T parameter);
}