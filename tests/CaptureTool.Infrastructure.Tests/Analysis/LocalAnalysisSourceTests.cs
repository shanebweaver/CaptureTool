using CaptureTool.Infrastructure.Analysis.Sources;

namespace CaptureTool.Infrastructure.Tests.Analysis;

[TestClass]
public sealed class LocalAnalysisSourceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LeaseVerifiesContentAndPreventsMutationUntilDisposed()
    {
        using var environment = new AnalysisTestEnvironment();
        Directory.CreateDirectory(environment.Root);
        string path = Path.Combine(environment.Root, "source.bin");
        await File.WriteAllTextAsync(path, "original", TestContext.CancellationToken);
        var source = new LocalAnalysisSource();
        var lease = await source.OpenAsync(path, TestContext.CancellationToken);
        var revision = lease.Revision;
        Assert.IsTrue(await lease.VerifyAsync(TestContext.CancellationToken));
        if (OperatingSystem.IsWindows())
        {
            await Assert.ThrowsAsync<IOException>(() => File.WriteAllTextAsync(path, "changed", TestContext.CancellationToken));
            Assert.Throws<IOException>(() => File.Move(path, path + ".moved"));
            Assert.Throws<IOException>(() => File.Delete(path));
            Assert.AreEqual("original", await File.ReadAllTextAsync(path, TestContext.CancellationToken));
        }
        await lease.DisposeAsync();
        File.Move(path, path + ".moved");
        await File.WriteAllTextAsync(path, "changed", TestContext.CancellationToken);
        await using var changed = await source.OpenAsync(path, TestContext.CancellationToken);
        Assert.AreNotEqual(revision, changed.Revision);
    }

    [TestMethod]
    public async Task MissingAndEmptySourcesFailBeforeAnalysis()
    {
        using var environment = new AnalysisTestEnvironment();
        Directory.CreateDirectory(environment.Root);
        string path = Path.Combine(environment.Root, "source.bin");
        var source = new LocalAnalysisSource();
        await Assert.ThrowsExactlyAsync<FileNotFoundException>(() => source.OpenAsync(path, TestContext.CancellationToken));
        await File.WriteAllBytesAsync(path, [], TestContext.CancellationToken);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => source.OpenAsync(path, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => source.OpenAsync("relative.bin", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task FileIdentitySurvivesRenameAndRejectsAReplacementWithIdenticalBytes()
    {
        using var environment = new AnalysisTestEnvironment();
        Directory.CreateDirectory(environment.Root);
        string path = Path.Combine(environment.Root, "source.bin");
        string renamed = Path.Combine(environment.Root, "renamed.bin");
        var source = new LocalAnalysisSource();
        await File.WriteAllTextAsync(path, "same content", TestContext.CancellationToken);
        var identity = await source.GetIdentityAsync(path, TestContext.CancellationToken);

        File.Move(path, renamed);
        await File.WriteAllTextAsync(path, "same content", TestContext.CancellationToken);

        Assert.AreEqual(identity, await source.GetIdentityAsync(renamed, TestContext.CancellationToken));
        Assert.AreNotEqual(identity, await source.GetIdentityAsync(path, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => source.OpenAsync(path, TestContext.CancellationToken, identity));
        // A rejected open must release its handle.
        File.Delete(path);
        await using var lease = await source.OpenAsync(renamed, TestContext.CancellationToken, identity);
        Assert.IsTrue(await lease.VerifyAsync(TestContext.CancellationToken));
    }
}
