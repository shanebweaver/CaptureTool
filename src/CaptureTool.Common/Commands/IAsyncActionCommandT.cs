namespace CaptureTool.Common.Commands;

public interface IAsyncActionCommand<T>
{
    bool CanExecute(T parameter);
    Task ExecuteAsync(T parameter, CancellationToken cancellationToken = default);
}