using CaptureTool.Domain;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Abstractions.Library.RecentCaptures;

public interface IRecentCaptureCatalog
{
    IReadOnlyList<RecentCaptureCatalogEntry> GetEntries();

    long GetCaptureAssetChangeCheckpoint();

    bool IsRetainedCaptureRecoveryExcluded(string filePath);

    void RecordCaptured(string filePath, CaptureFileType captureFileType);

    void RecordCaptured(string filePath, CaptureFileType captureFileType, CaptureId captureId);

    void RecordOpened(string filePath, CaptureFileType captureFileType);

    bool TryProjectCaptured(
        string filePath,
        CaptureFileType captureFileType,
        CaptureId captureId,
        long changeSequence,
        DateTime activityUtc);

    bool TryAdvanceCaptureAssetChangeCheckpoint(long changeSequence);

    bool TryAssignCaptureId(string filePath, CaptureId captureId);

    bool TryRepairCapturedProjection(
        string oldFilePath,
        string newFilePath,
        CaptureFileType captureFileType,
        CaptureId captureId,
        DateTime activityUtc);

    void ReplacePath(string oldFilePath, string newFilePath);

    void Touch(string filePath);

    bool Remove(string filePath);

    int RemoveRange(IEnumerable<string> filePaths);

    void Clear();

    void Clear(long throughChangeSequence);
}
