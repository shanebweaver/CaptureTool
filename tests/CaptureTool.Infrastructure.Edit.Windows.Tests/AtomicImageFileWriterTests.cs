using FluentAssertions;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests;

[TestClass]
public sealed class AtomicImageFileWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WriteAsync_WhenWriteSucceeds_ReplacesDestinationWithoutTrailingBytes()
    {
        string directoryPath = CreateTestDirectory();
        string destinationPath = Path.Combine(directoryPath, "image.png");
        await File.WriteAllTextAsync(destinationPath, "old image with more bytes");

        await AtomicImageFileWriter.WriteAsync(destinationPath, stream => WriteTextAsync(stream, "new"));

        (await File.ReadAllTextAsync(destinationPath)).Should().Be("new");
        Directory.GetFiles(directoryPath).Should().Equal(destinationPath);
    }

    [TestMethod]
    public async Task WriteAsync_WhenWriteFails_PreservesDestinationAndOriginalException()
    {
        string directoryPath = CreateTestDirectory();
        string destinationPath = Path.Combine(directoryPath, "image.png");
        await File.WriteAllTextAsync(destinationPath, "old");
        var failure = new IOException("The image write failed.");

        Func<Task> action = () => AtomicImageFileWriter.WriteAsync(destinationPath, async stream =>
        {
            await WriteTextAsync(stream, "incomplete");
            throw failure;
        });

        (await action.Should().ThrowAsync<IOException>()).Which.Should().BeSameAs(failure);
        (await File.ReadAllTextAsync(destinationPath)).Should().Be("old");
        Directory.GetFiles(directoryPath).Should().Equal(destinationPath);

        // A failed write must also release the transaction and provider update deferral.
        await AtomicImageFileWriter.WriteAsync(destinationPath, stream => WriteTextAsync(stream, "retry"));
        (await File.ReadAllTextAsync(destinationPath)).Should().Be("retry");
    }

    [TestMethod]
    public async Task WriteAsync_WhenDestinationDirectoryDoesNotExist_CreatesImage()
    {
        string destinationPath = Path.Combine(CreateTestDirectory(), "new-folder", "image.png");

        await AtomicImageFileWriter.WriteAsync(destinationPath, stream => WriteTextAsync(stream, "new"));

        (await File.ReadAllTextAsync(destinationPath)).Should().Be("new");
    }

    [TestMethod]
    public async Task WriteAsync_WhenNewFileWriteFails_RemovesEmptyDestination()
    {
        string directoryPath = CreateTestDirectory();
        string destinationPath = Path.Combine(directoryPath, "image.png");
        Func<Task> action = () => AtomicImageFileWriter.WriteAsync(destinationPath, async stream =>
        {
            await WriteTextAsync(stream, "incomplete");
            throw new IOException("The image write failed.");
        });

        await action.Should().ThrowAsync<IOException>().WithMessage("The image write failed.");

        Directory.GetFiles(directoryPath).Should().BeEmpty();
    }

    [TestMethod]
    public async Task WriteAsync_WhenDestinationIsReadOnly_PreservesDestination()
    {
        string destinationPath = Path.Combine(CreateTestDirectory(), "image.png");
        await File.WriteAllTextAsync(destinationPath, "old");
        File.SetAttributes(destinationPath, FileAttributes.ReadOnly);
        try
        {
            Func<Task> action = () => AtomicImageFileWriter.WriteAsync(
                destinationPath, stream => WriteTextAsync(stream, "new"));

            await action.Should().ThrowAsync<UnauthorizedAccessException>();
            (await File.ReadAllTextAsync(destinationPath)).Should().Be("old");
        }
        finally
        {
            File.SetAttributes(destinationPath, FileAttributes.Normal);
        }
    }

    private static async Task WriteTextAsync(IRandomAccessStream stream, string text)
    {
        using DataWriter writer = new(stream);
        try
        {
            writer.WriteString(text);
            await writer.StoreAsync();
        }
        finally
        {
            writer.DetachStream();
        }
    }

    private string CreateTestDirectory()
    {
        string directoryPath = Path.Combine(TestContext.TestRunDirectory!, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }
}
