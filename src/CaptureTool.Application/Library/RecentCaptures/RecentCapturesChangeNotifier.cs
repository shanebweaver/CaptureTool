using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.TaskEnvironment;

namespace CaptureTool.Application.Library.RecentCaptures;

internal sealed class RecentCapturesChangeNotifier : IRecentCapturesChangeNotifier
{
    private readonly ITaskEnvironment _taskEnvironment;

    public RecentCapturesChangeNotifier(ITaskEnvironment taskEnvironment)
    {
        _taskEnvironment = taskEnvironment;
    }

    public event EventHandler? RecentCapturesChanged;

    public void NotifyRecentCapturesChanged()
    {
        _taskEnvironment.TryExecute(() =>
            RecentCapturesChanged?.Invoke(this, EventArgs.Empty));
    }
}
