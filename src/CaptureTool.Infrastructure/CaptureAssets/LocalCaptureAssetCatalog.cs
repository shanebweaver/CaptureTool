using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.CaptureAssets.Serialization;
using System.Security.Cryptography;
using System.Text.Json;

namespace CaptureTool.Infrastructure.CaptureAssets;

internal sealed class LocalCaptureAssetCatalog : ICaptureAssetCatalog
{
    internal const string CatalogDirectoryName = "CaptureAssets";
    internal const string CatalogVersionDirectoryName = "v1";
    internal const string CatalogFileName = "catalog.assets";
    private const int CurrentSchemaVersion = 1;

    private readonly IStorageService _storageService;
    private readonly IUserDataProtectionService _dataProtectionService;
    private readonly IClock _clock;
    private readonly ILogService _logService;
    private readonly object _sync = new();

    private List<CaptureAsset> _assets = [];
    private List<CaptureAssetChange> _changes = [];
    private long _lastSequence;
    private bool _isLoaded;
    private bool _loadFailed;

    public LocalCaptureAssetCatalog(
        IStorageService storageService,
        IUserDataProtectionService dataProtectionService,
        IClock clock,
        ILogService logService)
    {
        _storageService = storageService;
        _dataProtectionService = dataProtectionService;
        _clock = clock;
        _logService = logService;
    }

    public IReadOnlyList<CaptureAsset> GetAssets()
    {
        lock (_sync)
        {
            EnsureLoaded();
            return _assets.ToArray();
        }
    }

    public CaptureAsset? Get(CaptureId captureId)
    {
        lock (_sync)
        {
            EnsureLoaded();
            return _assets.Find(asset => asset.Id == captureId);
        }
    }

    public CaptureAsset? FindByPath(string filePath)
    {
        if (!TryNormalizePath(filePath, out string normalizedPath))
        {
            return null;
        }

        lock (_sync)
        {
            EnsureLoaded();
            return FindByPathCore(normalizedPath);
        }
    }

    public IReadOnlyList<CaptureAssetChange> GetChangesAfter(long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);

