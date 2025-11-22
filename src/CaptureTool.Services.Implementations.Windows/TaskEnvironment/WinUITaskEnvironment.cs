using CaptureTool.Services.Interfaces.TaskEnvironment;
using Microsoft.UI.Dispatching;

namespace CaptureTool.Services.Implementations.Windows.TaskEnvironment;

public class WinUITaskEnvironment : ITaskEnvironment
{
    private readonly DispatcherQueue _dispatcherQueue;

    public WinUITaskEnvironment(DispatcherQueue dispatcherQueue) 
    {
        _dispatcherQueue = dispatcherQueue;
    }

    public bool TryExecute(Action action)
    {
        return _dispatcherQueue.TryEnqueue(new DispatcherQueueHandler(action));
    }
}
