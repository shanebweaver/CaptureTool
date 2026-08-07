using CaptureTool.Domain;

namespace CaptureTool.Application.Abstractions.Capture.Assets;

public enum CaptureAssetRemovalKind
{
    Unknown,
    ForgetHistory,
    DeleteRetainedSource,
}

public sealed record CaptureAssetRemovalRequest
{
    public CaptureAssetRemovalRequest(
        CaptureId captureId,
        CaptureAssetRemovalKind kind,
        bool isConfirmed = false)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("Capture removal requires an ID.", nameof(captureId));
        }

        if (!Enum.IsDefined(kind) || kind == CaptureAssetRemovalKind.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        CaptureId = captureId;
        Kind = kind;
        IsConfirmed = isConfirmed;
    }

    public CaptureId CaptureId { get; }

    public CaptureAssetRemovalKind Kind { get; }

    public bool IsConfirmed { get; }
}

public enum CaptureAssetRemovalStatus
{
    Unknown,
    Succeeded,
    AlreadyRemoved,
    ConfirmationRequired,
    OwnershipDenied,
    NotFound,
    Conflict,
    Incomplete,
    Unavailable,
}

public sealed record CaptureAssetRemovalResult
{
    public CaptureAssetRemovalResult(
        CaptureAssetRemovalStatus status,
        CaptureAssetRemovalRequest request)
    {
        if (!Enum.IsDefined(status) || status == CaptureAssetRemovalStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        ArgumentNullException.ThrowIfNull(request);
        Status = status;
        Request = request;
    }

    public CaptureAssetRemovalStatus Status { get; }

    public CaptureAssetRemovalRequest Request { get; }
}

public interface ICaptureAssetRemovalService
{
    ValueTask<CaptureAssetRemovalResult> RemoveAsync(
        CaptureAssetRemovalRequest request,
        CancellationToken cancellationToken = default);
}
