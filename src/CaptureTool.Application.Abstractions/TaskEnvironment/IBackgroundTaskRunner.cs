namespace CaptureTool.Application.Abstractions.TaskEnvironment;

public interface IBackgroundTaskRunner
{
    void Run(Action action, string failureMessage);
}
