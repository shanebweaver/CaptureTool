using System;
using Microsoft.UI.Dispatching;

namespace CaptureTool.Services.TaskEnvironment.WinUI;

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
