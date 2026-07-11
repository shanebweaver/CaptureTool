using System.ComponentModel;

namespace CaptureTool.Presentation.Notifications;

public sealed class AppNotificationService : IAppNotificationService
{
    private readonly List<AppNotification> _notifications = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppNotification? CurrentNotification => _notifications.Count > 0
        ? _notifications[0]
        : null;

    public bool HasNotification => CurrentNotification is not null;

    public int NotificationCount => _notifications.Count;

    public void ShowError(string message) => Show(AppNotificationKind.Error, message);

    public void ShowInfo(string message) => Show(AppNotificationKind.Info, message);

    private void Show(AppNotificationKind kind, string message)
    {
        string trimmedMessage = message.Trim();
        if (string.IsNullOrWhiteSpace(trimmedMessage))
        {
            return;
        }

        _notifications.Insert(0, new AppNotification(
            Guid.NewGuid(),
            kind,
            trimmedMessage));

        RaiseNotificationStateChanged();
    }

    public void DismissCurrent()
    {
        if (_notifications.Count == 0)
        {
            return;
        }

        _notifications.RemoveAt(0);
        RaiseNotificationStateChanged();
    }

    private void RaiseNotificationStateChanged()
    {
        PropertyChanged?.Invoke(this, new(nameof(CurrentNotification)));
        PropertyChanged?.Invoke(this, new(nameof(HasNotification)));
        PropertyChanged?.Invoke(this, new(nameof(NotificationCount)));
    }
}
