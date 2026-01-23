using System.Windows.Input;

namespace CaptureTool.Common.Commands;

public interface IAsyncCommand : ICommand
{
    Task ExecuteAsync(object? parameter);
}
