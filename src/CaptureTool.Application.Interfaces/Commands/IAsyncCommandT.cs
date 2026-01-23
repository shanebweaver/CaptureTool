namespace CaptureTool.Application.Interfaces.Commands;

public interface IAsyncCommand<in T> : IAsyncCommand
{
    Task ExecuteAsync(T parameter);
}
