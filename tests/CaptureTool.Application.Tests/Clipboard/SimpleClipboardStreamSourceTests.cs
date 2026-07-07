using CaptureTool.Application.Abstractions.Clipboard;

namespace CaptureTool.Application.Tests.Clipboard;

[TestClass]
public sealed class SimpleClipboardStreamSourceTests
{
    [TestMethod]
    public void GetStream_ReturnsOriginalStream()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var source = new SimpleClipboardStreamSource(stream);

        Stream result = source.GetStream();

        Assert.AreSame(stream, result);
        Assert.AreEqual(0, result.Position);
    }

    [TestMethod]
    public void ClipboardFile_StoresFilePath()
    {
        var file = new ClipboardFile(@"C:\Temp\capture.png");

        Assert.AreEqual(@"C:\Temp\capture.png", file.FilePath);
    }
}
