using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Application.Abstractions.Library.CaptureDetails;

/// <summary>Reads ordinary local media properties without analysis policy, models or consent.</summary>
public interface IMediaFileDetailsReader
{
    Task<FileDetailsMetadata?> ReadAsync(string path, AnalysisMediaKind kind, DateTimeOffset? capturedAt = null,
        CancellationToken cancellationToken = default);
}
