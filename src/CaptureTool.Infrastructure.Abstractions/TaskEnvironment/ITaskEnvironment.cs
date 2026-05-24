namespace CaptureTool.Infrastructure.Abstractions.TaskEnvironment;

public interface ITaskEnvironment
{
    bool TryExecute(Action action);
}

