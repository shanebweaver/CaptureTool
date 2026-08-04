using CaptureKit.Abstractions;
using Moq;
using System.Drawing;

namespace CaptureTool.Infrastructure.Capture.Windows.Tests;

[TestClass]
public sealed class WindowsScreenCaptureTests
{
    [TestMethod]
    public void SaveImageToFile_WhenDestinationIsReserved_ReplacesReservation()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"CaptureTool-{Guid.NewGuid():N}.png");

        try
        {
            File.Create(filePath).Dispose();
            using var image = new Bitmap(2, 2);
            var screenCapture = new WindowsScreenCapture(Mock.Of<IDisplayCaptureService>());

            screenCapture.SaveImageToFile(image, filePath);

            Assert.IsGreaterThan(0, new FileInfo(filePath).Length);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
