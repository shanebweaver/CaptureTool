namespace CaptureTool.Infrastructure.Abstractions.Commands;

/// <summary>
/// Represents a command that can be executed with a parameter of type <typeparamref name="TParameter"/>.
/// This is a platform-agnostic command interface that can be converted to platform-specific command types.
/// </summary>
/// <typeparam name="T">The type of the command parameter.</typeparam>
public interface IAppCommand<in TParameter> where TParameter : notnull
{
    /// <summary>
    /// Executes the command with the specified parameter.
    /// </summary>
    /// <param name="parameter">The command parameter.</param>
    void Execute(TParameter parameter);
}
