using FluentAssertions;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests;

[TestClass]
public sealed class AtomicImageFileWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WriteAsync_WhenStagingSucceeds_ReplacesDestinationAndRemovesTemporaryFile()
    {
        string directoryPath = CreateTestDirectory();
        string destinationPath = Path.Combine(directoryPath, "image.png");
        await File.WriteAllTextAsync(destinationPath, "old");
        string? temporaryFilePath = null;

        await AtomicImageFileWriter.WriteAsync(destinationPath, async path =>
        {
            temporaryFilePath = path;
            Path.GetExtension(path).Should().Be(".png");
            await File.WriteAllTextAsync(path, "new");
        });

        (await File.ReadAllTextAsync(destinationPath)).Should().Be("new");
        File.Exists(temporaryFilePath).Should().BeFalse();
    }

    [TestMethod]
    public async Task WriteAsync_WhenStagingFails_PreservesDestinationAndRemovesTemporaryFile()
    {
        string directoryPath = CreateTestDirectory();
        string destinationPath = Path.Combine(directoryPath, "image.png");
        await File.WriteAllTextAsync(destinationPath, "old");
        string? temporaryFilePath = null;

        Func<Task> action = () => AtomicImageFileWriter.WriteAsync(destinationPath, async path =>
        {
            temporaryFilePath = path;
            await File.WriteAllTextAsync(path, "incomplete");
            throw new IOException("The staged write failed.");
        });

        await action.Should().ThrowAsync<IOException>();
        (await File.ReadAllTextAsync(destinationPath)).Should().Be("old");
        File.Exists(temporaryFilePath).Should().BeFalse();
    }

    private string CreateTestDirectory()
    {
        string directoryPath = Path.Combine(TestContext.TestRunDirectory!, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }
}
