using CaptureTool.Application.Abstractions.Telemetry;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Shared.Commands;

internal static class TelemetryCommandFactory
{
    public static IRelayCommand Relay(
        string action,
        Action execute,
        ITelemetryService? telemetryService,
        string surface,
        Func<bool>? canExecute = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentException.ThrowIfNullOrWhiteSpace(surface);

        return canExecute is null
            ? new RelayCommand(() => Execute(action, surface, execute, telemetryService))
            : new RelayCommand(() => Execute(action, surface, execute, telemetryService), canExecute);
    }

    public static IRelayCommand<T> Relay<T>(
        string action,
        Action<T?> execute,
        ITelemetryService? telemetryService,
        string surface)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentException.ThrowIfNullOrWhiteSpace(surface);

        return new RelayCommand<T>(
            parameter => Execute(action, surface, () => execute(parameter), telemetryService));
    }

    public static IAsyncRelayCommand Async(
        string action,
        Func<Task> execute,
        ITelemetryService? telemetryService,
        string surface,
        Func<bool>? canExecute = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentException.ThrowIfNullOrWhiteSpace(surface);

        return canExecute is null
            ? new AsyncRelayCommand(
                () => ExecuteAsync(action, surface, execute, telemetryService),
                AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler)
            : new AsyncRelayCommand(
                () => ExecuteAsync(action, surface, execute, telemetryService),
                canExecute,
                AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    public static IAsyncRelayCommand<T> Async<T>(
        string action,
        Func<T?, Task> execute,
        ITelemetryService? telemetryService,
        string surface)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentException.ThrowIfNullOrWhiteSpace(surface);

        return new AsyncRelayCommand<T>(
            parameter => ExecuteAsync(action, surface, () => execute(parameter), telemetryService),
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    private static void Execute(
        string action,
        string surface,
        Action execute,
        ITelemetryService? telemetryService)
    {
        Track(telemetryService, TelemetryEvents.UiCommandInvoked, action, surface);

        try
        {
            execute();
            Track(telemetryService, TelemetryEvents.UiCommandCompleted, action, surface, TelemetryOutcomes.Succeeded);
        }
        catch
        {
            Track(telemetryService, TelemetryEvents.UiCommandCompleted, action, surface, TelemetryOutcomes.Failed);
            throw;
        }
    }

    private static async Task ExecuteAsync(
        string action,
        string surface,
        Func<Task> execute,
        ITelemetryService? telemetryService)
    {
        Track(telemetryService, TelemetryEvents.UiCommandInvoked, action, surface);

        try
        {
            await execute();
            Track(telemetryService, TelemetryEvents.UiCommandCompleted, action, surface, TelemetryOutcomes.Succeeded);
        }
        catch (OperationCanceledException)
        {
            Track(telemetryService, TelemetryEvents.UiCommandCompleted, action, surface, TelemetryOutcomes.Canceled);
            throw;
        }
        catch
        {
            Track(telemetryService, TelemetryEvents.UiCommandCompleted, action, surface, TelemetryOutcomes.Failed);
            throw;
        }
    }

    private static void Track(
        ITelemetryService? telemetryService,
        string eventName,
        string action,
        string surface,
        string? outcome = null)
    {
        var properties = new Dictionary<string, object?>
        {
            [TelemetryProperties.Action] = action,
            [TelemetryProperties.Surface] = surface
        };

        if (outcome is not null)
        {
            properties[TelemetryProperties.Outcome] = outcome;
        }

        telemetryService?.TrackEvent(eventName, properties);
    }
}
