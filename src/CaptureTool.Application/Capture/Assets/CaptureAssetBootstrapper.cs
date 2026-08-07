using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Capture.Assets;

internal sealed class CaptureAssetBootstrapper : ICaptureAssetBootstrapper
{
    private readonly ICaptureAssetCatalog _captureAssetCatalog;
    private readonly IRecentCaptureCatalog _recentCaptureCatalog;
    private readonly ICaptureAssetChangeSignal _changeSignal;
    private readonly IStorageService _storageService;
    private readonly IClock _clock;
    private readonly ILogService _logService;

    public CaptureAssetBootstrapper(
        ICaptureAssetCatalog captureAssetCatalog,
        IRecentCaptureCatalog recentCaptureCatalog,
        ICaptureAssetChangeSignal changeSignal,
        IStorageService storageService,
        IClock clock,
        ILogService logService)
    {
        _captureAssetCatalog = captureAssetCatalog;
        _recentCaptureCatalog = recentCaptureCatalog;
        _changeSignal = changeSignal;
        _storageService = storageService;
        _clock = clock;
        _logService = logService;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Reconcile(cancellationToken), cancellationToken);
    }

    private void Reconcile(CancellationToken cancellationToken)
    {
        bool catalogChanged = RepairPendingProjectionChanges(cancellationToken);
        catalogChanged |= MigrateRecentCaptures(cancellationToken);
        catalogChanged |= ReconcilePreferredLocations(cancellationToken);
        catalogChanged |= RecoverRetainedCaptureOrphans(cancellationToken);

        if (catalogChanged)
        {
            TrySignal();
        }
    }

    private bool RepairPendingProjectionChanges(CancellationToken cancellationToken)
    {
        long checkpoint = _recentCaptureCatalog.GetCaptureAssetChangeCheckpoint();
        IReadOnlyList<CaptureAssetChange> changes = _captureAssetCatalog.GetChangesAfter(checkpoint);
        bool observedChanges = changes.Count > 0;
        foreach (CaptureAssetChange change in changes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CaptureAsset? asset = _captureAssetCatalog.Get(change.CaptureId);
            if (asset is null || asset.LifecycleState == CaptureAssetLifecycleState.Deleted)
            {
                if (!_recentCaptureCatalog.TryAdvanceCaptureAssetChangeCheckpoint(change.Sequence))
                {
                    break;
                }

                continue;
            }

            RecentCaptureCatalogEntry? existing = _recentCaptureCatalog
                .GetEntries()
                .FirstOrDefault(entry => entry.CaptureId == asset.Id);
            DateTime activityUtc = existing?.LastActivityUtc ?? asset.CapturedAtUtc.UtcDateTime;
            string filePath = asset.PreferredOpenPath ?? asset.RetainedSourcePath;
            if (!_recentCaptureCatalog.TryProjectCaptured(
                filePath,
                asset.MediaType,
                asset.Id,
                change.Sequence,
                activityUtc))
            {
                break;
            }
        }

        return observedChanges;
    }

    private bool MigrateRecentCaptures(CancellationToken cancellationToken)
    {
        IReadOnlyList<RecentCaptureCatalogEntry> capturedEntries = _recentCaptureCatalog
            .GetEntries()
            .Where(entry => entry.Origin == RecentCaptureOrigin.Captured)
            .ToArray();
        if (capturedEntries.Count == 0)
        {
            return false;
        }

        string retainedFolderPath = _storageService.GetApplicationRetainedCaptureFolderPath();
        List<MigrationCandidate> candidates = [];
        foreach (RecentCaptureCatalogEntry entry in capturedEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CaptureAsset? existing = entry.CaptureId is CaptureId captureId
                ? _captureAssetCatalog.Get(captureId)
                : null;
            existing ??= _captureAssetCatalog.FindByPath(entry.FilePath);
            if (existing is not null)
            {
                TryAssignAndProject(entry, existing, FindFinalizedSequence(existing.Id));
                continue;
            }

            CaptureId id = entry.CaptureId ?? CaptureId.New();
            CaptureSourceOwnership ownership = IsVerifiedRetainedCaptureFile(
                entry.FilePath,
                retainedFolderPath)
                ? CaptureSourceOwnership.AppOwned
                : CaptureSourceOwnership.LegacyExternal;
            var asset = new CaptureAsset(
                id,
                entry.CaptureFileType,
                entry.FilePath,
                ownership,
                GetUtc(entry.LastActivityUtc),
                preferredOpenPath: entry.FilePath);
            candidates.Add(new(entry, asset));
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        IReadOnlyList<CaptureAssetCatalogWriteResult> results =
            _captureAssetCatalog.TryAddRange(candidates.Select(candidate => candidate.Asset).ToArray());
        bool changed = false;
        for (int index = 0; index < candidates.Count; index++)
        {
            CaptureAssetCatalogWriteResult result = results[index];
            if (!result.Succeeded || result.Asset is null)
            {
                continue;
            }

            changed |= result.Changed;
            TryAssignAndProject(candidates[index].Entry, result.Asset, result.ChangeSequence);
        }

        return changed;
    }

    private bool ReconcilePreferredLocations(CancellationToken cancellationToken)
    {
        bool changed = false;
        IReadOnlyList<RecentCaptureCatalogEntry> entries = _recentCaptureCatalog.GetEntries();
        foreach (RecentCaptureCatalogEntry entry in entries.Where(entry =>
            entry.Origin == RecentCaptureOrigin.Captured && entry.CaptureId is not null))
        {
            cancellationToken.ThrowIfCancellationRequested();
            CaptureAsset? asset = _captureAssetCatalog.Get(entry.CaptureId!.Value);
            if (asset is null || asset.LifecycleState == CaptureAssetLifecycleState.Deleted)
            {
                continue;
            }

            if (PathsEqual(entry.FilePath, asset.PreferredOpenPath ?? asset.RetainedSourcePath))
            {
                continue;
            }

            if (asset.PreferredOpenPath is not null && PathsEqual(entry.FilePath, asset.RetainedSourcePath))
            {
                long? latestSequence = FindLatestSequence(asset.Id);
                if (latestSequence is long existingSequence)
                {
                    _recentCaptureCatalog.TryProjectCaptured(
                        asset.PreferredOpenPath,
                        asset.MediaType,
                        asset.Id,
                        existingSequence,
                        entry.LastActivityUtc);
                }

                continue;
            }

            CaptureAsset updated = asset.ChangePreferredOpenPath(entry.FilePath);
            CaptureAssetCatalogWriteResult result = _captureAssetCatalog.TryUpdate(
                updated,
                asset.LifecycleRevision,
                CaptureAssetChangeType.PreferredLocationChanged);
            if (result.Succeeded && result.ChangeSequence is long sequence)
            {
                changed |= result.Changed;
                _recentCaptureCatalog.TryProjectCaptured(
                    entry.FilePath,
                    updated.MediaType,
                    updated.Id,
                    sequence,
                    entry.LastActivityUtc);
            }
        }

        return changed;
    }

    private bool RecoverRetainedCaptureOrphans(CancellationToken cancellationToken)
    {
        string retainedFolderPath = _storageService.GetApplicationRetainedCaptureFolderPath();
        if (!IsVerifiedRetainedCaptureFolder(retainedFolderPath))
        {
            return false;
        }

        HashSet<string> openedPaths = _recentCaptureCatalog
            .GetEntries()
            .Where(entry => entry.Origin == RecentCaptureOrigin.Opened)
            .Select(entry => entry.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<CaptureAsset> candidates = [];

        try
        {
            foreach (string filePath in Directory.EnumerateFiles(retainedFolderPath, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsVerifiedRetainedCaptureFile(filePath, retainedFolderPath) ||
                    openedPaths.Contains(filePath) ||
                    _recentCaptureCatalog.IsRetainedCaptureRecoveryExcluded(filePath) ||
                    _captureAssetCatalog.FindByPath(filePath) is not null)
                {
                    continue;
                }

                CaptureFileType mediaType = CaptureFileTypeDetector.DetectFileType(filePath);
                if (mediaType == CaptureFileType.Unknown)
                {
                    continue;
                }

                candidates.Add(CaptureAsset.Create(
                    mediaType,
                    filePath,
                    CaptureSourceOwnership.AppOwned,
                    GetFileTimestampUtc(filePath)));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logService.LogWarning(
                $"Failed to enumerate retained captures for identity repair ({ex.GetType().Name}).");
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        IReadOnlyList<CaptureAssetCatalogWriteResult> results = _captureAssetCatalog.TryAddRange(candidates);
        bool changed = false;
        foreach (CaptureAssetCatalogWriteResult result in results)
        {
            if (!result.Succeeded || result.Asset is null || result.ChangeSequence is not long sequence)
            {
                continue;
            }

            changed |= result.Changed;
            _recentCaptureCatalog.TryProjectCaptured(
                result.Asset.RetainedSourcePath,
                result.Asset.MediaType,
                result.Asset.Id,
                sequence,
                result.Asset.CapturedAtUtc.UtcDateTime);
        }

        return changed;
    }

    private void TryAssignAndProject(
        RecentCaptureCatalogEntry entry,
        CaptureAsset asset,
        long? changeSequence)
    {
        if (entry.CaptureId != asset.Id)
        {
            _recentCaptureCatalog.TryAssignCaptureId(entry.FilePath, asset.Id);
        }

        if (changeSequence is long sequence)
        {
            _recentCaptureCatalog.TryProjectCaptured(
                entry.FilePath,
                entry.CaptureFileType,
                asset.Id,
                sequence,
                entry.LastActivityUtc);
        }
    }

    private long? FindFinalizedSequence(CaptureId captureId)
    {
        long sequence = _captureAssetCatalog
            .GetChangesAfter(0)
            .FirstOrDefault(change =>
                change.CaptureId == captureId &&
                change.ChangeType == CaptureAssetChangeType.Finalized)
            .Sequence;
        return sequence > 0 ? sequence : null;
    }

    private long? FindLatestSequence(CaptureId captureId)
    {
        long sequence = _captureAssetCatalog
            .GetChangesAfter(0)
            .LastOrDefault(change => change.CaptureId == captureId)
            .Sequence;
        return sequence > 0 ? sequence : null;
    }

    private DateTimeOffset GetFileTimestampUtc(string filePath)
    {
        try
        {
            return GetUtc(File.GetCreationTimeUtc(filePath));
        }
        catch (Exception ex)
        {
            _logService.LogWarning(
                $"Failed to read retained capture file facts ({ex.GetType().Name}).");
            return GetUtc(_clock.UtcNow);
        }
    }

    private void TrySignal()
    {
        try
        {
            _ = _changeSignal.TrySignal();
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to signal reconciled capture asset changes.");
        }
    }

    private static DateTimeOffset GetUtc(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private static bool IsPathWithinFolder(string filePath, string folderPath)
    {
        try
        {
            string relativePath = Path.GetRelativePath(folderPath, filePath);
            return !Path.IsPathRooted(relativePath) &&
                !relativePath.Equals("..", StringComparison.Ordinal) &&
                !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsVerifiedRetainedCaptureFolder(string folderPath)
    {
        try
        {
            string normalizedPath = Path.GetFullPath(folderPath);
            FileAttributes attributes = File.GetAttributes(normalizedPath);
            return (attributes & FileAttributes.Directory) != 0 &&
                (attributes & FileAttributes.ReparsePoint) == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsVerifiedRetainedCaptureFile(string filePath, string folderPath)
    {
        try
        {
            string normalizedFolderPath = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(folderPath));
            string normalizedFilePath = Path.GetFullPath(filePath);
            if (!IsPathWithinFolder(normalizedFilePath, normalizedFolderPath) ||
                !IsVerifiedRetainedCaptureFolder(normalizedFolderPath))
            {
                return false;
            }

            string relativePath = Path.GetRelativePath(normalizedFolderPath, normalizedFilePath);
            string[] pathSegments = relativePath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            if (pathSegments.Length == 0)
            {
                return false;
            }

            string currentPath = normalizedFolderPath;
            for (int index = 0; index < pathSegments.Length; index++)
            {
                currentPath = Path.Combine(currentPath, pathSegments[index]);
                FileAttributes attributes = File.GetAttributes(currentPath);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return false;
                }

                bool isDirectory = (attributes & FileAttributes.Directory) != 0;
                bool isLastSegment = index == pathSegments.Length - 1;
                if ((!isLastSegment && !isDirectory) || (isLastSegment && isDirectory))
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool PathsEqual(string? left, string? right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record MigrationCandidate(
        RecentCaptureCatalogEntry Entry,
        CaptureAsset Asset);
}
