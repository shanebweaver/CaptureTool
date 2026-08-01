using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Library.RecentCaptures;
using Moq;

namespace CaptureTool.Application.Tests.Library;

[TestClass]
public sealed class RecentCapturesChangeNotifierTests
{
    [TestMethod]
    public void NotifyRecentCapturesChanged_ShouldDispatchNotificationToTaskEnvironment()
    {
        var taskEnvironment = new Mock<ITaskEnvironment>();
        taskEnvironment
            .Setup(environment => environment.TryExecute(It.IsAny<Action>()))
            .Callback<Action>(action => action())
            .Returns(true);
        var notifier = new RecentCapturesChangeNotifier(taskEnvironment.Object);
        int notificationCount = 0;
        notifier.RecentCapturesChanged += (_, _) => notificationCount++;

        notifier.NotifyRecentCapturesChanged();

        Assert.AreEqual(1, notificationCount);
        taskEnvironment.Verify(
            environment => environment.TryExecute(It.IsAny<Action>()),
            Times.Once);
    }
}
