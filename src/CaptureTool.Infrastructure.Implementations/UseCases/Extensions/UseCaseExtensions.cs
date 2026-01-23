namespace CaptureTool.Infrastructure.Implementations.UseCases.Extensions;

using CaptureTool.Infrastructure.Interfaces.UseCases;

public static partial class UseCaseExtensions
{
    public static void ExecuteCommand(this IUseCase command)
    {
        if (!command.CanExecute())
        {
            throw new InvalidOperationException("Command cannot be invoked");
        }

        command.Execute();
    }

    public static async Task ExecuteCommandAsync(this IAsyncUseCase asyncCommand, CancellationToken cancellationToken = default)
    {
        if (!asyncCommand.CanExecute())
        {
            throw new InvalidOperationException("Command cannot be invoked");
        }

        await asyncCommand.ExecuteAsync(cancellationToken);
    }

    public static void ExecuteCommand<T>(this IUseCase<T> command, T parameter)
    {
        if (!command.CanExecute(parameter))
        {
            throw new InvalidOperationException("Command cannot be invoked");
        }

        command.Execute(parameter);
    }

    public static async Task ExecuteCommandAsync<T>(this IAsyncUseCase<T> asyncCommand, T parameter, CancellationToken cancellationToken = default)
    {
        if (!asyncCommand.CanExecute(parameter))
        {
            throw new InvalidOperationException("Command cannot be invoked");
        }

        await asyncCommand.ExecuteAsync(parameter, cancellationToken);
    }
}
