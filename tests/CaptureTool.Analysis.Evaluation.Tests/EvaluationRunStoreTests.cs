using CaptureTool.Analysis.Evaluation;
using System.Text.Json;

namespace CaptureTool.Analysis.Evaluation.Tests;

[TestClass]
public sealed class EvaluationRunStoreTests
{
    [TestMethod]
    public async Task WriteAndPrune_ShouldOnlyManageExpiredMarkedEvaluationRuns()
    {
        string root = CreateTemporaryPath();
        try
        {
            EvaluationReport report = EvaluationEngineTests.Evaluate(
                EvaluationEngineTests.CreateCorpus(),
                EvaluationEngineTests.CreatePassingRun());
            var store = new EvaluationRunStore(root);

            string reportPath = await store.WriteAsync(report);
            Directory.CreateDirectory(Path.Combine(root, "foreign-directory"));
            Assert.IsTrue(File.Exists(reportPath));
            Assert.ThrowsExactly<IOException>(() => store.WriteAsync(report).GetAwaiter().GetResult());

            int removedBeforeExpiry = await store.PruneExpiredAsync(report.ExpiresUtc.AddTicks(-1));
            int removedAfterExpiry = await store.PruneExpiredAsync(report.ExpiresUtc);

            Assert.AreEqual(0, removedBeforeExpiry);
            Assert.AreEqual(1, removedAfterExpiry);
            Assert.IsFalse(Directory.Exists(Path.Combine(root, report.RunId)));
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "foreign-directory")));
        }
        finally
        {
            DeleteTemporaryPath(root);
        }
    }

    [TestMethod]
    public async Task Write_ShouldRefuseUnmarkedNonemptyDirectoryAndForeignReport()
    {
        string root = CreateTemporaryPath();
        try
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(Path.Combine(root, "user-file.txt"), "keep");
            var store = new EvaluationRunStore(root);
            EvaluationReport report = EvaluationEngineTests.Evaluate(
                EvaluationEngineTests.CreateCorpus(),
                EvaluationEngineTests.CreatePassingRun());

            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.WriteAsync(report));
            report.Namespace = "foreign";
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                new EvaluationRunStore(CreateTemporaryPath()).WriteAsync(report));
            Assert.IsTrue(File.Exists(Path.Combine(root, "user-file.txt")));
        }
        finally
        {
            DeleteTemporaryPath(root);
        }
    }

    [TestMethod]
    public async Task Prune_ShouldIgnoreTamperedOrMismatchedReports()
    {
        string root = CreateTemporaryPath();
        try
        {
            EvaluationReport report = EvaluationEngineTests.Evaluate(
                EvaluationEngineTests.CreateCorpus(),
                EvaluationEngineTests.CreatePassingRun());
            var store = new EvaluationRunStore(root);
            string reportPath = await store.WriteAsync(report);
            report.RunId = "different-run";
            await File.WriteAllTextAsync(
                reportPath,
                JsonSerializer.Serialize(report, EvaluationJsonContext.Default.EvaluationReport));

            int removed = await store.PruneExpiredAsync(report.ExpiresUtc.AddDays(1));

            Assert.AreEqual(0, removed);
            Assert.IsTrue(File.Exists(reportPath));
        }
        finally
        {
            DeleteTemporaryPath(root);
        }
    }

    [TestMethod]
    public void Constructor_ShouldRejectRootAndUnsafeRunPaths()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new EvaluationRunStore(Path.GetPathRoot(Path.GetTempPath())!));

        ProviderEvaluationRun run = EvaluationEngineTests.CreatePassingRun();
        run.RunId = "..";
        Assert.ThrowsExactly<InvalidDataException>(() =>
            EvaluationEngineTests.Evaluate(EvaluationEngineTests.CreateCorpus(), run));
    }

    private static string CreateTemporaryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "CaptureTool-Evaluation-Tests",
            Guid.NewGuid().ToString("N"));
    }

    private static void DeleteTemporaryPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string expectedParent = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "CaptureTool-Evaluation-Tests"));
        if (Directory.Exists(fullPath) &&
            Path.GetDirectoryName(fullPath) == expectedParent)
        {
            Directory.Delete(fullPath, recursive: true);
        }
    }
}
