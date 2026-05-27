using CaptureTool.Application.Abstractions.RecentCaptures;
using CaptureTool.Application.RecentCaptures;
using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.UseCases.RecentCaptures;

internal class GetRecentCapturesAppQuery : IGetRecentCapturesAppQuery
{
    public GetRecentCapturesAppQuery(
        IStorageService storageService, 
        IFileTypeDetector fileTypeDetector)
    {
        _storageService = storageService;
        _fileTypeDetector = fileTypeDetector;
    }

    private readonly IStorageService _storageService;
    private readonly IFileTypeDetector _fileTypeDetector;

    public bool IsExecuting { get; protected set; }

    public bool CanExecute()
    {
        return !IsExecuting;
    }

    public IEnumerable<IRecentCapture> Execute()
    {
        string recentCapturesFolder = _storageService.GetApplicationTemporaryFolderPath();

        var recentCaptureFiles = Directory.GetFiles(recentCapturesFolder, "*.*")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(5);

        return recentCaptureFiles
            .Where(filePath => !string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            .Select(filePath => new RecentCapture(
                filePath,
                Path.GetFileName(filePath),
                _fileTypeDetector.DetectFileType(filePath)));
    }
}