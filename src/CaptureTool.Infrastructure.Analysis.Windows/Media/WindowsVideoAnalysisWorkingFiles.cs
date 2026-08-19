namespace CaptureTool.Infrastructure.Analysis.Windows.Media;

internal static class WindowsVideoAnalysisWorkingFiles
{
    private static readonly TimeSpan AbandonedFileRetention = TimeSpan.FromDays(7);
    private static readonly string WorkingFolder = Path.Combine(
        Path.GetTempPath(),
        "CaptureTool",
        "AnalysisVideo");
    private static int _hasPrunedAbandonedFiles;

    public static string CreatePath(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        Directory.CreateDirectory(WorkingFolder);
        if (Interlocked.Exchange(ref _hasPrunedAbandonedFiles, 1) == 0)
        {
            TryPruneAbandonedFiles(DateTime.UtcNow - AbandonedFileRetention);
        }

        return Path.Combine(WorkingFolder, $"{Guid.NewGuid():N}{extension}");
    }

    public static async Task CopyToNewFileAsync(
        Stream source,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    public static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string fullPath = Path.GetFullPath(path);
            string managedRoot = Path.GetFullPath(WorkingFolder)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(managedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch
        {
            // These are app-created non-user working files. Cleanup is best effort.
        }
    }

    internal static void TryPruneAbandonedFiles(DateTime olderThanUtc)
    {
        try
        {
            foreach (string path in Directory.EnumerateFiles(
                WorkingFolder,
                "*",
                SearchOption.TopDirectoryOnly))
            {
                if (File.GetLastWriteTimeUtc(path) < olderThanUtc)
                {
                    TryDelete(path);
                }
            }
        }
        catch
        {
            // This folder contains only app-created working files; later runs retry cleanup.
        }
    }
}
