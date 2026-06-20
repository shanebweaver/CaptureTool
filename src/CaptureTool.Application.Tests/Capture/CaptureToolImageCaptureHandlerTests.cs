using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Features.ImageCapture;
using CaptureTool.Application.Features.Settings;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;
using Moq;
using System.Drawing;
using System.Runtime.Versioning;

namespace CaptureTool.Application.Tests.Capture;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class CaptureToolImageCaptureHandlerTests
{
    [TestMethod]
    public void PerformAllScreensCapture_CombinesMonitorsSavesImageAndRaisesEvent()
    {
        string tempFolder = CreateTestFolder();
        var monitor = CreateMonitor();
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture.Setup(service => service.CaptureAllMonitors()).Returns([monitor]);
        screenCapture.Setup(service => service.CombineMonitors(It.IsAny<IList<MonitorCaptureResult>>())).Returns(new Bitmap(2, 2));
        var handler = CreateHandler(tempFolder, screenCapture: screenCapture.Object);
        IImageFile? captured = null;
        handler.NewImageCaptured += (_, file) => captured = file;

        ImageFile result = handler.PerformAllScreensCapture();

        Assert.IsNotNull(captured);
        Assert.AreEqual(result.FilePath, captured.FilePath);
        StringAssert.StartsWith(Path.GetFileName(result.FilePath), "Capture_");
        screenCapture.Verify(service => service.SaveImageToFile(It.IsAny<Image>(), result.FilePath), Times.Once);
    }

    [TestMethod]
    public void PerformImageCapture_CropsMonitorImageBeforeSaving()
    {
        string tempFolder = CreateTestFolder();
        var monitor = CreateMonitor();
        var area = new Rectangle(1, 2, 3, 4);
        using var bitmap = new Bitmap(5, 5);
        using var cropped = new Bitmap(3, 4);
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture.Setup(service => service.CreateBitmapFromMonitorCaptureResult(monitor)).Returns(bitmap);
        screenCapture.Setup(service => service.CreateCroppedBitmap(bitmap, area, monitor.Scale)).Returns(cropped);
        var handler = CreateHandler(tempFolder, screenCapture: screenCapture.Object);

        ImageFile result = handler.PerformImageCapture(new NewCaptureArgs(monitor, area));

        screenCapture.Verify(service => service.CreateCroppedBitmap(bitmap, area, monitor.Scale), Times.Once);
        screenCapture.Verify(service => service.SaveImageToFile(cropped, result.FilePath), Times.Once);
    }

    [TestMethod]
    public void PerformMultiMonitorImageCapture_WhenAutoSaveAndCopyEnabled_CopiesToClipboardAndSavesToConfiguredFolder()
    {
        string tempFolder = CreateTestFolder();
        string screenshotsFolder = CreateTestFolder();
        var clipboard = new Mock<IClipboardService>();
        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.Get(CaptureToolSettings.Settings_ImageCapture_AutoSave)).Returns(true);
        settings.Setup(service => service.Get(CaptureToolSettings.Settings_ImageCapture_AutoCopy)).Returns(true);
        settings.Setup(service => service.Get(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder)).Returns(screenshotsFolder);
        var screenCapture = new Mock<IScreenCapture>();
        screenCapture.Setup(service => service.CombineMonitors(It.IsAny<IList<MonitorCaptureResult>>())).Returns(new Bitmap(2, 2));
        screenCapture
            .Setup(service => service.SaveImageToFile(It.IsAny<Image>(), It.IsAny<string>()))
            .Callback<Image, string>((_, path) => File.WriteAllText(path, "image"));
        var handler = CreateHandler(
            tempFolder,
            clipboard.Object,
            settings.Object,
            screenCapture.Object);

        ImageFile result = handler.PerformMultiMonitorImageCapture([CreateMonitor()]);

        clipboard.Verify(service => service.CopyBitmapAsync(It.Is<ClipboardFile>(file => file.FilePath == result.FilePath)), Times.Once);
        string[] savedFiles = Directory.GetFiles(screenshotsFolder, "Capture_*.png");
        Assert.HasCount(1, savedFiles);
        Assert.AreEqual(savedFiles[0], result.AutoSavedFilePath);
    }

    private static CaptureToolImageCaptureHandler CreateHandler(
        string tempFolder,
        IClipboardService? clipboard = null,
        ISettingsService? settings = null,
        IScreenCapture? screenCapture = null)
    {
        var storage = new Mock<IStorageService>();
        storage.Setup(service => service.GetApplicationTemporaryFolderPath()).Returns(tempFolder);
        storage.Setup(service => service.GetSystemDefaultScreenshotsFolderPath()).Returns(tempFolder);

        var taskEnvironment = new Mock<ITaskEnvironment>();
        taskEnvironment
            .Setup(environment => environment.TryExecute(It.IsAny<Action>()))
            .Callback<Action>(action => action())
            .Returns(true);

        var defaultSettings = new Mock<ISettingsService>();
        defaultSettings.Setup(service => service.Get(CaptureToolSettings.Settings_ImageCapture_AutoSave)).Returns(false);
        defaultSettings.Setup(service => service.Get(CaptureToolSettings.Settings_ImageCapture_AutoCopy)).Returns(false);
        defaultSettings.Setup(service => service.Get(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder)).Returns("");

        return new CaptureToolImageCaptureHandler(
            clipboard ?? Mock.Of<IClipboardService>(),
            storage.Object,
            settings ?? defaultSettings.Object,
            screenCapture ?? Mock.Of<IScreenCapture>(),
            taskEnvironment.Object,
            Mock.Of<ITelemetryService>());
    }

    private static MonitorCaptureResult CreateMonitor() =>
        new(1, [], 96, new Rectangle(0, 0, 10, 10), new Rectangle(0, 0, 10, 10), true);

    private static string CreateTestFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
    }
}
