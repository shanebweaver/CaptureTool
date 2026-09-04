using CaptureTool.Domain;

namespace CaptureTool.Application.Abstractions.Analysis.Privacy;

public enum CaptureAnalysisExclusionKind
{
    Unknown,
    UserExcluded,
    PrivateCapture,
}

public sealed record CaptureAnalysisExclusionRequest
{
    public CaptureAnalysisExclusionRequest(
        CaptureId captureId,
        CaptureAnalysisExclusionKind kind)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("An exclusion requires a capture ID.", nameof(captureId));
        }

        if (!Enum.IsDefined(kind) || kind == CaptureAnalysisExclusionKind.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        CaptureId = captureId;
        Kind = kind;
    }

    public CaptureId CaptureId { get; }

    public CaptureAnalysisExclusionKind Kind { get; }
}

public enum CaptureAnalysisExclusionStatus
{
    Unknown,
    Succeeded,
    AlreadyExcluded,
    Conflict,
    Rejected,
    Unavailable,
}

public sealed record CaptureAnalysisExclusionResult
{
    public CaptureAnalysisExclusionResult(
        CaptureAnalysisExclusionStatus status,
        CaptureAnalysisExclusionRequest request)
    {
        if (!Enum.IsDefined(status) || status == CaptureAnalysisExclusionStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        ArgumentNullException.ThrowIfNull(request);
        Status = status;
        Request = request;
    }

    public CaptureAnalysisExclusionStatus Status { get; }

    public CaptureAnalysisExclusionRequest Request { get; }
}

public interface ICaptureAnalysisExclusionService
{
    ValueTask<CaptureAnalysisExclusionResult> ExcludeAsync(
        CaptureAnalysisExclusionRequest request,
        CancellationToken cancellationToken = default);
}
