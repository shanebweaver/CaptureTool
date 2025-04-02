using System;

namespace CaptureTool.Services.TaskEnvironment;

public interface ITaskEnvironment
{
    bool TryExecute(Action action);
}

