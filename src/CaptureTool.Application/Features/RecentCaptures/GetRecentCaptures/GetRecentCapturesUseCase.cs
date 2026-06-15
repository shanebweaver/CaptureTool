using CaptureTool.Application.Abstractions.Features.RecentCaptures;
using CaptureTool.Application.Abstractions.Features.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Storage;

namespace CaptureTool.Application.Features.RecentCaptures.GetRecentCaptures;

public sealed class GetRecentCapturesUseCase : IGetRecentCapturesUseCase
{
    private readonly IStorageService _storageService;
    private readonly IFileTypeDetector _fileTypeDetector;

    public GetRecentCapturesUseCase(
        IStorageService storageService,
        IFileTypeDetector fileTypeDetector)
    {
        _storageService = storageService;
        _fileTypeDetector = fileTypeDetector;
    }

    public bool CanExecute(GetRecentCapturesRequest request)
    {
        return true;
    }

    public Task<GetRecentCapturesResponse> ExecuteAsync(GetRecentCapturesRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            string recentCapturesFolder = _storageService.GetApplicationTemporaryFolderPath();

            IReadOnlyList<RecentCapture> recentCaptures = Directory.GetFiles(recentCapturesFolder, "*.*")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(5)
                .Where(filePath => !string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                .Select(filePath => new RecentCapture(
                    filePath,
                    Path.GetFileName(filePath),
                    _fileTypeDetector.DetectFileType(filePath)))
                .ToArray();

            return Task.FromResult(new GetRecentCapturesResponse(recentCaptures));
        }
        catch (Exception)
        {
            return Task.FromResult(new GetRecentCapturesResponse([]));
        }
    }
}
