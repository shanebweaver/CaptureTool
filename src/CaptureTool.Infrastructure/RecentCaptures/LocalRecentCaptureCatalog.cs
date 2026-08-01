using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.RecentCaptures.Serialization;
using System.Text.Json;

namespace CaptureTool.Infrastructure.RecentCaptures;

internal sealed class LocalRecentCaptureCatalog : IRecentCaptureCatalog
{
    private const int MaximumEntryCount = 1000;
    private const string CatalogFileName = "RecentCaptures.json";

    private readonly Lock _sync = new();
    private readonly IStorageService _storageService;
    private readonly IClock _clock;
    private readonly ILogService _logService;
    private readonly IRecentCapturesChangeNotifier _changeNotifier;
    private List<RecentCaptureCatalogEntry> _entries = [];
    private bool _isLoaded;

    public LocalRecentCaptureCatalog(
        IStorageService storageService,
        IClock clock,
        ILogService logService,
        IRecentCapturesChangeNotifier changeNotifier)
    {
        _storageService = storageService;
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
        Record(filePath, captureFileType, RecentCaptureOrigin.Captured);
    }

    public void RecordOpened(string filePath, CaptureFileType captureFileType)
    {
        Record(filePath, captureFileType, RecentCaptureOrigin.Opened);
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
            removedCount = _entries.RemoveAll(entry => normalizedPaths.Contains(entry.FilePath));
            if (removedCount > 0)
            {
                Save();
            }
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
            if (changed)
            {
                _entries.Clear();
                Save();
            }
        }

        if (changed)
        {
            _changeNotifier.NotifyRecentCapturesChanged();
        }
    }

    private void Record(
        string filePath,
        CaptureFileType captureFileType,
        RecentCaptureOrigin origin)
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
                _entries[index] = existing with
                {
                    CaptureFileType = captureFileType,
                    Origin = recordedOrigin,
                    LastActivityUtc = _clock.UtcNow,
                };
            }
            else
            {
                _entries.Add(new(normalizedPath, captureFileType, origin, _clock.UtcNow));
            }

            TrimEntries();
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
            List<RecentCaptureCatalogEntry>? storedEntries = JsonSerializer.Deserialize(
                json,
                RecentCaptureCatalogContext.Default.ListRecentCaptureCatalogEntry);
            if (storedEntries is null)
            {
                return;
            }

            _entries = storedEntries
                .Where(entry =>
                    entry.CaptureFileType != CaptureFileType.Unknown &&
                    TryNormalizePath(entry.FilePath, out _))
                .Select(entry => entry with { FilePath = Path.GetFullPath(entry.FilePath) })
                .GroupBy(entry => entry.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.MaxBy(entry => entry.LastActivityUtc)!)
                .OrderByDescending(entry => entry.LastActivityUtc)
                .Take(MaximumEntryCount)
                .ToList();
        }
        catch (Exception ex)
        {
            _entries = [];
            _logService.LogException(ex, "Failed to load the recent captures catalog.");
        }
    }

    private void Save()
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

            string json = JsonSerializer.Serialize(
                _entries,
                RecentCaptureCatalogContext.Default.ListRecentCaptureCatalogEntry);
            File.WriteAllText(temporaryFilePath, json);
            File.Move(temporaryFilePath, catalogFilePath, true);
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to save the recent captures catalog.");
        }
    }

    private void TrimEntries()
    {
        if (_entries.Count <= MaximumEntryCount)
        {
            return;
        }

        _entries = _entries
            .OrderByDescending(entry => entry.LastActivityUtc)
            .Take(MaximumEntryCount)
            .ToList();
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
