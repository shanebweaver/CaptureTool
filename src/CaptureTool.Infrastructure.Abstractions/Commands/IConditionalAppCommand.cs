namespace CaptureTool.Infrastructure.Abstractions.Commands;

public interface IConditionalAppCommand : IAppCommand
{
    /// <summary>
    /// Determines whether the command can execute.
    /// </summary>
    /// <returns>True if the command can execute; otherwise, false.</returns>
    bool CanExecute();
}