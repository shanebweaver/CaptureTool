using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Library.CaptureDetails;

public enum CaptureDetailsStatus { Available, Empty, SourceChanged, SourceUnavailable, Unavailable }

/// <summary>A verified read of saved analysis. Opening details never schedules analysis.</summary>
public sealed record CaptureDetailsSnapshot(CaptureDetailsStatus Status, CaptureAnalysisRecord? Record = null,
    AnalysisRun? Run = null);

public interface ICaptureDetailsReader
{
    Task<CaptureDetailsSnapshot> ReadAsync(string path, CancellationToken cancellationToken = default);
}
