using System.Windows.Input;

namespace CaptureTool.Application.Interfaces.Commands;

public interface IAsyncCommand : ICommand
{
    Task ExecuteAsync(object? parameter);
}
