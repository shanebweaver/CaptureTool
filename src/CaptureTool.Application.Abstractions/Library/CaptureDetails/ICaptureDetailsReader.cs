using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Application.Abstractions.Library.CaptureDetails;

public enum CaptureDetailsStatus { Available, Empty, SourceChanged, SourceUnavailable, Unavailable }

/// <summary>A verified read of saved analysis. Opening details never schedules analysis.</summary>
public sealed record CaptureDetailsSnapshot(CaptureDetailsStatus Status, CaptureAnalysisRecord? Record = null,
    AnalysisRun? Run = null);

public interface ICaptureDetailsReader
{
    Task<CaptureDetailsSnapshot> ReadAsync(string path, CancellationToken cancellationToken = default);
    Task<FileDetailsMetadata?> ReadFileAsync(string path, AnalysisMediaKind kind, CancellationToken cancellationToken = default);
    Task<bool> VerifySourceAsync(string path, SourceRevision revision, CancellationToken cancellationToken = default);
}
