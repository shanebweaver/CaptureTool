using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Capture.Assets;
using System.Security.Cryptography;
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
    private readonly ICaptureAssetCatalog? _assets;

    public GetRecentCapturesUseCase(
        IRecentCaptureCatalog recentCaptureCatalog,
        IFileSystem fileSystem,
        IUseCaseExecutor useCaseExecutor, ICaptureAssetCatalog? assets = null)
    {
        _useCaseExecutor = useCaseExecutor;
        _recentCaptureCatalog = recentCaptureCatalog;
        _fileSystem = fileSystem;
        _assets = assets;
    }

    public bool CanExecute(GetRecentCapturesRequest request)
    {
        return request.Skip >= 0 && request.Take > 0;
    }

    public Task<UseCaseResponse<GetRecentCapturesResponse>> ExecuteAsync(GetRecentCapturesRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
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

                var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (_assets != null)
                {
                    try
                    {
                        var assets = await _assets.ReadAllAsync(cancellationToken);
                        foreach (var group in assets.SelectMany(asset => new[] { asset.SourcePath, asset.PreferredPath }
                            .Where(path => path != null).Distinct(StringComparer.OrdinalIgnoreCase).Select(path => (Path: path!, Asset: asset)))
                            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
                        {
                            if (group.Count() == 1 && group.First().Asset.Name is { } name) names[group.Key] = name.Text;
                        }
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException) { }
                }
                IReadOnlyList<RecentCapture> requestedCaptures = catalogEntries
                    .Where(entry => _fileSystem.FileExists(entry.FilePath))
                    .OrderByDescending(entry => entry.LastActivityUtc)
                    .Select(entry => new RecentCapture(
                        entry.FilePath,
                        names.GetValueOrDefault(entry.FilePath) ?? Path.GetFileName(entry.FilePath),
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
