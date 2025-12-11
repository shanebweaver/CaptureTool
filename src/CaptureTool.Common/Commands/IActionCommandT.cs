namespace CaptureTool.Common.Commands;

public interface IActionCommand<T>
{
    bool CanExecute(T parameter);
    void Execute(T parameter);
}