using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.RecentCaptures.Serialization;
using System.Security.Cryptography;
using System.Text.Json;

namespace CaptureTool.Infrastructure.RecentCaptures;

internal sealed class LocalRecentCaptureCatalog : IRecentCaptureCatalog
{
    private const int MaximumEntryCount = 1000;
    private const string CatalogFileName = "RecentCaptures.json";
    private const int CurrentSchemaVersion = 1;

    private readonly Lock _sync = new();
    private readonly IStorageService _storageService;
    private readonly IUserDataProtectionService _dataProtectionService;
    private readonly IClock _clock;
    private readonly ILogService _logService;
    private readonly IRecentCapturesChangeNotifier _changeNotifier;
    private List<RecentCaptureCatalogEntry> _entries = [];
    private long _captureAssetChangeCheckpoint;
    private SortedSet<long> _appliedOutOfOrderSequences = [];
    private HashSet<string> _retainedCaptureRecoveryExclusions = new(StringComparer.OrdinalIgnoreCase);
    private bool _retainedCaptureRecoveryDisabled;
    private bool _isLoaded;

    public LocalRecentCaptureCatalog(
        IStorageService storageService,
        IUserDataProtectionService dataProtectionService,
        IClock clock,
        ILogService logService,
        IRecentCapturesChangeNotifier changeNotifier)
    {
        _storageService = storageService;
        _dataProtectionService = dataProtectionService;
        _clock = clock;
        _logService = logService;
        _changeNotifier = changeNotifier;
    }

    public IReadOnlyList<RecentCaptureCatalogEntry> GetEntries()
    {
        lock (_sync)
        {
            EnsureLoaded();
            return [.. _entries];
        }
    }

    public void RecordCaptured(string filePath, CaptureFileType captureFileType)
    {
        Record(filePath, captureFileType, RecentCaptureOrigin.Captured, captureId: null);
    }

    public long GetCaptureAssetChangeCheckpoint()
    {
        lock (_sync)
        {
            EnsureLoaded();
            return _captureAssetChangeCheckpoint;
        }
    }

    public bool IsRetainedCaptureRecoveryExcluded(string filePath)
    {
        if (!TryNormalizePath(filePath, out string normalizedPath))
        {
            return true;
        }

        lock (_sync)
        {
            EnsureLoaded();
            return _retainedCaptureRecoveryDisabled ||
                _retainedCaptureRecoveryExclusions.Contains(normalizedPath);
        }
    }

