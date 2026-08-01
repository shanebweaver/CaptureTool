using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Library.RecentCaptures.GetRecentCaptures;

internal sealed class GetRecentCapturesUseCase : IGetRecentCapturesUseCase
{
    private const string ActivityId = "GetRecentCaptures";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IRecentCaptureCatalog _recentCaptureCatalog;
    private readonly IFileSystem _fileSystem;

    public GetRecentCapturesUseCase(
        IRecentCaptureCatalog recentCaptureCatalog,
        IFileSystem fileSystem,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _recentCaptureCatalog = recentCaptureCatalog;
        _fileSystem = fileSystem;
    }

    public bool CanExecute(GetRecentCapturesRequest request)
    {
        return request.Skip >= 0 && request.Take > 0;
    }

    public Task<UseCaseResponse<GetRecentCapturesResponse>> ExecuteAsync(GetRecentCapturesRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                int skip = Math.Max(0, request.Skip);
                int take = request.Take <= 0 ? 5 : request.Take;

                IReadOnlyList<RecentCaptureCatalogEntry> catalogEntries = _recentCaptureCatalog.GetEntries();
                string[] missingFilePaths = catalogEntries
                    .Where(entry => !_fileSystem.FileExists(entry.FilePath))
                    .Select(entry => entry.FilePath)
                    .ToArray();
                if (missingFilePaths.Length > 0)
                {
                    _recentCaptureCatalog.RemoveRange(missingFilePaths);
                }

                IReadOnlyList<RecentCapture> requestedCaptures = catalogEntries
                    .Where(entry => _fileSystem.FileExists(entry.FilePath))
                    .OrderByDescending(entry => entry.LastActivityUtc)
                    .Select(entry => new RecentCapture(
                        entry.FilePath,
                        Path.GetFileName(entry.FilePath),
                        entry.CaptureFileType))
                    .Skip(skip)
                    .Take(take + 1)
                    .ToArray();

                IReadOnlyList<RecentCapture> recentCaptures = requestedCaptures
                    .Take(take)
                    .ToArray();

                return new GetRecentCapturesResponse(recentCaptures, requestedCaptures.Count > take);
            },
            cancellationToken: cancellationToken);
    }
}
