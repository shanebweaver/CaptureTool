using CaptureTool.Common.Commands;
using CaptureTool.Infrastructure.Interfaces.Telemetry;

namespace CaptureTool.Application.Implementations.ViewModels.Helpers;

/// <summary>
/// Factory for creating RelayCommand instances that automatically wrap execution with telemetry tracking.
/// </summary>
public sealed class TelemetryCommandFactory
{
    private readonly ITelemetryService _telemetryService;
    private readonly string _context;

    public TelemetryCommandFactory(ITelemetryService telemetryService, string context)
    {
        _telemetryService = telemetryService;
        _context = context;
    }

    /// <summary>
    /// Creates a RelayCommand that wraps the action with telemetry tracking.
    /// </summary>
    /// <param name="activityId">The activity identifier for telemetry tracking.</param>
    /// <param name="action">The action to execute.</param>
    /// <param name="canExecute">Optional function to determine if the command can execute.</param>
    /// <returns>A RelayCommand with telemetry tracking.</returns>
    public RelayCommand Create(string activityId, Action action, Func<bool>? canExecute = null)
    {
        return new RelayCommand(
            () => TelemetryHelper.ExecuteActivity(_telemetryService, _context, activityId, action),
            canExecute);
    }

    /// <summary>
    /// Creates a generic RelayCommand that wraps the action with telemetry tracking.
    /// </summary>
    /// <typeparam name="T">The type of the command parameter.</typeparam>
    /// <param name="activityId">The activity identifier for telemetry tracking.</param>
    /// <param name="action">The action to execute with parameter.</param>
    /// <param name="canExecute">Optional function to determine if the command can execute.</param>
    /// <returns>A RelayCommand with telemetry tracking.</returns>
    public RelayCommand<T> Create<T>(string activityId, Action<T?> action, Func<T, bool>? canExecute = null)
    {
        return new RelayCommand<T>(
            param => TelemetryHelper.ExecuteActivity(_telemetryService, _context, activityId, () => action(param)),
            canExecute);
    }

    /// <summary>
    /// Creates an AsyncRelayCommand that wraps the async action with telemetry tracking.
    /// </summary>
    /// <param name="activityId">The activity identifier for telemetry tracking.</param>
    /// <param name="asyncAction">The async action to execute.</param>
    /// <param name="canExecute">Optional function to determine if the command can execute.</param>
    /// <returns>An AsyncRelayCommand with telemetry tracking.</returns>
    public AsyncRelayCommand CreateAsync(string activityId, Func<Task> asyncAction, Func<bool>? canExecute = null)
    {
        return new AsyncRelayCommand(
            () => TelemetryHelper.ExecuteActivityAsync(_telemetryService, _context, activityId, asyncAction),
            canExecute);
    }

    /// <summary>
    /// Creates a generic AsyncRelayCommand that wraps the async action with telemetry tracking.
    /// </summary>
    /// <typeparam name="T">The type of the command parameter.</typeparam>
    /// <param name="activityId">The activity identifier for telemetry tracking.</param>
    /// <param name="asyncAction">The async action to execute with parameter.</param>
    /// <param name="canExecute">Optional function to determine if the command can execute.</param>
    /// <returns>An AsyncRelayCommand with telemetry tracking.</returns>
    public AsyncRelayCommand<T> CreateAsync<T>(string activityId, Func<T?, Task> asyncAction, Func<T, bool>? canExecute = null)
    {
        return new AsyncRelayCommand<T>(
            param => TelemetryHelper.ExecuteActivityAsync(_telemetryService, _context, activityId, () => asyncAction(param)),
            canExecute);
    }
}
