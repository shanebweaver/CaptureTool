namespace CaptureTool.Application.Abstractions.UseCases;

public interface IConditional<TRequest>
{
    bool CanExecute(TRequest request);
}
