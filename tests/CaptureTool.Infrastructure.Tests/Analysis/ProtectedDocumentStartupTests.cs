using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Infrastructure.Analysis.Persistence;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace CaptureTool.Infrastructure.Tests.Analysis;

[TestClass]
public sealed class ProtectedDocumentStartupTests
{
    public TestContext TestContext { get; set; } = null!;
    private CancellationToken Ct => TestContext.CancellationToken;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [DoNotParallelize]
    public async Task FreshStartupSkipsMissingDocumentsWithoutThrowing(bool appDataDirectoryExists)
    {
        using var environment = new AnalysisTestEnvironment();
        if (appDataDirectoryExists) Directory.CreateDirectory(environment.Root);
        var policy = new LocalCaptureMemoryPolicyStore(environment, environment.Protector, environment.Files);
        using var analysis = environment.CreateStore();
        using var catalog = environment.CreateCatalog();
        int reads = 0;
        environment.Files.BeforeRead = (_, _) =>
        {
            reads++;
            return Task.CompletedTask;
        };
        ConcurrentQueue<Exception> missingFileExceptions = new();
        void ObserveException(object? sender, FirstChanceExceptionEventArgs args)
        {
            if (args.Exception is FileNotFoundException or DirectoryNotFoundException &&
                args.Exception.Message.Contains(environment.Root, StringComparison.Ordinal))
                missingFileExceptions.Enqueue(args.Exception);
        }

        // A null result alone would also pass when missing-file exceptions were caught internally.
        AppDomain.CurrentDomain.FirstChanceException += ObserveException;
        try
        {
            Assert.IsNull(await policy.LoadAsync(Ct));
            await analysis.InitializeAsync(Ct);
            Assert.IsEmpty(await catalog.ReadAllAsync(Ct));
            Assert.IsEmpty(await catalog.ReadAllAsync(Ct));
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= ObserveException;
        }

        Assert.IsEmpty(missingFileExceptions, string.Join(Environment.NewLine, missingFileExceptions.Select(exception => exception.Message)));
        Assert.AreEqual(0, reads, "Missing documents must be skipped before the file read is attempted.");
        Assert.IsTrue(File.Exists(environment.ControlPath), "A fresh analysis store still initializes its control document.");
        Assert.IsFalse(File.Exists(Path.Combine(environment.Root, "CaptureAssets", "catalog.bin")));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DocumentRemovedBetweenExistenceCheckAndReadIsHandled(bool removeDirectory)
    {
        using var environment = new AnalysisTestEnvironment();
        var policy = new LocalCaptureMemoryPolicyStore(environment, environment.Protector, environment.Files);
        await policy.SaveAsync(CaptureMemoryPolicy.Disabled(), Ct);
        environment.Files.BeforeRead = (path, _) =>
        {
            File.Delete(path);
            if (removeDirectory) Directory.Delete(environment.Root);
            return Task.CompletedTask;
        };

        Assert.IsNull(await policy.LoadAsync(Ct));
    }

    [TestMethod]
    public async Task ExistingDocumentReadFailureIsStillReported()
    {
        using var environment = new AnalysisTestEnvironment();
        var policy = new LocalCaptureMemoryPolicyStore(environment, environment.Protector, environment.Files);
        var saved = CaptureMemoryPolicy.Disabled();
        await policy.SaveAsync(saved, Ct);
        environment.Files.BeforeRead = (_, _) => throw new UnauthorizedAccessException("Injected read failure.");

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => policy.LoadAsync(Ct));
        environment.Files.BeforeRead = null;
        Assert.AreEqual(saved, await policy.LoadAsync(Ct));
    }
}
