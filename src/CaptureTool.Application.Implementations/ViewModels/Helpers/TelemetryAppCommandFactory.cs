using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Implementations.Commands;
using CaptureTool.Infrastructure.Interfaces.Telemetry;

namespace CaptureTool.Application.Implementations.ViewModels.Helpers;

/// <summary>
/// Factory for creating platform-agnostic app commands that automatically wrap execution with telemetry tracking.
/// </summary>
public sealed class TelemetryAppCommandFactory
{
    private readonly ITelemetryService _telemetryService;
    private readonly string _context;

    public TelemetryAppCommandFactory(ITelemetryService telemetryService, string context)
    {
        _telemetryService = telemetryService;
        _context = context;
    }

    /// <summary>
    /// Creates an AppCommand that wraps the action with telemetry tracking.
    /// </summary>
    /// <param name="activityId">The activity identifier for telemetry tracking.</param>
    /// <param name="action">The action to execute.</param>
    /// <param name="canExecute">Optional function to determine if the command can execute.</param>
    /// <returns>An AppCommand with telemetry tracking.</returns>
    public IAppCommand Create(string activityId, Action action, Func<bool>? canExecute = null)
    {
        Action wrappedAction = () => TelemetryHelper.ExecuteActivity(_telemetryService, _context, activityId, action);
        return new AppCommand(wrappedAction, canExecute);
    }

    /// <summary>
    /// Creates a generic AppCommand that wraps the action with telemetry tracking.
    /// </summary>
    /// <typeparam name="T">The type of the command parameter.</typeparam>
    /// <param name="activityId">The activity identifier for telemetry tracking.</param>
    /// <param name="action">The action to execute with parameter.</param>
    /// <param name="canExecute">Optional predicate to determine if the command can execute.</param>
    /// <returns>An AppCommand with telemetry tracking.</returns>
    public IAppCommand<T> Create<T>(string activityId, Action<T?> action, Predicate<T?>? canExecute = null)
    {
        Action<T?> wrappedAction = (T? param) => TelemetryHelper.ExecuteActivity(_telemetryService, _context, activityId, () => action(param));
        return new AppCommand<T>(wrappedAction, canExecute);
    }

    /// <summary>
    /// Creates an AsyncAppCommand that wraps the async action with telemetry tracking.
    /// </summary>
    /// <param name="activityId">The activity identifier for telemetry tracking.</param>
    /// <param name="asyncAction">The async action to execute.</param>
    /// <param name="canExecute">Optional function to determine if the command can execute.</param>
    /// <returns>An AsyncAppCommand with telemetry tracking.</returns>
    public IAsyncAppCommand CreateAsync(string activityId, Func<Task> asyncAction, Func<bool>? canExecute = null)
    {
        Func<Task> wrappedAction = () => TelemetryHelper.ExecuteActivityAsync(_telemetryService, _context, activityId, asyncAction);
        return new AsyncAppCommand(wrappedAction, canExecute);
    }

    /// <summary>
    /// Creates a generic AsyncAppCommand that wraps the async action with telemetry tracking.
    /// </summary>
    /// <typeparam name="T">The type of the command parameter.</typeparam>
    /// <param name="activityId">The activity identifier for telemetry tracking.</param>
    /// <param name="asyncAction">The async action to execute with parameter.</param>
    /// <param name="canExecute">Optional predicate to determine if the command can execute.</param>
    /// <returns>An AsyncAppCommand with telemetry tracking.</returns>
    public IAsyncAppCommand<T> CreateAsync<T>(string activityId, Func<T?, Task> asyncAction, Predicate<T?>? canExecute = null)
    {
        Func<T?, Task> wrappedAction = (T? param) => TelemetryHelper.ExecuteActivityAsync(_telemetryService, _context, activityId, () => asyncAction(param));
        return new AsyncAppCommand<T>(wrappedAction, canExecute);
    }
}
