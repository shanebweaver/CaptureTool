using CaptureTool.Application.Abstractions.Telemetry;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Shared.Commands;

internal static class TelemetryCommandFactory
{
    public static IRelayCommand Relay(
        string commandId,
        Action execute,
        ITelemetryService? telemetryService,
        string surface,
        Func<bool>? canExecute = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentException.ThrowIfNullOrWhiteSpace(surface);

        return canExecute is null
            ? new RelayCommand(() => Execute(commandId, surface, execute, telemetryService))
            : new RelayCommand(() => Execute(commandId, surface, execute, telemetryService), canExecute);
    }

    public static IRelayCommand<T> Relay<T>(
        string commandId,
        Action<T?> execute,
        ITelemetryService? telemetryService,
        string surface)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentException.ThrowIfNullOrWhiteSpace(surface);

        return new RelayCommand<T>(parameter => Execute(commandId, surface, () => execute(parameter), telemetryService));
    }

    public static IAsyncRelayCommand Async(
        string commandId,
        Func<Task> execute,
        ITelemetryService? telemetryService,
        string surface,
        Func<bool>? canExecute = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentException.ThrowIfNullOrWhiteSpace(surface);

        return canExecute is null
            ? new AsyncRelayCommand(() => ExecuteAsync(commandId, surface, execute, telemetryService), AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler)
            : new AsyncRelayCommand(() => ExecuteAsync(commandId, surface, execute, telemetryService), canExecute, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    public static IAsyncRelayCommand<T> Async<T>(
        string commandId,
        Func<T?, Task> execute,
        ITelemetryService? telemetryService,
        string surface)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentException.ThrowIfNullOrWhiteSpace(surface);

        return new AsyncRelayCommand<T>(
            parameter => ExecuteAsync(commandId, surface, () => execute(parameter), telemetryService),
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    private static void Execute(
        string commandId,
        string surface,
        Action execute,
        ITelemetryService? telemetryService)
    {
        TrackInvoked(commandId, surface, telemetryService);

        try
        {
            execute();
        }
        catch (Exception exception)
        {
            TrackException(commandId, surface, exception, telemetryService);
            throw;
        }
    }

    private static async Task ExecuteAsync(
        string commandId,
        string surface,
        Func<Task> execute,
        ITelemetryService? telemetryService)
    {
        TrackInvoked(commandId, surface, telemetryService);

        try
        {
            await execute();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TrackException(commandId, surface, exception, telemetryService);
            throw;
        }
    }

    private static void TrackInvoked(
        string commandId,
        string surface,
        ITelemetryService? telemetryService)
    {
        telemetryService?.TrackEvent(
            TelemetryEvents.UiCommandInvoked,
            CreateAttributes(commandId, surface));
    }

    private static void TrackException(
        string commandId,
        string surface,
        Exception exception,
        ITelemetryService? telemetryService)
    {
        telemetryService?.TrackException(
            exception,
            new TelemetryExceptionContext(
                Component: "Command",
                ActivityId: commandId,
                Attributes: CreateAttributes(commandId, surface)));
    }

    private static Dictionary<string, object?> CreateAttributes(string commandId, string surface)
    {
        return new Dictionary<string, object?>
        {
            [TelemetryAttributes.CommandId] = commandId,
            [TelemetryAttributes.Surface] = surface
        };
    }
}
