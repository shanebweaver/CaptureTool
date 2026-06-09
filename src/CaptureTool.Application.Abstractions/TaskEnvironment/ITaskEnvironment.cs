namespace CaptureTool.Application.Abstractions.TaskEnvironment;

public interface ITaskEnvironment
{
    bool TryExecute(Action action);
}

