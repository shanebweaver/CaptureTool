namespace CaptureTool.Common.Commands;

public interface IAsyncActionCommand
{
    bool CanExecute();
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}