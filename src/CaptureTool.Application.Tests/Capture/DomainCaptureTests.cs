using CaptureTool.Domain.Capture;
using System.Drawing;

namespace CaptureTool.Application.Tests.Capture;

[TestClass]
public sealed class DomainCaptureTests
{
    [TestMethod]
    public async Task PendingVideoFile_ShouldCompleteWhenReadyTask()
    {
        var file = new PendingVideoFile(@"C:\Captures\recording.mp4");

        Assert.AreEqual("recording.mp4", file.FileName);
        Assert.IsFalse(file.IsReady);

        file.Complete();

        await file.WhenReadyAsync();
        Assert.IsTrue(file.IsReady);
    }

    [TestMethod]
    public async Task PendingVideoFile_ShouldFaultWhenFinalizationFails()
    {
        var file = new PendingVideoFile("recording.mp4");
        var exception = new InvalidOperationException("failed");

        file.Fail(exception);

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(file.WhenReadyAsync);
        Assert.AreSame(exception, actual);
        Assert.IsTrue(file.IsReady);
    }

    [TestMethod]
    public void WindowInfo_ShouldExposeConstructorValues()
    {
        var position = new Rectangle(1, 2, 3, 4);

        var window = new WindowInfo(123, "Capture", position);

        Assert.AreEqual((nint)123, window.Handle);
        Assert.AreEqual("Capture", window.Title);
        Assert.AreEqual(position, window.Position);
    }
}
