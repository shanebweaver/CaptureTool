namespace CaptureTool.Infrastructure.Interfaces.TaskEnvironment;

public interface ITaskEnvironment
{
    bool TryExecute(Action action);
}

