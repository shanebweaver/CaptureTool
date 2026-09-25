using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.DependencyInjection;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Windows.Media;
using CaptureTool.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CaptureTool.Infrastructure.Windows.Tests.Analysis;

[TestClass]
public sealed class WindowsAnalysisMediaTests
{
    public TestContext TestContext { get; set; } = null!;
    private CancellationToken Ct => TestContext.CancellationToken;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CaptureToolAnalysisMediaTests", Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [TestMethod]
    [DataRow("complete")]
    [DataRow("dispose")]
    [DataRow("cancel")]
    public async Task ClearTemporaryFilesPreservesActiveAudioAndIteratorExitReleasesItsLease(string exit)
    {
        Directory.CreateDirectory(_root);
        string source = Path.Combine(_root, "source.wav");
        byte[] sourceBytes = CreateWav(seconds: 16);
        await File.WriteAllBytesAsync(source, sourceBytes, Ct);
        string scratchRoot = Path.Combine(_root, "scratch");
        var storage = new Mock<IStorageService>();
        storage.Setup(value => value.GetApplicationScratchFolderPath()).Returns(scratchRoot);
        storage.Setup(value => value.GetTemporaryFileName()).Returns(() => Guid.NewGuid().ToString("N") + ".tmp");
        using var services = new ServiceCollection().AddGenericServices().AddApplicationServices()
            .AddSingleton(storage.Object).BuildServiceProvider();
        var scratch = services.GetRequiredService<IScratchArtifactStore>();
        var media = new WindowsAnalysisMedia(scratch);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using (var chunks = media.ReadAudioChunksAsync(source, AnalysisMediaKind.Audio, cancellation.Token).GetAsyncEnumerator(cancellation.Token))
        {
            Assert.IsTrue(await chunks.MoveNextAsync());
            string firstPath = chunks.Current.Path;
            Assert.AreEqual(TimeSpan.Zero, chunks.Current.Offset);
            Assert.AreEqual(TimeSpan.FromSeconds(15), chunks.Current.Duration);
            Assert.IsLessThanOrEqualTo(1024 * 1024L, new FileInfo(firstPath).Length);
            string abandoned = Path.Combine(scratchRoot, "abandoned.wav");
            await File.WriteAllTextAsync(abandoned, "unleased", Ct);

            scratch.ClearUnleasedArtifacts();
            Assert.IsFalse(File.Exists(abandoned), "The temporary-files action must still clear unleased artifacts.");
            Assert.IsTrue(File.Exists(firstPath), "The provider still owns the current audio chunk.");

            if (exit == "complete")
            {
                Assert.IsTrue(await chunks.MoveNextAsync(), "Clearing scratch must not prevent creating the next chunk.");
                Assert.AreEqual(firstPath, chunks.Current.Path, "Only one leased output is needed for an extraction.");
                Assert.AreEqual(TimeSpan.FromSeconds(15), chunks.Current.Offset);
                Assert.AreEqual(TimeSpan.FromSeconds(1), chunks.Current.Duration);
                scratch.ClearUnleasedArtifacts();
                Assert.IsTrue(File.Exists(chunks.Current.Path));
                Assert.IsFalse(await chunks.MoveNextAsync());
            }
            else if (exit == "cancel")
            {
                await cancellation.CancelAsync();
                await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await chunks.MoveNextAsync());
            }
        }

        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(scratchRoot), "Completion, disposal, and cancellation must release and remove the scratch artifact.");
        CollectionAssert.AreEqual(sourceBytes, await File.ReadAllBytesAsync(source, Ct));
    }

    private static byte[] CreateWav(int seconds)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true);
        int bytes = 48000 * 2 * 2 * seconds;
        writer.Write("RIFF"u8); writer.Write(36 + bytes); writer.Write("WAVEfmt "u8);
        writer.Write(16); writer.Write((short)1); writer.Write((short)2); writer.Write(48000);
        writer.Write(192000); writer.Write((short)4); writer.Write((short)16);
        writer.Write("data"u8); writer.Write(bytes); writer.Write(new byte[bytes]);
        return stream.ToArray();
    }
}
