using CaptureTool.Infrastructure.Analysis.Windows.Media;

namespace CaptureTool.Infrastructure.Analysis.Windows.Tests.Media;

[TestClass]
public sealed class WindowsVideoAnalysisWorkingFilesTests
{
    [TestMethod]
    public void TryDelete_ShouldDeleteManagedArtifactAndPreserveExternalCanary()
    {
        string externalDirectory = Path.Combine(
            Path.GetTempPath(),
            "CaptureToolTests",
            "ExternalVideoCanary",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(externalDirectory);
        string externalPath = Path.Combine(externalDirectory, "user-content.mp4");
        File.WriteAllBytes(externalPath, [1, 2, 3]);
        string managedPath = WindowsVideoAnalysisWorkingFiles.CreatePath(".tmp");
        File.WriteAllBytes(managedPath, [4, 5, 6]);

        try
        {
            WindowsVideoAnalysisWorkingFiles.TryDelete(externalPath);
            WindowsVideoAnalysisWorkingFiles.TryDelete(managedPath);

            Assert.IsTrue(File.Exists(externalPath));
            Assert.IsFalse(File.Exists(managedPath));
        }
        finally
        {
            File.Delete(externalPath);
            Directory.Delete(externalDirectory);
            WindowsVideoAnalysisWorkingFiles.TryDelete(managedPath);
        }
    }

    [TestMethod]
    public void Prune_ShouldDeleteOnlyAbandonedManagedArtifacts()
    {
        string oldPath = WindowsVideoAnalysisWorkingFiles.CreatePath(".old");
        string freshPath = WindowsVideoAnalysisWorkingFiles.CreatePath(".fresh");
        File.WriteAllBytes(oldPath, [1]);
        File.WriteAllBytes(freshPath, [2]);
        DateTime nowUtc = new(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(oldPath, nowUtc.AddDays(-8));
        File.SetLastWriteTimeUtc(freshPath, nowUtc.AddDays(-1));

        try
        {
            WindowsVideoAnalysisWorkingFiles.TryPruneAbandonedFiles(nowUtc.AddDays(-7));

            Assert.IsFalse(File.Exists(oldPath));
            Assert.IsTrue(File.Exists(freshPath));
        }
        finally
        {
            WindowsVideoAnalysisWorkingFiles.TryDelete(oldPath);
            WindowsVideoAnalysisWorkingFiles.TryDelete(freshPath);
        }
    }
}
