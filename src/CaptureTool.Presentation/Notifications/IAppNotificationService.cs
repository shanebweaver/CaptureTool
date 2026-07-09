using System.ComponentModel;

namespace CaptureTool.Presentation.Notifications;

public interface IAppNotificationService : INotifyPropertyChanged
{
    AppNotification? CurrentNotification { get; }

    bool HasNotification { get; }

    int NotificationCount { get; }

    void ShowError(string message);

    void DismissCurrent();
}
