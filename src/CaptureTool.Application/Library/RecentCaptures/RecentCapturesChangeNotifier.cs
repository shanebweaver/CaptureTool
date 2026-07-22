using CaptureTool.Application.Abstractions.Library.RecentCaptures;

namespace CaptureTool.Application.Library.RecentCaptures;

internal sealed class RecentCapturesChangeNotifier : IRecentCapturesChangeNotifier
{
    public event EventHandler? RecentCapturesChanged;

    public void NotifyRecentCapturesChanged()
    {
        RecentCapturesChanged?.Invoke(this, EventArgs.Empty);
    }
}
