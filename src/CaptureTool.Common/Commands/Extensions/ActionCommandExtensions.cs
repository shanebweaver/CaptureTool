namespace CaptureTool.Common.Commands.Extensions;

public static partial class ActionCommandExtensions
{
    public static void ExecuteCommand(this IActionCommand command)
    {
        if (!command.CanExecute())
        {
            throw new InvalidOperationException("Command cannot be invoked");
        }

        command.Execute();
    }

    public static async Task ExecuteCommandAsync(this IAsyncActionCommand asyncCommand)
    {
        if (!asyncCommand.CanExecute())
        {
            throw new InvalidOperationException("Command cannot be invoked");
        }

        await asyncCommand.ExecuteAsync();
    }

    public static void ExecuteCommand<T>(this IActionCommand<T> command, T parameter)
    {
        if (!command.CanExecute(parameter))
        {
            throw new InvalidOperationException("Command cannot be invoked");
        }

        command.Execute(parameter);
    }

    public static async Task ExecuteCommandAsync<T>(this IAsyncActionCommand<T> asyncCommand, T parameter)
    {
        if (!asyncCommand.CanExecute(parameter))
        {
            throw new InvalidOperationException("Command cannot be invoked");
        }

        await asyncCommand.ExecuteAsync(parameter);
    }
}
