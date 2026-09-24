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
            await Assert.ThrowsAsync<IOException>(() => File.WriteAllTextAsync(path, "changed", TestContext.CancellationToken));
        await lease.DisposeAsync();
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
}
