using CaptureTool.Application.Abstractions.Clipboard;

namespace CaptureTool.Application.Tests.Clipboard;

[TestClass]
public sealed class ClipboardFileTests
{
    [TestMethod]
    public void ClipboardFile_StoresFilePath()
    {
        var file = new ClipboardFile(@"C:\Temp\capture.png");

        Assert.AreEqual(@"C:\Temp\capture.png", file.FilePath);
    }
}
