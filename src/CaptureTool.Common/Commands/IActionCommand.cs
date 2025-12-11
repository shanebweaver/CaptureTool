namespace CaptureTool.Common.Commands;

public interface IActionCommand
{
    bool CanExecute();
    void Execute();
}