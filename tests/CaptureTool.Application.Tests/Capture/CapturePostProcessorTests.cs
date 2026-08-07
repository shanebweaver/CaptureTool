using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Capture;
using CaptureTool.Application.Capture.Audio;
using CaptureTool.Application.Capture.Image;
using CaptureTool.Application.Capture.Video;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using Moq;

namespace CaptureTool.Application.Tests.Capture;

[TestClass]
public sealed class CapturePostProcessorTests
{
    [TestMethod]
    public void ImageAutoSave_CopiesWithoutOverwrite()
    {
        const string SourcePath = @"C:\Temp\capture.png";
        const string DestinationFolder = @"C:\Captures";
        var fileSystem = new Mock<IFileSystem>();
        Mock<ISettingsService> settings = CreateSettings(
            CaptureToolSettings.Settings_ImageCapture_AutoSave,
            CaptureToolSettings.Settings_ImageCapture_AutoCopy,
            CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder,
            DestinationFolder);
        CaptureId captureId = CaptureId.New();
        var lifecycle = new RecordingCaptureAssetLifecycleService
        {
            FinalizedCaptureId = captureId,
        };
        ImageCapturePostProcessor processor = new(
            Mock.Of<IClipboardService>(),
            new CaptureFileAllocator(fileSystem.Object),
            settings.Object,
            Mock.Of<IStorageService>(),
            CreateImmediateTaskEnvironment().Object,
            Mock.Of<ILogService>(),
            new ImageCaptureFileNameGenerator(TestClock.Instance),
            lifecycle);

        processor.Process(new ImageFile(SourcePath));

        fileSystem.Verify(
            service => service.CopyFile(
                SourcePath,
                It.Is<string>(path => path.StartsWith(DestinationFolder, StringComparison.Ordinal) && path.EndsWith(".png", StringComparison.Ordinal)),
                false),
            Times.Once);
        Assert.HasCount(1, lifecycle.Finalizations);
        Assert.AreEqual((SourcePath, CaptureFileType.Image), lifecycle.Finalizations[0]);
        Assert.HasCount(1, lifecycle.PreferredOpenPathChanges);
        var preferredPathChange = lifecycle.PreferredOpenPathChanges[0];
        Assert.AreEqual(captureId, preferredPathChange.CaptureId);
        Assert.AreEqual(SourcePath, preferredPathChange.RetainedSourcePath);
        StringAssert.StartsWith(preferredPathChange.PreferredOpenPath, DestinationFolder);
        StringAssert.EndsWith(preferredPathChange.PreferredOpenPath, ".png");
    }

    [TestMethod]
    public void VideoAutoSave_CopiesWithoutOverwrite()
    {
        const string SourcePath = @"C:\Temp\capture.mp4";
        const string DestinationFolder = @"C:\Captures";
        var fileSystem = new Mock<IFileSystem>();
        Mock<ISettingsService> settings = CreateSettings(
            CaptureToolSettings.Settings_VideoCapture_AutoSave,
            CaptureToolSettings.Settings_VideoCapture_AutoCopy,
            CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder,
            DestinationFolder);
        CaptureId captureId = CaptureId.New();
        var lifecycle = new RecordingCaptureAssetLifecycleService
        {
            FinalizedCaptureId = captureId,
        };
        VideoCapturePostProcessor processor = new(
            Mock.Of<IClipboardService>(),
            new CaptureFileAllocator(fileSystem.Object),
            settings.Object,
            Mock.Of<IStorageService>(),
            CreateImmediateTaskEnvironment().Object,
            Mock.Of<ILogService>(),
            new VideoCaptureFileNameGenerator(TestClock.Instance),
            lifecycle);

        processor.Process(new VideoFile(SourcePath));

        fileSystem.Verify(
            service => service.CopyFile(
                SourcePath,
                It.Is<string>(path => path.StartsWith(DestinationFolder, StringComparison.Ordinal) && path.EndsWith(".mp4", StringComparison.Ordinal)),
                false),
            Times.Once);
        Assert.HasCount(1, lifecycle.Finalizations);
        Assert.AreEqual((SourcePath, CaptureFileType.Video), lifecycle.Finalizations[0]);
        Assert.HasCount(1, lifecycle.PreferredOpenPathChanges);
        var preferredPathChange = lifecycle.PreferredOpenPathChanges[0];
        Assert.AreEqual(captureId, preferredPathChange.CaptureId);
        Assert.AreEqual(SourcePath, preferredPathChange.RetainedSourcePath);
        StringAssert.StartsWith(preferredPathChange.PreferredOpenPath, DestinationFolder);
        StringAssert.EndsWith(preferredPathChange.PreferredOpenPath, ".mp4");
    }

    [TestMethod]
    public void AudioAutoSave_CopiesWithoutOverwrite()
    {
        const string SourcePath = @"C:\Temp\capture.wav";
        const string DestinationFolder = @"C:\Captures";
        var fileSystem = new Mock<IFileSystem>();
        Mock<ISettingsService> settings = CreateSettings(
            CaptureToolSettings.Settings_AudioCapture_AutoSave,
            CaptureToolSettings.Settings_AudioCapture_AutoCopy,
            CaptureToolSettings.Settings_AudioCapture_AutoSaveFolder,
            DestinationFolder);
        CaptureId captureId = CaptureId.New();
        var lifecycle = new RecordingCaptureAssetLifecycleService
        {
            FinalizedCaptureId = captureId,
        };
        AudioCapturePostProcessor processor = new(
            Mock.Of<IClipboardService>(),
            new CaptureFileAllocator(fileSystem.Object),
            settings.Object,
            Mock.Of<IStorageService>(),
            CreateImmediateTaskEnvironment().Object,
            Mock.Of<ILogService>(),
            new AudioCaptureFileNameGenerator(TestClock.Instance),
            lifecycle);

        processor.Process(new AudioFile(SourcePath));

        fileSystem.Verify(
            service => service.CopyFile(
                SourcePath,
                It.Is<string>(path => path.StartsWith(DestinationFolder, StringComparison.Ordinal) && path.EndsWith(".wav", StringComparison.Ordinal)),
                false),
            Times.Once);
        Assert.HasCount(1, lifecycle.Finalizations);
        Assert.AreEqual((SourcePath, CaptureFileType.Audio), lifecycle.Finalizations[0]);
        Assert.HasCount(1, lifecycle.PreferredOpenPathChanges);
        var preferredPathChange = lifecycle.PreferredOpenPathChanges[0];
        Assert.AreEqual(captureId, preferredPathChange.CaptureId);
        Assert.AreEqual(SourcePath, preferredPathChange.RetainedSourcePath);
        StringAssert.StartsWith(preferredPathChange.PreferredOpenPath, DestinationFolder);
        StringAssert.EndsWith(preferredPathChange.PreferredOpenPath, ".wav");
    }

    private static Mock<ISettingsService> CreateSettings(
        IBoolSettingDefinition autoSaveSetting,
        IBoolSettingDefinition autoCopySetting,
        IStringSettingDefinition folderSetting,
        string destinationFolder)
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.Get(autoSaveSetting)).Returns(true);
        settings.Setup(service => service.Get(autoCopySetting)).Returns(false);
        settings.Setup(service => service.Get(folderSetting)).Returns(destinationFolder);
        return settings;
    }

    private static Mock<ITaskEnvironment> CreateImmediateTaskEnvironment()
    {
        var taskEnvironment = new Mock<ITaskEnvironment>();
        taskEnvironment
            .Setup(environment => environment.TryExecute(It.IsAny<Action>()))
            .Callback<Action>(action => action())
            .Returns(true);
        return taskEnvironment;
    }
}