        lock (_sync)
        {
            EnsureLoaded();
            return _changes.Where(change => change.Sequence > sequence).ToArray();
        }
    }

    public long GetLatestChangeSequence()
    {
        lock (_sync)
        {
            EnsureLoaded();
            return _lastSequence;
        }
    }

    public CaptureAssetCatalogWriteResult TryAdd(CaptureAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        return TryAddRange([asset])[0];
    }

    public IReadOnlyList<CaptureAssetCatalogWriteResult> TryAddRange(IReadOnlyList<CaptureAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        if (assets.Count == 0)
        {
            return [];
        }

        if (assets.Any(asset => asset is null))
        {
            throw new ArgumentException("Capture assets cannot contain null entries.", nameof(assets));
        }

        lock (_sync)
        {
            EnsureLoaded();
            if (_loadFailed)
            {
                return CreateFailedResults(assets.Count);
            }

            try
            {
                List<CaptureAsset> candidateAssets = [.. _assets];
                List<CaptureAssetChange> candidateChanges = [.. _changes];
                List<CaptureAssetCatalogWriteResult> results = new(assets.Count);
                long sequence = _lastSequence;

                foreach (CaptureAsset asset in assets)
                {
                    if (asset.LifecycleState != CaptureAssetLifecycleState.Active ||
                        asset.LifecycleRevision != 1)
                    {
                        results.Add(CaptureAssetCatalogWriteResult.Failed);
                        continue;
                    }

                    CaptureAsset? existingById = candidateAssets.Find(existing => existing.Id == asset.Id);
                    if (existingById is not null)
                    {
                        results.Add(AssetsEqual(existingById, asset)
                            ? CaptureAssetCatalogWriteResult.Unchanged(
                                existingById,
                                FindFinalizedChangeSequence(candidateChanges, existingById.Id))
                            : CaptureAssetCatalogWriteResult.Failed);
                        continue;
                    }

                    CaptureAsset? existingByPath = FindByPath(candidateAssets, asset.RetainedSourcePath);
                    if (existingByPath is null && asset.PreferredOpenPath is not null)
                    {
                        existingByPath = FindByPath(candidateAssets, asset.PreferredOpenPath);
                    }
                    if (existingByPath is not null)
                    {
                        results.Add(CaptureAssetCatalogWriteResult.Unchanged(
                            existingByPath,
                            FindFinalizedChangeSequence(candidateChanges, existingByPath.Id)));
                        continue;
                    }

                    sequence = checked(sequence + 1);
                    candidateAssets.Add(asset);
                    candidateChanges.Add(new CaptureAssetChange(
                        sequence,
                        asset.Id,
                        asset.LifecycleRevision,
                        CaptureAssetChangeType.Finalized,
                        GetUtcNow()));
                    results.Add(CaptureAssetCatalogWriteResult.Committed(asset, sequence));
                }

                if (sequence == _lastSequence)
                {
                    return results;
                }

                if (!TrySave(candidateAssets, candidateChanges, sequence))
                {
                    return CreateFailedResults(assets.Count);
                }

                _assets = candidateAssets;
                _changes = candidateChanges;
                _lastSequence = sequence;
                return results;
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Failed to add capture assets.");
                return CreateFailedResults(assets.Count);
            }
        }
    }

    public CaptureAssetCatalogWriteResult TryUpdate(
        CaptureAsset asset,
        long expectedLifecycleRevision,
        CaptureAssetChangeType changeType)
    {
        ArgumentNullException.ThrowIfNull(asset);

        lock (_sync)
        {
            EnsureLoaded();
            if (_loadFailed)
            {
                return CaptureAssetCatalogWriteResult.Failed;
            }

            int index = _assets.FindIndex(existing => existing.Id == asset.Id);
            if (index < 0)
            {
                return CaptureAssetCatalogWriteResult.Failed;
            }

            CaptureAsset existing = _assets[index];
            if (existing.LifecycleRevision != expectedLifecycleRevision)
            {
                return AssetsEqual(existing, asset)
                    ? CaptureAssetCatalogWriteResult.Unchanged(
                        existing,
                        FindLatestChangeSequence(existing.Id))
                    : CaptureAssetCatalogWriteResult.Failed;
            }

            if (AssetsEqual(existing, asset))
            {
                return CaptureAssetCatalogWriteResult.Unchanged(
                    existing,
                    FindLatestChangeSequence(existing.Id));
            }

            if (asset.LifecycleRevision != checked(expectedLifecycleRevision + 1) ||
                asset.MediaType != existing.MediaType ||
                asset.CapturedAtUtc != existing.CapturedAtUtc ||
                HasPathCollision(asset, index) ||
                !IsExpectedChange(existing, asset, changeType))
            {
                return CaptureAssetCatalogWriteResult.Failed;
            }

            try
            {
                long sequence = checked(_lastSequence + 1);
                var change = new CaptureAssetChange(
                    sequence,
                    asset.Id,
                    asset.LifecycleRevision,
                    changeType,
                    GetUtcNow());
                List<CaptureAsset> assets = [.. _assets];
                assets[index] = asset;
                List<CaptureAssetChange> changes = [.. _changes, change];
                if (!TrySave(assets, changes, sequence))
                {
                    return CaptureAssetCatalogWriteResult.Failed;
                }

                _assets = assets;
                _changes = changes;
                _lastSequence = sequence;
                return CaptureAssetCatalogWriteResult.Committed(asset, sequence);
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Failed to update a capture asset.");
                return CaptureAssetCatalogWriteResult.Failed;
            }
        }
    }

    public CaptureAssetCatalogWriteResult TryForget(
        CaptureId captureId,
        long expectedLifecycleRevision)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("A forgotten asset requires a capture ID.", nameof(captureId));
        }

        if (expectedLifecycleRevision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedLifecycleRevision));
        }

        lock (_sync)
        {
            EnsureLoaded();
            if (_loadFailed)
            {
                return CaptureAssetCatalogWriteResult.Failed;
            }

            int index = _assets.FindIndex(existing => existing.Id == captureId);
            if (index < 0)
            {
                CaptureAssetChange priorForget = _changes.LastOrDefault(change =>
                    change.CaptureId == captureId &&
                    change.ChangeType == CaptureAssetChangeType.Forgotten);
                return priorForget.Sequence > 0
                    ? new CaptureAssetCatalogWriteResult(true, false, null, priorForget.Sequence)
                    : CaptureAssetCatalogWriteResult.Failed;
            }

            CaptureAsset existing = _assets[index];
            if (existing.LifecycleRevision != expectedLifecycleRevision)
            {
                return CaptureAssetCatalogWriteResult.Failed;
            }

            try
            {
                long sequence = checked(_lastSequence + 1);
                var change = new CaptureAssetChange(
                    sequence,
                    captureId,
                    checked(existing.LifecycleRevision + 1),
                    CaptureAssetChangeType.Forgotten,
                    GetUtcNow());
                List<CaptureAsset> assets = [.. _assets];
                assets.RemoveAt(index);
                List<CaptureAssetChange> changes = [.. _changes, change];
                if (!TrySave(assets, changes, sequence))
                {
                    return CaptureAssetCatalogWriteResult.Failed;
                }

                _assets = assets;
                _changes = changes;
                _lastSequence = sequence;
                return new CaptureAssetCatalogWriteResult(true, true, null, sequence);
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Failed to forget a capture asset.");
                return CaptureAssetCatalogWriteResult.Failed;
            }
        }
    }

    private void EnsureLoaded()
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        string catalogFilePath = GetCatalogFilePath();
        if (!File.Exists(catalogFilePath))
        {
            return;
        }

        try
        {
            byte[] protectedBytes = File.ReadAllBytes(catalogFilePath);
            byte[] plaintext = _dataProtectionService.Unprotect(protectedBytes);
            CaptureAssetCatalogDocument? document;
            try
            {
                document = JsonSerializer.Deserialize(
                    plaintext,
                    CaptureAssetCatalogContext.Default.CaptureAssetCatalogDocument);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
            if (document is null || document.SchemaVersion != CurrentSchemaVersion)
            {
                throw new InvalidDataException("The capture asset catalog schema is unsupported.");
            }

            List<CaptureAsset> assets = document.Assets.Select(ToDomain).ToList();
            if (assets.Select(asset => asset.Id).Distinct().Count() != assets.Count)
            {
                throw new InvalidDataException("The capture asset catalog contains duplicate identities.");
            }

            if (!HaveUniqueActivePaths(assets))
            {
                throw new InvalidDataException("The capture asset catalog contains duplicate active paths.");
            }

            List<CaptureAssetChange> changes = document.Changes.Select(ToDomain).ToList();
            if (!IsOrdered(changes) ||
                (changes.Count > 0 && document.LastSequence != changes[^1].Sequence) ||
                (changes.Count == 0 && document.LastSequence != 0) ||
                !IsConsistent(assets, changes))
            {
                throw new InvalidDataException("The capture asset change feed is not ordered.");
            }

            _assets = assets;
            _changes = changes;
            _lastSequence = document.LastSequence;
        }
        catch (Exception ex)
        {
            _assets = [];
            _changes = [];
            _lastSequence = 0;
            _loadFailed = true;
            _logService.LogException(ex, "Failed to load the capture asset catalog.");
        }
    }

    private bool TrySave(
        IReadOnlyList<CaptureAsset> assets,
        IReadOnlyList<CaptureAssetChange> changes,
        long lastSequence)
    {
        string catalogFilePath = GetCatalogFilePath();
        string temporaryFilePath = catalogFilePath + ".tmp";

        try
        {
            string? folderPath = Path.GetDirectoryName(catalogFilePath);
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var document = new CaptureAssetCatalogDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                LastSequence = lastSequence,
                Assets = assets.Select(ToDocument).ToList(),
                Changes = changes.Select(ToDocument).ToList(),
            };
            byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(
                document,
                CaptureAssetCatalogContext.Default.CaptureAssetCatalogDocument);
            byte[] protectedBytes;
            try
            {
                protectedBytes = _dataProtectionService.Protect(plaintext);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }

            using (var stream = new FileStream(
                temporaryFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                stream.Write(protectedBytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryFilePath, catalogFilePath, true);
            return true;
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to save the capture asset catalog.");
            try
            {
                File.Delete(temporaryFilePath);
            }
            catch (Exception cleanupException)
            {
                _logService.LogException(cleanupException, "Failed to clean up a capture asset catalog write.");
            }

            return false;
        }
    }

    private CaptureAsset? FindByPathCore(string normalizedPath)
    {
        return FindByPath(_assets, normalizedPath);
    }

    private static CaptureAsset? FindByPath(IReadOnlyList<CaptureAsset> assets, string normalizedPath)
    {
        return assets.FirstOrDefault(asset =>
            asset.LifecycleState == CaptureAssetLifecycleState.Active &&
            (PathsEqual(asset.RetainedSourcePath, normalizedPath) ||
             PathsEqual(asset.PreferredOpenPath, normalizedPath)));
    }

    private static IReadOnlyList<CaptureAssetCatalogWriteResult> CreateFailedResults(int count)
    {
        return Enumerable.Repeat(CaptureAssetCatalogWriteResult.Failed, count).ToArray();
    }

    private bool HasPathCollision(CaptureAsset asset, int existingIndex)
    {
        for (int index = 0; index < _assets.Count; index++)
        {
            if (index == existingIndex ||
                _assets[index].LifecycleState != CaptureAssetLifecycleState.Active)
            {
                continue;
            }

            CaptureAsset other = _assets[index];
            if (PathsEqual(asset.RetainedSourcePath, other.RetainedSourcePath) ||
                PathsEqual(asset.RetainedSourcePath, other.PreferredOpenPath) ||
                (asset.PreferredOpenPath is not null &&
                 (PathsEqual(asset.PreferredOpenPath, other.RetainedSourcePath) ||
                  PathsEqual(asset.PreferredOpenPath, other.PreferredOpenPath))))
            {
                return true;
            }
        }

        return false;
    }

    private long? FindLatestChangeSequence(CaptureId captureId)
    {
        long sequence = _changes.LastOrDefault(change => change.CaptureId == captureId).Sequence;
        return sequence > 0 ? sequence : null;
    }

    private static long? FindFinalizedChangeSequence(
        IReadOnlyList<CaptureAssetChange> changes,
        CaptureId captureId)
    {
        long sequence = changes.FirstOrDefault(change =>
            change.CaptureId == captureId &&
            change.ChangeType == CaptureAssetChangeType.Finalized).Sequence;
        return sequence > 0 ? sequence : null;
    }

    private string GetCatalogFilePath()
    {
        return Path.Combine(
            _storageService.GetApplicationDataFolderPath(),
            CatalogDirectoryName,
            CatalogVersionDirectoryName,
            CatalogFileName);
    }

    private DateTimeOffset GetUtcNow()
    {
        DateTime utcNow = _clock.UtcNow;
        return new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
    }

    private static CaptureAsset ToDomain(CaptureAssetDocument document)
    {
        return new(
            CaptureId.Parse(document.CaptureId),
            document.MediaType,
            document.RetainedSourcePath,
            document.SourceOwnership,
            document.PreferredOpenPath,
            document.CapturedAtUtc,
            document.LifecycleState,
            document.LifecycleRevision);
    }

    private static CaptureAssetChange ToDomain(CaptureAssetChangeDocument document)
    {
        return new(
            document.Sequence,
            CaptureId.Parse(document.CaptureId),
            document.LifecycleRevision,
            document.ChangeType,
            document.ChangedAtUtc);
    }

    private static CaptureAssetDocument ToDocument(CaptureAsset asset)
    {
        return new()
        {
            CaptureId = asset.Id.ToString(),
            MediaType = asset.MediaType,
            RetainedSourcePath = asset.RetainedSourcePath,
            SourceOwnership = asset.SourceOwnership,
            PreferredOpenPath = asset.PreferredOpenPath,
            CapturedAtUtc = asset.CapturedAtUtc,
            LifecycleState = asset.LifecycleState,
            LifecycleRevision = asset.LifecycleRevision,
        };
    }

    private static CaptureAssetChangeDocument ToDocument(CaptureAssetChange change)
    {
        return new()
        {
            Sequence = change.Sequence,
            CaptureId = change.CaptureId.ToString(),
            LifecycleRevision = change.LifecycleRevision,
            ChangeType = change.ChangeType,
            ChangedAtUtc = change.ChangedAtUtc,
        };
    }

    private static bool IsOrdered(IReadOnlyList<CaptureAssetChange> changes)
    {
        long previous = 0;
        foreach (CaptureAssetChange change in changes)
        {
            if (change.Sequence != previous + 1)
            {
                return false;
            }

            previous = change.Sequence;
        }

        return true;
    }

    private static bool IsConsistent(
        IReadOnlyList<CaptureAsset> assets,
        IReadOnlyList<CaptureAssetChange> changes)
    {
        HashSet<CaptureId> assetIds = assets.Select(asset => asset.Id).ToHashSet();
        CaptureAssetChange[] orphanChanges = changes
            .Where(change => !assetIds.Contains(change.CaptureId))
            .ToArray();
        if (orphanChanges
            .GroupBy(change => change.CaptureId)
            .Any(group =>
                group.Count(change => change.ChangeType == CaptureAssetChangeType.Finalized) != 1 ||
                group.Last().ChangeType != CaptureAssetChangeType.Forgotten ||
                group.Count(change => change.ChangeType == CaptureAssetChangeType.Forgotten) != 1))
        {
            return false;
        }

        foreach (CaptureAsset asset in assets)
        {
            CaptureAssetChange[] assetChanges = changes
                .Where(change => change.CaptureId == asset.Id)
                .ToArray();
            int finalizedChangeCount = assetChanges.Count(change =>
                change.ChangeType == CaptureAssetChangeType.Finalized);
            int deletedChangeCount = assetChanges.Count(change =>
                change.ChangeType == CaptureAssetChangeType.Deleted);
            int forgottenChangeCount = assetChanges.Count(change =>
                change.ChangeType == CaptureAssetChangeType.Forgotten);
            if (assetChanges.Length == 0 ||
                finalizedChangeCount != 1 ||
                assetChanges[0].ChangeType != CaptureAssetChangeType.Finalized ||
                assetChanges[0].LifecycleRevision != 1 ||
                assetChanges[^1].LifecycleRevision != asset.LifecycleRevision ||
                (asset.LifecycleState == CaptureAssetLifecycleState.Deleted &&
                 (deletedChangeCount != 1 ||
                  assetChanges[^1].ChangeType != CaptureAssetChangeType.Deleted)) ||
                (asset.LifecycleState == CaptureAssetLifecycleState.Active &&
                 (deletedChangeCount != 0 || forgottenChangeCount != 0)))
            {
                return false;
            }

            for (int index = 1; index < assetChanges.Length; index++)
            {
                if (assetChanges[index].LifecycleRevision !=
                    assetChanges[index - 1].LifecycleRevision + 1)
                {
                    return false;
                }
            }
        }

        foreach (IGrouping<CaptureId, CaptureAssetChange> forgotten in orphanChanges
            .GroupBy(change => change.CaptureId))
        {
            CaptureAssetChange[] captureChanges = forgotten.ToArray();
            for (int index = 1; index < captureChanges.Length; index++)
            {
                if (captureChanges[index].LifecycleRevision !=
                    captureChanges[index - 1].LifecycleRevision + 1)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool HaveUniqueActivePaths(IReadOnlyList<CaptureAsset> assets)
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (CaptureAsset asset in assets.Where(asset =>
            asset.LifecycleState == CaptureAssetLifecycleState.Active))
        {
            if (!paths.Add(asset.RetainedSourcePath) ||
                (asset.PreferredOpenPath is not null &&
                 !PathsEqual(asset.RetainedSourcePath, asset.PreferredOpenPath) &&
                 !paths.Add(asset.PreferredOpenPath)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsExpectedChange(
        CaptureAsset existing,
        CaptureAsset updated,
        CaptureAssetChangeType changeType)
    {
        return changeType switch
        {
            CaptureAssetChangeType.SourceChanged =>
                existing.LifecycleState == CaptureAssetLifecycleState.Active &&
                updated.LifecycleState == CaptureAssetLifecycleState.Active &&
                (!PathsEqual(existing.RetainedSourcePath, updated.RetainedSourcePath) ||
                 existing.SourceOwnership != updated.SourceOwnership) &&
                PathsEqual(existing.PreferredOpenPath, updated.PreferredOpenPath) &&
                existing.LifecycleState == updated.LifecycleState,
            CaptureAssetChangeType.PreferredLocationChanged =>
                existing.LifecycleState == CaptureAssetLifecycleState.Active &&
                updated.LifecycleState == CaptureAssetLifecycleState.Active &&
                !PathsEqual(existing.PreferredOpenPath, updated.PreferredOpenPath) &&
                PathsEqual(existing.RetainedSourcePath, updated.RetainedSourcePath) &&
                existing.SourceOwnership == updated.SourceOwnership &&
                existing.LifecycleState == updated.LifecycleState,
            CaptureAssetChangeType.Deleted =>
                existing.LifecycleState == CaptureAssetLifecycleState.Active &&
                updated.LifecycleState == CaptureAssetLifecycleState.Deleted &&
                PathsEqual(existing.RetainedSourcePath, updated.RetainedSourcePath) &&
                existing.SourceOwnership == updated.SourceOwnership &&
                PathsEqual(existing.PreferredOpenPath, updated.PreferredOpenPath),
            _ => false,
        };
    }

    private static bool AssetsEqual(CaptureAsset left, CaptureAsset right)
    {
        return left.Id == right.Id &&
            left.MediaType == right.MediaType &&
            PathsEqual(left.RetainedSourcePath, right.RetainedSourcePath) &&
            left.SourceOwnership == right.SourceOwnership &&
            PathsEqual(left.PreferredOpenPath, right.PreferredOpenPath) &&
            left.CapturedAtUtc == right.CapturedAtUtc &&
            left.LifecycleState == right.LifecycleState &&
            left.LifecycleRevision == right.LifecycleRevision;
    }

    private static bool TryNormalizePath(string filePath, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(filePath);
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
}
