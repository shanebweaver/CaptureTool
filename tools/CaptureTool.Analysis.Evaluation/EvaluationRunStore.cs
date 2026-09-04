using System.Text;
using System.Text.Json;

namespace CaptureTool.Analysis.Evaluation;

public sealed class EvaluationRunStore
{
    public const string NamespaceName = "capturetool.analysis.evaluation/v1";
    public const string ReportFileName = "report.json";

    private const string MarkerFileName = ".capturetool-evaluation-root";
    private const string MarkerContents = NamespaceName + "\nnon-user-content=true\n";

    private readonly string _rootPath;

    public EvaluationRunStore(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _rootPath = Path.GetFullPath(rootPath);
        if (Path.GetPathRoot(_rootPath) == _rootPath)
        {
            throw new ArgumentException("The evaluation namespace cannot be a file-system root.", nameof(rootPath));
        }
    }

    public string RootPath => _rootPath;

    public async Task<string> WriteAsync(
        EvaluationReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Namespace != NamespaceName)
        {
            throw new InvalidDataException("Only CaptureTool evaluation reports can enter this namespace.");
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        string runDirectory = GetRunDirectory(report.RunId);
        Directory.CreateDirectory(runDirectory);
        string reportPath = Path.Combine(runDirectory, ReportFileName);
        if (File.Exists(reportPath))
        {
            throw new IOException($"Evaluation run '{report.RunId}' is immutable and already exists.");
        }

        string temporaryPath = Path.Combine(runDirectory, $".{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    report,
                    EvaluationJsonContext.Default.EvaluationReport,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, reportPath);
            return reportPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public async Task<int> PruneExpiredAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_rootPath))
        {
            return 0;
        }

        await VerifyMarkerAsync(cancellationToken).ConfigureAwait(false);
        int removed = 0;
        foreach (string runDirectory in Directory.EnumerateDirectories(_rootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string reportPath = Path.Combine(runDirectory, ReportFileName);
            if (!File.Exists(reportPath))
            {
                continue;
            }

            EvaluationReport? report;
            await using (FileStream stream = File.OpenRead(reportPath))
            {
                report = await JsonSerializer.DeserializeAsync(
                    stream,
                    EvaluationJsonContext.Default.EvaluationReport,
                    cancellationToken).ConfigureAwait(false);
            }

            if (report is null ||
                report.Namespace != NamespaceName ||
                !string.Equals(
                    Path.GetFileName(runDirectory),
                    report.RunId,
                    StringComparison.Ordinal) ||
                report.ExpiresUtc > nowUtc)
            {
                continue;
            }

            EnsureImmediateChild(runDirectory);
            Directory.Delete(runDirectory, recursive: true);
            removed++;
        }

        return removed;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        string markerPath = Path.Combine(_rootPath, MarkerFileName);
        if (Directory.Exists(_rootPath))
        {
            if (File.Exists(markerPath))
            {
                await VerifyMarkerAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            if (Directory.EnumerateFileSystemEntries(_rootPath).Any())
            {
                throw new InvalidDataException(
                    "The requested output directory is not an empty or marked CaptureTool evaluation namespace.");
            }
        }
        else
        {
            Directory.CreateDirectory(_rootPath);
        }

        await File.WriteAllTextAsync(
            markerPath,
            MarkerContents,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task VerifyMarkerAsync(CancellationToken cancellationToken)
    {
        string markerPath = Path.Combine(_rootPath, MarkerFileName);
        if (!File.Exists(markerPath) ||
            await File.ReadAllTextAsync(markerPath, cancellationToken).ConfigureAwait(false) != MarkerContents)
        {
            throw new InvalidDataException(
                "The directory is not a verified CaptureTool evaluation namespace.");
        }
    }

    private string GetRunDirectory(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId) ||
            runId is "." or ".." ||
            runId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidDataException("The evaluation run id is not a safe directory name.");
        }

        string runDirectory = Path.GetFullPath(Path.Combine(_rootPath, runId));
        EnsureImmediateChild(runDirectory);
        return runDirectory;
    }

    private void EnsureImmediateChild(string candidatePath)
    {
        string relative = Path.GetRelativePath(_rootPath, Path.GetFullPath(candidatePath));
        if (relative is "." or ".." ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relative.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
            relative.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
        {
            throw new InvalidDataException("Evaluation data must remain an immediate child of its namespace.");
        }
    }
}
