namespace CaptureTool.Presentation.Notifications;

public sealed record AppNotification(
    Guid Id,
    AppNotificationKind Kind,
    string Message);
