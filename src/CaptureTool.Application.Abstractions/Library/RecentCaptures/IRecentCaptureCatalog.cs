using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Abstractions.Library.RecentCaptures;

public interface IRecentCaptureCatalog
{
    IReadOnlyList<RecentCaptureCatalogEntry> GetEntries();

    void RecordCaptured(string filePath, CaptureFileType captureFileType);

    void RecordOpened(string filePath, CaptureFileType captureFileType);

    void ReplacePath(string oldFilePath, string newFilePath);

    void Touch(string filePath);

    bool Remove(string filePath);

    int RemoveRange(IEnumerable<string> filePaths);

    void Clear();
}
