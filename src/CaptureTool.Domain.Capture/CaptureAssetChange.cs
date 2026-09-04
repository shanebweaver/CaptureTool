using CaptureTool.Domain;

namespace CaptureTool.Domain.Capture;

public readonly record struct CaptureAssetChange
{
    public CaptureAssetChange(
        long sequence,
        CaptureId captureId,
        long lifecycleRevision,
        CaptureAssetChangeType changeType,
        DateTimeOffset changedAtUtc)
    {
        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "A change sequence must be positive.");
        }

        if (captureId.IsEmpty)
        {
            throw new ArgumentException("A capture change requires a non-empty capture ID.", nameof(captureId));
        }

        if (lifecycleRevision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lifecycleRevision), "A lifecycle revision must be positive.");
        }

        if (!Enum.IsDefined(changeType))
        {
            throw new ArgumentOutOfRangeException(nameof(changeType));
        }

        if (changedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A capture change timestamp must be expressed in UTC.", nameof(changedAtUtc));
        }

        Sequence = sequence;
        CaptureId = captureId;
        LifecycleRevision = lifecycleRevision;
        ChangeType = changeType;
        ChangedAtUtc = changedAtUtc;
    }

    public long Sequence { get; }

    public CaptureId CaptureId { get; }

    public long LifecycleRevision { get; }

    public CaptureAssetChangeType ChangeType { get; }

    public DateTimeOffset ChangedAtUtc { get; }
}
