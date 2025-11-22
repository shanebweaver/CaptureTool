namespace CaptureTool.Services.Interfaces.TaskEnvironment;

public interface ITaskEnvironment
{
    bool TryExecute(Action action);
}

