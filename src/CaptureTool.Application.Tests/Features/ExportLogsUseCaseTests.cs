using CaptureTool.Application.Abstractions.Features.Diagnostics.ExportLogs;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Features.Diagnostics.ExportLogs;
using CaptureTool.Domain.Capture.Files;
using Moq;

namespace CaptureTool.Application.Tests.Features;

[TestClass]
public sealed class ExportLogsUseCaseTests
{
    [TestMethod]
    public async Task ExecuteAsync_WhenFileSelected_ShouldWriteLogsToTextFile()
    {
        var filePicker = new Mock<IFilePickerService>();
        var logService = new Mock<ILogService>();
        string destinationPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");

        filePicker
            .Setup(service => service.PickSaveFileAsync(FilePickerType.Text, UserFolder.Documents))
            .ReturnsAsync(Mock.Of<IFile>(file => file.FilePath == destinationPath));
        logService
            .Setup(service => service.GetLogs())
            .Returns([
                new TestLogEntry(new DateTime(2026, 1, 1, 1, 2, 3), "First"),
                new TestLogEntry(new DateTime(2026, 1, 1, 4, 5, 6), "Second"),
            ]);

        try
        {
            var useCase = new ExportLogsUseCase(
                filePicker.Object,
                logService.Object,
                TestUseCaseExecutor.Instance);

            ExportLogsResponse? response = (await useCase.ExecuteAsync(new ExportLogsRequest(), TestContext.CancellationToken)).Value;

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Exported);
            Assert.AreEqual(
                $"01:02:03 - First{Environment.NewLine}04:05:06 - Second",
                await File.ReadAllTextAsync(destinationPath, TestContext.CancellationToken));
        }
        finally
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenPickerCanceled_ShouldDoNothing()
    {
        var filePicker = new Mock<IFilePickerService>();
        var logService = new Mock<ILogService>();
        var useCase = new ExportLogsUseCase(
            filePicker.Object,
            logService.Object,
            TestUseCaseExecutor.Instance);

        filePicker
            .Setup(service => service.PickSaveFileAsync(FilePickerType.Text, UserFolder.Documents))
            .ReturnsAsync((IFile?)null);

        ExportLogsResponse? response = (await useCase.ExecuteAsync(new ExportLogsRequest(), TestContext.CancellationToken)).Value;

        Assert.IsNotNull(response);
        Assert.IsFalse(response.Exported);
        logService.Verify(service => service.GetLogs(), Times.Never);
    }

    private sealed class TestLogEntry(DateTime timestamp, string message) : ILogEntry
    {
        public string Message { get; } = message;
        public DateTime Timestamp { get; } = timestamp;
        public override string ToString() => $"{Timestamp:HH:mm:ss} - {Message}";
    }

    public TestContext TestContext { get; set; } = null!;
}
