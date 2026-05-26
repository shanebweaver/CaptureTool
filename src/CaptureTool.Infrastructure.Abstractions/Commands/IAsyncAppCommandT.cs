namespace CaptureTool.Infrastructure.Abstractions.Commands;

/// <summary>
/// Represents an asynchronous command that can be executed with a parameter of type <typeparamref name="TParameter"/>.
/// This is a platform-agnostic command interface that can be converted to platform-specific command types.
/// </summary>
/// <typeparam name="T">The type of the command parameter.</typeparam>
public interface IAsyncAppCommand<in TParameter> where TParameter : notnull
{
    /// <summary>
    /// Executes the command asynchronously with the specified parameter.
    /// </summary>
    /// <param name="parameter">The command parameter.</param>
    Task ExecuteAsync(TParameter parameter, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a value indicating whether the command is currently executing.
    /// </summary>
    bool IsExecuting { get; }
}