    public void RecordCaptured(string filePath, CaptureFileType captureFileType, CaptureId captureId)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("Capture identity cannot be empty.", nameof(captureId));
        }

        Record(filePath, captureFileType, RecentCaptureOrigin.Captured, captureId);
    }

    public void RecordOpened(string filePath, CaptureFileType captureFileType)
    {
        Record(filePath, captureFileType, RecentCaptureOrigin.Opened, captureId: null);
    }

    public bool TryProjectCaptured(
        string filePath,
        CaptureFileType captureFileType,
        CaptureId captureId,
        long changeSequence,
        DateTime activityUtc)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("Capture identity cannot be empty.", nameof(captureId));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(changeSequence);
        if (activityUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Capture activity must be expressed in UTC.", nameof(activityUtc));
        }

        if (captureFileType == CaptureFileType.Unknown ||
            !TryNormalizePath(filePath, out string normalizedPath))
        {
            return false;
        }

        bool entryChanged;
        lock (_sync)
        {
            EnsureLoaded();
            if (IsChangeSequenceApplied(changeSequence))
            {
                if (!_entries.Any(entry => entry.CaptureId == captureId))
                {
                    return true;
                }

                List<RecentCaptureCatalogEntry> repairEntries = [.. _entries];
                entryChanged = ProjectCapturedEntry(
                    repairEntries,
                    normalizedPath,
                    captureFileType,
                    captureId,
                    activityUtc);
                if (!entryChanged)
                {
                    return true;
                }

                HashSet<string> repairExclusions = new(
                    _retainedCaptureRecoveryExclusions,
                    StringComparer.OrdinalIgnoreCase);
                repairExclusions.Remove(normalizedPath);
                TrimEntries(repairEntries, repairExclusions);
                if (!TrySave(
                    repairEntries,
                    _captureAssetChangeCheckpoint,
                    _appliedOutOfOrderSequences,
                    repairExclusions))
                {
                    return false;
                }

                _entries = repairEntries;
                _retainedCaptureRecoveryExclusions = repairExclusions;
            }
            else
            {
                _ = TryCreateAcknowledgedSequenceState(
                    changeSequence,
                    out long candidateCheckpoint,
                    out SortedSet<long> candidateOutOfOrderSequences);
                List<RecentCaptureCatalogEntry> candidateEntries = [.. _entries];
                entryChanged = ProjectCapturedEntry(
                    candidateEntries,
                    normalizedPath,
                    captureFileType,
                    captureId,
                    activityUtc);
                HashSet<string> candidateExclusions = new(
                    _retainedCaptureRecoveryExclusions,
                    StringComparer.OrdinalIgnoreCase);
                candidateExclusions.Remove(normalizedPath);
                TrimEntries(candidateEntries, candidateExclusions);

                if (!TrySave(
                    candidateEntries,
                    candidateCheckpoint,
                    candidateOutOfOrderSequences,
                    candidateExclusions))
                {
                    return false;
                }

                _entries = candidateEntries;
                _captureAssetChangeCheckpoint = candidateCheckpoint;
                _appliedOutOfOrderSequences = candidateOutOfOrderSequences;
                _retainedCaptureRecoveryExclusions = candidateExclusions;
            }
        }

        if (entryChanged)
        {
            _changeNotifier.NotifyRecentCapturesChanged();
        }

        return true;
    }

    public bool TryAdvanceCaptureAssetChangeCheckpoint(long changeSequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(changeSequence);

        lock (_sync)
        {
            EnsureLoaded();
            if (IsChangeSequenceApplied(changeSequence))
            {
                return true;
            }

            _ = TryCreateAcknowledgedSequenceState(
                changeSequence,
                out long candidateCheckpoint,
                out SortedSet<long> candidateOutOfOrderSequences);

            if (!TrySave(_entries, candidateCheckpoint, candidateOutOfOrderSequences))
            {
                return false;
            }

            _captureAssetChangeCheckpoint = candidateCheckpoint;
            _appliedOutOfOrderSequences = candidateOutOfOrderSequences;
            return true;
        }
    }

    public bool TryAssignCaptureId(string filePath, CaptureId captureId)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("Capture identity cannot be empty.", nameof(captureId));
        }

        if (!TryNormalizePath(filePath, out string normalizedPath))
        {
            return false;
        }

        bool changed = false;
        lock (_sync)
        {
            EnsureLoaded();
            int index = FindEntryIndex(normalizedPath);
            if (index >= 0 &&
                _entries[index] is { Origin: RecentCaptureOrigin.Captured, CaptureId: null } entry)
            {
                List<RecentCaptureCatalogEntry> candidateEntries = [.. _entries];
                candidateEntries[index] = entry with { CaptureId = captureId };
                HashSet<string> candidateExclusions = new(
                    _retainedCaptureRecoveryExclusions,
                    StringComparer.OrdinalIgnoreCase);
                candidateExclusions.Remove(normalizedPath);
                if (!TrySave(
                    candidateEntries,
                    _captureAssetChangeCheckpoint,
                    _appliedOutOfOrderSequences,
                    candidateExclusions))
                {
                    return false;
                }

                _entries = candidateEntries;
                _retainedCaptureRecoveryExclusions = candidateExclusions;
                changed = true;
            }
        }

        if (changed)
        {
            _changeNotifier.NotifyRecentCapturesChanged();
        }

        return changed;
    }

    public bool TryRepairCapturedProjection(
        string oldFilePath,
        string newFilePath,
        CaptureFileType captureFileType,
        CaptureId captureId,
        DateTime activityUtc)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("Capture identity cannot be empty.", nameof(captureId));
        }

        if (captureFileType == CaptureFileType.Unknown ||
            activityUtc.Kind != DateTimeKind.Utc ||
            !TryNormalizePath(oldFilePath, out string oldPath) ||
            !TryNormalizePath(newFilePath, out string newPath))
        {
            return false;
        }

        bool changed;
        lock (_sync)
        {
            EnsureLoaded();
            var projectedEntry = new RecentCaptureCatalogEntry(
                newPath,
                captureFileType,
                RecentCaptureOrigin.Captured,
                activityUtc,
                captureId);
            List<RecentCaptureCatalogEntry> candidateEntries = [.. _entries];
            List<RecentCaptureCatalogEntry> matchingEntries = candidateEntries
                .Where(entry =>
                    entry.CaptureId == captureId ||
                    string.Equals(entry.FilePath, oldPath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entry.FilePath, newPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
            changed = matchingEntries.Count != 1 || matchingEntries[0] != projectedEntry;
            if (!changed)
            {
                return true;
            }

            candidateEntries.RemoveAll(entry =>
                entry.CaptureId == captureId ||
                string.Equals(entry.FilePath, oldPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.FilePath, newPath, StringComparison.OrdinalIgnoreCase));
            candidateEntries.Add(projectedEntry);
            HashSet<string> candidateExclusions = new(
                _retainedCaptureRecoveryExclusions,
                StringComparer.OrdinalIgnoreCase);
            candidateExclusions.Remove(oldPath);
            candidateExclusions.Remove(newPath);
            TrimEntries(candidateEntries, candidateExclusions);
            if (!TrySave(
                candidateEntries,
                _captureAssetChangeCheckpoint,
                _appliedOutOfOrderSequences,
                candidateExclusions))
            {
                return false;
            }

            _entries = candidateEntries;
            _retainedCaptureRecoveryExclusions = candidateExclusions;
        }

        if (changed)
        {
            _changeNotifier.NotifyRecentCapturesChanged();
        }

        return changed;
    }

    public void ReplacePath(string oldFilePath, string newFilePath)
    {
        if (!TryNormalizePath(oldFilePath, out string oldPath) ||
            !TryNormalizePath(newFilePath, out string newPath))
        {
            return;
        }

        lock (_sync)
        {
            EnsureLoaded();
            int oldIndex = FindEntryIndex(oldPath);
            RecentCaptureCatalogEntry replacement = oldIndex >= 0
                ? _entries[oldIndex] with { FilePath = newPath, LastActivityUtc = _clock.UtcNow }
                : new(
                    newPath,
                    CaptureFileTypeDetector.DetectFileType(newPath),
                    RecentCaptureOrigin.Captured,
                    _clock.UtcNow);

            if (oldIndex >= 0)
            {
                _entries.RemoveAt(oldIndex);
            }

            int newIndex = FindEntryIndex(newPath);
            if (newIndex >= 0)
            {
                _entries.RemoveAt(newIndex);
            }

            _entries.Add(replacement);
            if (replacement.CaptureId is not null)
            {
                _retainedCaptureRecoveryExclusions.Remove(oldPath);
                _retainedCaptureRecoveryExclusions.Remove(newPath);
            }

            TrimEntries();
            Save();
        }

        _changeNotifier.NotifyRecentCapturesChanged();
    }

    public void Touch(string filePath)
    {
        if (!TryNormalizePath(filePath, out string normalizedPath))
        {
            return;
        }

        bool changed = false;
        lock (_sync)
        {
            EnsureLoaded();
            int index = FindEntryIndex(normalizedPath);
            if (index >= 0)
            {
                _entries[index] = _entries[index] with { LastActivityUtc = _clock.UtcNow };
                Save();
                changed = true;
            }
        }

        if (changed)
        {
            _changeNotifier.NotifyRecentCapturesChanged();
        }
    }

    public bool Remove(string filePath)
    {
        return RemoveRange([filePath]) > 0;
    }

    public int RemoveRange(IEnumerable<string> filePaths)
    {
        HashSet<string> normalizedPaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (string filePath in filePaths)
        {
            if (TryNormalizePath(filePath, out string normalizedPath))
            {
                normalizedPaths.Add(normalizedPath);
            }
        }

        int removedCount;
        lock (_sync)
        {
            EnsureLoaded();
            List<RecentCaptureCatalogEntry> candidateEntries = [.. _entries];
            List<RecentCaptureCatalogEntry> removedEntries = candidateEntries
                .Where(entry => normalizedPaths.Contains(entry.FilePath))
                .ToList();
            removedCount = candidateEntries.RemoveAll(entry => normalizedPaths.Contains(entry.FilePath));
            if (removedCount == 0)
            {
                return 0;
            }

            HashSet<string> candidateExclusions = new(
                _retainedCaptureRecoveryExclusions,
                StringComparer.OrdinalIgnoreCase);
            AddRetainedRecoveryExclusions(removedEntries, candidateExclusions);
            if (!TrySave(
                candidateEntries,
                _captureAssetChangeCheckpoint,
                _appliedOutOfOrderSequences,
                candidateExclusions))
            {
                return 0;
            }

            _entries = candidateEntries;
            _retainedCaptureRecoveryExclusions = candidateExclusions;
        }

        if (removedCount > 0)
        {
            _changeNotifier.NotifyRecentCapturesChanged();
        }

        return removedCount;
    }

    public void Clear()
    {
        bool changed;
        lock (_sync)
        {
            EnsureLoaded();
            changed = _entries.Count > 0;
            if (!changed)
            {
                return;
            }

            HashSet<string> candidateExclusions = new(
                _retainedCaptureRecoveryExclusions,
                StringComparer.OrdinalIgnoreCase);
            AddRetainedRecoveryExclusions(_entries, candidateExclusions);
            if (!TrySave(
                [],
                _captureAssetChangeCheckpoint,
                _appliedOutOfOrderSequences,
                candidateExclusions))
            {
                return;
            }

            _entries = [];
            _retainedCaptureRecoveryExclusions = candidateExclusions;
        }

        if (changed)
        {
            _changeNotifier.NotifyRecentCapturesChanged();
        }
    }

    public void Clear(long throughChangeSequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(throughChangeSequence);

        bool entriesChanged;
        lock (_sync)
        {
            EnsureLoaded();
            long candidateCheckpoint = Math.Max(
                _captureAssetChangeCheckpoint,
                throughChangeSequence);
            // A projection acknowledged before this lock was acquired is part of the history
            // being cleared, even when its sequence is newer than the caller's snapshot.
            SortedSet<long> candidateOutOfOrderSequences = new(
                _appliedOutOfOrderSequences.Where(sequence => sequence > candidateCheckpoint));
            AdvanceCheckpointAcrossAppliedSequences(
                ref candidateCheckpoint,
                candidateOutOfOrderSequences);
            entriesChanged = _entries.Count > 0;
            bool checkpointChanged = candidateCheckpoint != _captureAssetChangeCheckpoint ||
                candidateOutOfOrderSequences.Count != _appliedOutOfOrderSequences.Count;
            if (!entriesChanged && !checkpointChanged)
            {
                return;
            }

            HashSet<string> candidateExclusions = new(
                _retainedCaptureRecoveryExclusions,
                StringComparer.OrdinalIgnoreCase);
            AddRetainedRecoveryExclusions(_entries, candidateExclusions);
            if (!TrySave(
                [],
                candidateCheckpoint,
                candidateOutOfOrderSequences,
                candidateExclusions))
            {
                return;
            }

            _entries = [];
            _captureAssetChangeCheckpoint = candidateCheckpoint;
            _appliedOutOfOrderSequences = candidateOutOfOrderSequences;
            _retainedCaptureRecoveryExclusions = candidateExclusions;
        }

        if (entriesChanged)
        {
            _changeNotifier.NotifyRecentCapturesChanged();
        }
    }

    private void Record(
        string filePath,
        CaptureFileType captureFileType,
        RecentCaptureOrigin origin,
        CaptureId? captureId)
    {
        if (captureFileType == CaptureFileType.Unknown ||
            !TryNormalizePath(filePath, out string normalizedPath))
        {
            return;
        }

        lock (_sync)
        {
            EnsureLoaded();
            int index = FindEntryIndex(normalizedPath);
            if (index >= 0)
            {
                RecentCaptureCatalogEntry existing = _entries[index];
                RecentCaptureOrigin recordedOrigin = existing.Origin == RecentCaptureOrigin.Captured
                    ? RecentCaptureOrigin.Captured
                    : origin;
                CaptureId? recordedCaptureId = recordedOrigin == RecentCaptureOrigin.Captured
                    ? captureId ?? existing.CaptureId
                    : null;
                _entries[index] = existing with
                {
                    CaptureFileType = captureFileType,
                    Origin = recordedOrigin,
                    LastActivityUtc = _clock.UtcNow,
                    CaptureId = recordedCaptureId,
                };
            }
            else
            {
                _entries.Add(new(
                    normalizedPath,
                    captureFileType,
                    origin,
                    _clock.UtcNow,
                    origin == RecentCaptureOrigin.Captured ? captureId : null));
            }

            TrimEntries();
            RecentCaptureCatalogEntry? recordedEntry = _entries.Find(entry =>
                string.Equals(entry.FilePath, normalizedPath, StringComparison.OrdinalIgnoreCase));
            if (recordedEntry?.CaptureId is not null)
            {
                _retainedCaptureRecoveryExclusions.Remove(normalizedPath);
            }

            Save();
        }

        _changeNotifier.NotifyRecentCapturesChanged();
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
            string json = File.ReadAllText(catalogFilePath);
            List<RecentCaptureCatalogEntry> storedEntries;
            long checkpoint;
            SortedSet<long> outOfOrderSequences;
            HashSet<string> recoveryExclusions;
            bool recoveryDisabled;
            ReadOnlySpan<char> jsonContent = json.AsSpan().TrimStart();
            if (jsonContent.IsEmpty)
            {
                throw new InvalidDataException("The recent captures catalog is empty.");
            }

            if (jsonContent[0] == '[')
            {
                storedEntries = JsonSerializer.Deserialize(
                    json,
                    RecentCaptureCatalogContext.Default.ListRecentCaptureCatalogEntry) ??
                    throw new InvalidDataException("The legacy recent captures catalog is invalid.");
                checkpoint = 0;
                outOfOrderSequences = [];
                recoveryExclusions = new(StringComparer.OrdinalIgnoreCase);
                recoveryDisabled = false;
            }
            else
            {
                RecentCaptureCatalogEnvelope envelope = JsonSerializer.Deserialize(
                    json,
                    RecentCaptureCatalogContext.Default.RecentCaptureCatalogEnvelope) ??
                    throw new InvalidDataException("The recent captures catalog is invalid.");
                if (envelope.SchemaVersion != CurrentSchemaVersion ||
                    envelope.AssetChangeCheckpoint < 0 ||
                    !AreValidOutOfOrderSequences(
                        envelope.AppliedOutOfOrderSequences,
                        envelope.AssetChangeCheckpoint))
                {
                    throw new InvalidDataException("The recent captures catalog schema is unsupported or invalid.");
                }

                storedEntries = envelope.Entries;
                checkpoint = envelope.AssetChangeCheckpoint;
                outOfOrderSequences = new(envelope.AppliedOutOfOrderSequences);
                recoveryExclusions = ReadRecoveryExclusions(
                    envelope.ProtectedRetainedCaptureRecoveryExclusions,
                    out bool recoveryExclusionsUnreadable)
                    .Where(path => TryNormalizePath(path, out _))
                    .Select(Path.GetFullPath)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                recoveryDisabled = envelope.RetainedCaptureRecoveryDisabled ||
                    recoveryExclusionsUnreadable;
                if (recoveryExclusions.Count > MaximumEntryCount)
                {
                    recoveryExclusions.Clear();
                    recoveryDisabled = true;
                }
            }

            _retainedCaptureRecoveryDisabled = recoveryDisabled;
            List<RecentCaptureCatalogEntry> normalizedEntries = storedEntries
                .Where(entry =>
                    entry.CaptureFileType != CaptureFileType.Unknown &&
                    TryNormalizePath(entry.FilePath, out _))
                .Select(entry => entry with
                {
                    FilePath = Path.GetFullPath(entry.FilePath),
                    CaptureId = entry.Origin == RecentCaptureOrigin.Captured ? entry.CaptureId : null,
                })
                .GroupBy(entry => entry.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.MaxBy(entry => entry.LastActivityUtc)!)
                .OrderByDescending(entry => entry.LastActivityUtc)
                .ToList();
            _entries = normalizedEntries.Take(MaximumEntryCount).ToList();
            AddRetainedRecoveryExclusions(
                normalizedEntries.Skip(MaximumEntryCount),
                recoveryExclusions);
            _captureAssetChangeCheckpoint = checkpoint;
            _appliedOutOfOrderSequences = outOfOrderSequences;
            _retainedCaptureRecoveryExclusions = recoveryExclusions;
        }
        catch (Exception ex)
        {
            _entries = [];
            _captureAssetChangeCheckpoint = 0;
            _appliedOutOfOrderSequences = [];
            _retainedCaptureRecoveryExclusions = new(StringComparer.OrdinalIgnoreCase);
            _retainedCaptureRecoveryDisabled = true;
            _logService.LogException(ex, "Failed to load the recent captures catalog.");
        }
    }

    private void Save()
    {
        _ = TrySave(
            _entries,
            _captureAssetChangeCheckpoint,
            _appliedOutOfOrderSequences);
    }

    private bool TrySave(
        IReadOnlyList<RecentCaptureCatalogEntry> entries,
        long checkpoint,
        IReadOnlyCollection<long> appliedOutOfOrderSequences,
        IReadOnlyCollection<string>? recoveryExclusions = null)
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

            IReadOnlyCollection<string> selectedRecoveryExclusions =
                recoveryExclusions ?? _retainedCaptureRecoveryExclusions;
            bool recoveryDisabled = _retainedCaptureRecoveryDisabled ||
                selectedRecoveryExclusions.Count > MaximumEntryCount;
            string? protectedRecoveryExclusions = recoveryDisabled ||
                selectedRecoveryExclusions.Count == 0
                ? null
                : ProtectRecoveryExclusions(selectedRecoveryExclusions);
            var envelope = new RecentCaptureCatalogEnvelope
            {
                SchemaVersion = CurrentSchemaVersion,
                AssetChangeCheckpoint = checkpoint,
                AppliedOutOfOrderSequences = [.. appliedOutOfOrderSequences.OrderBy(sequence => sequence)],
                Entries = [.. entries],
                ProtectedRetainedCaptureRecoveryExclusions = protectedRecoveryExclusions,
                RetainedCaptureRecoveryDisabled = recoveryDisabled,
            };
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(
                envelope,
                RecentCaptureCatalogContext.Default.RecentCaptureCatalogEnvelope);
            using (var stream = new FileStream(
                temporaryFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                stream.Write(json);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryFilePath, catalogFilePath, true);
            _retainedCaptureRecoveryDisabled = recoveryDisabled;
            return true;
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to save the recent captures catalog.");
            try
            {
                File.Delete(temporaryFilePath);
            }
            catch (Exception cleanupException)
            {
                _logService.LogException(
                    cleanupException,
                    "Failed to clean up a recent captures catalog write.");
            }

            return false;
        }
    }

    private void TrimEntries()
    {
        TrimEntries(_entries, _retainedCaptureRecoveryExclusions);
    }

    private bool TryCreateAcknowledgedSequenceState(
        long changeSequence,
        out long checkpoint,
        out SortedSet<long> outOfOrderSequences)
    {
        checkpoint = _captureAssetChangeCheckpoint;
        outOfOrderSequences = new(_appliedOutOfOrderSequences);
        if (changeSequence <= checkpoint || outOfOrderSequences.Contains(changeSequence))
        {
            return false;
        }

        if (changeSequence == checkpoint + 1)
        {
            checkpoint = changeSequence;
            AdvanceCheckpointAcrossAppliedSequences(ref checkpoint, outOfOrderSequences);
        }
        else
        {
            outOfOrderSequences.Add(changeSequence);
        }

        return true;
    }

    private static void AdvanceCheckpointAcrossAppliedSequences(
        ref long checkpoint,
        SortedSet<long> outOfOrderSequences)
    {
        while (checkpoint < long.MaxValue && outOfOrderSequences.Remove(checkpoint + 1))
        {
            checkpoint++;
        }
    }

    private bool IsChangeSequenceApplied(long changeSequence)
    {
        return changeSequence <= _captureAssetChangeCheckpoint ||
            _appliedOutOfOrderSequences.Contains(changeSequence);
    }

    private static bool ProjectCapturedEntry(
        List<RecentCaptureCatalogEntry> entries,
        string filePath,
        CaptureFileType captureFileType,
        CaptureId captureId,
        DateTime activityUtc)
    {
        var projectedEntry = new RecentCaptureCatalogEntry(
            filePath,
            captureFileType,
            RecentCaptureOrigin.Captured,
            activityUtc,
            captureId);
        List<RecentCaptureCatalogEntry> matchingEntries = entries
            .Where(entry =>
                entry.CaptureId == captureId ||
                string.Equals(entry.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matchingEntries.Count == 1 && matchingEntries[0] == projectedEntry)
        {
            return false;
        }

        entries.RemoveAll(entry =>
            entry.CaptureId == captureId ||
            string.Equals(entry.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        entries.Add(projectedEntry);
        return true;
    }

    private void TrimEntries(
        List<RecentCaptureCatalogEntry> entries,
        HashSet<string> recoveryExclusions)
    {
        if (entries.Count <= MaximumEntryCount)
        {
            return;
        }

        List<RecentCaptureCatalogEntry> retainedEntries = entries
            .OrderByDescending(entry => entry.LastActivityUtc)
            .Take(MaximumEntryCount)
            .ToList();
        AddRetainedRecoveryExclusions(entries.Except(retainedEntries), recoveryExclusions);
        entries.Clear();
        entries.AddRange(retainedEntries);
    }

    private void AddRetainedRecoveryExclusions(
        IEnumerable<RecentCaptureCatalogEntry> entries,
        HashSet<string> recoveryExclusions)
    {
        string retainedFolderPath;
        try
        {
            retainedFolderPath = _storageService.GetApplicationRetainedCaptureFolderPath();
        }
        catch
        {
            recoveryExclusions.Clear();
            _retainedCaptureRecoveryDisabled = true;
            return;
        }

        foreach (RecentCaptureCatalogEntry entry in entries)
        {
            if (_retainedCaptureRecoveryDisabled)
            {
                recoveryExclusions.Clear();
                return;
            }

            if ((entry.Origin == RecentCaptureOrigin.Opened || entry.CaptureId is null) &&
                IsPathWithinFolder(entry.FilePath, retainedFolderPath))
            {
                recoveryExclusions.Add(entry.FilePath);
                if (recoveryExclusions.Count > MaximumEntryCount)
                {
                    recoveryExclusions.Clear();
                    _retainedCaptureRecoveryDisabled = true;
                    return;
                }
            }
        }
    }

    private HashSet<string> ReadRecoveryExclusions(
        string? protectedValue,
        out bool unreadable)
    {
        unreadable = false;
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }

        byte[]? protectedBytes = null;
        byte[]? plaintext = null;
        try
        {
            protectedBytes = Convert.FromBase64String(protectedValue);
            plaintext = _dataProtectionService.Unprotect(protectedBytes);
            List<string> paths = JsonSerializer.Deserialize(
                plaintext,
                RecentCaptureCatalogContext.Default.ListString) ??
                throw new InvalidDataException("The retained capture recovery data is invalid.");
            return paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            unreadable = true;
            _logService.LogException(ex, "Failed to load retained capture recovery data.");
            return new(StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (plaintext is not null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }

            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }
        }
    }

    private string ProtectRecoveryExclusions(IReadOnlyCollection<string> recoveryExclusions)
    {
        List<string> orderedPaths =
            [.. recoveryExclusions.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)];
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(
            orderedPaths,
            RecentCaptureCatalogContext.Default.ListString);
        byte[]? protectedBytes = null;
        try
        {
            protectedBytes = _dataProtectionService.Protect(plaintext);
            return Convert.ToBase64String(protectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }
        }
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

    private static bool AreValidOutOfOrderSequences(
        IReadOnlyList<long> sequences,
        long checkpoint)
    {
        if (sequences.Count > 0 &&
            checkpoint < long.MaxValue &&
            sequences[0] == checkpoint + 1)
        {
            return false;
        }

        long previous = checkpoint;
        foreach (long sequence in sequences)
        {
            if (sequence <= previous)
            {
                return false;
            }

            previous = sequence;
        }

        return true;
    }

    private int FindEntryIndex(string filePath)
    {
        return _entries.FindIndex(entry =>
            string.Equals(entry.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
    }

    private string GetCatalogFilePath()
    {
        return Path.Combine(_storageService.GetApplicationDataFolderPath(), CatalogFileName);
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
}
