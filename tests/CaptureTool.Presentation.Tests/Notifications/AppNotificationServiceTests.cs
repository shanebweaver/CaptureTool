using CaptureTool.Presentation.Notifications;
using FluentAssertions;

namespace CaptureTool.Presentation.Tests.Notifications;

[TestClass]
public sealed class AppNotificationServiceTests
{
    [TestMethod]
    public void ShowError_ShouldPushNewestErrorToTopOfStack()
    {
        var service = new AppNotificationService();

        service.ShowError("First error");
        service.ShowError("Second error");

        service.HasNotification.Should().BeTrue();
        service.NotificationCount.Should().Be(2);
        service.CurrentNotification.Should().NotBeNull();
        service.CurrentNotification!.Kind.Should().Be(AppNotificationKind.Error);
        service.CurrentNotification.Message.Should().Be("Second error");
    }

    [TestMethod]
    public void ShowInfo_ShouldPushInfoNotification()
    {
        var service = new AppNotificationService();

        service.ShowInfo("Saved");

        service.HasNotification.Should().BeTrue();
        service.NotificationCount.Should().Be(1);
        service.CurrentNotification.Should().NotBeNull();
        service.CurrentNotification!.Kind.Should().Be(AppNotificationKind.Info);
        service.CurrentNotification.Message.Should().Be("Saved");
    }

    [TestMethod]
    public void DismissCurrent_ShouldPopCurrentErrorAndRevealNext()
    {
        var service = new AppNotificationService();
        service.ShowError("First error");
        service.ShowError("Second error");

        service.DismissCurrent();

        service.HasNotification.Should().BeTrue();
        service.NotificationCount.Should().Be(1);
        service.CurrentNotification.Should().NotBeNull();
        service.CurrentNotification!.Message.Should().Be("First error");

        service.DismissCurrent();

        service.HasNotification.Should().BeFalse();
        service.NotificationCount.Should().Be(0);
        service.CurrentNotification.Should().BeNull();
    }

    [TestMethod]
    public void ShowError_ShouldIgnoreBlankMessages()
    {
        var service = new AppNotificationService();

        service.ShowError("   ");

        service.HasNotification.Should().BeFalse();
        service.NotificationCount.Should().Be(0);
    }
}
