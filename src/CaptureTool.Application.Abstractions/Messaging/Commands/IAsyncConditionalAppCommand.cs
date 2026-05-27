namespace CaptureTool.Application.Abstractions.Messaging.Commands;

public interface IAsyncConditionalAppCommand : IAsyncAppCommand
{
    /// <summary>
    /// Determines whether the command can execute with the specified parameter.
    /// </summary>
    /// <param name="parameter">The parameter to evaluate.</param>
    /// <returns>True if the command can execute; otherwise, false.</returns>
    bool CanExecute();
}
