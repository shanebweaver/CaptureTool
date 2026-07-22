namespace CaptureTool.Application.Abstractions.Library.RecentCaptures;

public interface IRecentCapturesChangeNotifier
{
    event EventHandler? RecentCapturesChanged;

    void NotifyRecentCapturesChanged();
}
