namespace CaptureTool.Common.Commands;

public interface IAsyncCommand<in T> : IAsyncCommand
{
    Task ExecuteAsync(T parameter);
}
