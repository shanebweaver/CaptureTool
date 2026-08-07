using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Abstractions.Analysis.Intake;

public sealed record CaptureAssetChangeBatch
{
    private readonly IReadOnlyList<CaptureAssetChange> _changes;

    public CaptureAssetChangeBatch(
        long requestedAfterSequence,
        long nextCheckpoint,
        long latestSequence,
        IEnumerable<CaptureAssetChange> changes)
    {
        if (requestedAfterSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedAfterSequence));
        }

        if (nextCheckpoint < requestedAfterSequence || latestSequence < nextCheckpoint)
        {
            throw new ArgumentException("Capture change checkpoints must be monotonic.");
        }

        ArgumentNullException.ThrowIfNull(changes);
        CaptureAssetChange[] copiedChanges = [.. changes];
        long previousSequence = requestedAfterSequence;
        foreach (CaptureAssetChange change in copiedChanges)
        {
            if (change.Sequence <= previousSequence ||
                change.Sequence > nextCheckpoint ||
                change.CaptureId.IsEmpty ||
                change.LifecycleRevision <= 0)
            {
                throw new ArgumentException(
                    "Capture changes must be valid and strictly ordered inside the checkpoint range.",
                    nameof(changes));
            }

            previousSequence = change.Sequence;
        }

        if ((copiedChanges.Length == 0 && nextCheckpoint != requestedAfterSequence) ||
            (copiedChanges.Length > 0 && copiedChanges[^1].Sequence != nextCheckpoint))
        {
            throw new ArgumentException(
                "The next checkpoint must identify the last returned change.",
                nameof(nextCheckpoint));
        }

        RequestedAfterSequence = requestedAfterSequence;
        NextCheckpoint = nextCheckpoint;
        LatestSequence = latestSequence;
        _changes = Array.AsReadOnly(copiedChanges);
    }

    public long RequestedAfterSequence { get; }

    public long NextCheckpoint { get; }

    public long LatestSequence { get; }

    public IReadOnlyList<CaptureAssetChange> Changes => _changes;

    public bool HasMore => NextCheckpoint < LatestSequence;
}

public interface ICaptureAssetChangeReader
{
    ValueTask<CaptureAssetChangeBatch> ReadAfterAsync(
        long checkpoint,
        CancellationToken cancellationToken = default);
}

public interface ICaptureAnalysisWakeSignal
{
    bool TrySignal();
}
