using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Time;

namespace CaptureTool.Application.Storage;

internal sealed class ScratchArtifactStore : IScratchArtifactStore
{
    private readonly Lock _lock = new();
    private readonly HashSet<string> _activeOwnerDirectories = new(StringComparer.OrdinalIgnoreCase);
    private readonly IStorageService _storageService;
    private readonly IFileSystem _fileSystem;
    private readonly IClock _clock;
    private readonly ILogService _logService;

    public ScratchArtifactStore(
        IStorageService storageService,
        IFileSystem fileSystem,
        IClock clock,
        ILogService logService)
    {
        _storageService = storageService;
        _fileSystem = fileSystem;
        _clock = clock;
        _logService = logService;
    }

    public string CreateLeasedArtifactPath(string owner, string extension) => CreateLeasedPath(owner, extension, null);

    public string CreateLeasedArtifactPath(string owner, string extension, int maximumRetainedArtifacts)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRetainedArtifacts);
        return CreateLeasedPath(owner, extension, maximumRetainedArtifacts);
    }

    private string CreateLeasedPath(string owner, string extension, int? maximumRetainedArtifacts)
    {
        string rootPath = GetNormalizedRootPath();
        _fileSystem.CreateDirectory(rootPath);

        string ownerName = NormalizeOwnerName(owner);
        string ownerDirectory = Path.Combine(rootPath, $"{ownerName}-{Guid.NewGuid():N}");
        lock (_lock)
        {
            if (maximumRetainedArtifacts is { } limit) PruneOwnerArtifacts(rootPath, ownerName, limit);
            _activeOwnerDirectories.Add(ownerDirectory);
        }

        try
        {
            _fileSystem.CreateDirectory(ownerDirectory);

            string normalizedExtension = string.IsNullOrWhiteSpace(extension)
                ? string.Empty
                : extension.StartsWith('.') ? extension : $".{extension}";
            string fileName = $"{Path.GetFileNameWithoutExtension(_storageService.GetTemporaryFileName())}{normalizedExtension}";
            return Path.Combine(ownerDirectory, fileName);
        }
        catch
        {
            lock (_lock)
            {
                _activeOwnerDirectories.Remove(ownerDirectory);
            }
            TryDeleteEntry(ownerDirectory);
            throw;
        }
    }

    // Called under the lease lock so concurrent allocations cannot prune each other or exceed the limit.
    private void PruneOwnerArtifacts(string rootPath, string ownerName, int limit)
    {
        string prefix = ownerName + "-";
        bool IsOwned(string entry)
        {
            string name = Path.GetFileName(entry);
            return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Guid.TryParseExact(name[prefix.Length..], "N", out _);
        }
        // Include reserved directories that an allocating caller has not created yet.
        int retained = _activeOwnerDirectories.Count(IsOwned);
        foreach (string entry in _fileSystem.EnumerateFileSystemEntries(rootPath))
        {
            if (!IsOwned(entry) || _activeOwnerDirectories.Contains(entry)) continue;
            TryDeleteEntry(entry);
            if (_fileSystem.DirectoryExists(entry) || _fileSystem.FileExists(entry)) retained++;
        }
        if (retained >= limit) throw new IOException("Pending scratch cleanup exceeds its bound.");
    }

    public void DeleteArtifact(string artifactPath)
    {
        string? ownerDirectory = TryGetOwnerDirectory(artifactPath);
        if (ownerDirectory is null)
        {
            return;
        }

        lock (_lock)
        {
            _activeOwnerDirectories.Remove(ownerDirectory);
        }

        TryDeleteEntry(ownerDirectory);
    }

    public void RelinquishArtifact(string artifactPath)
    {
        string? ownerDirectory = TryGetOwnerDirectory(artifactPath);
        if (ownerDirectory is null)
        {
            return;
        }

        lock (_lock)
        {
            _activeOwnerDirectories.Remove(ownerDirectory);
        }
    }

    public void ClearUnleasedArtifacts()
    {
        string rootPath = GetNormalizedRootPath();
        if (!_fileSystem.DirectoryExists(rootPath))
        {
            return;
        }

        foreach (string entry in _fileSystem.EnumerateFileSystemEntries(rootPath))
        {
            if (!IsActive(entry))
            {
                TryDeleteEntry(entry);
            }
        }
    }

    public void ScavengeStaleArtifacts(TimeSpan maximumAge)
    {
        string rootPath = GetNormalizedRootPath();
        if (!_fileSystem.DirectoryExists(rootPath))
        {
            return;
        }

        DateTime cutoffUtc = _clock.UtcNow - maximumAge;
        foreach (string entry in _fileSystem.EnumerateFileSystemEntries(rootPath))
        {
            if (IsActive(entry))
            {
                continue;
            }

            try
            {
                if (_fileSystem.GetLastWriteTimeUtc(entry) < cutoffUtc)
                {
                    TryDeleteEntry(entry);
                }
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, $"Failed to inspect scratch artifact: {entry}");
            }
        }
    }

    private bool IsActive(string entry)
    {
        string normalizedEntry = Path.GetFullPath(entry)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        lock (_lock)
        {
            return _activeOwnerDirectories.Contains(normalizedEntry);
        }
    }

    private string? TryGetOwnerDirectory(string artifactPath)
    {
        if (string.IsNullOrWhiteSpace(artifactPath))
        {
            return null;
        }

        string rootPath = GetNormalizedRootPath();
        string rootPrefix = rootPath + Path.DirectorySeparatorChar;
        string fullArtifactPath;
        try
        {
            fullArtifactPath = Path.GetFullPath(artifactPath);
        }
        catch
        {
            return null;
        }

        if (!fullArtifactPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string relativePath = fullArtifactPath[rootPrefix.Length..];
        int separatorIndex = relativePath.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        if (separatorIndex <= 0)
        {
            return null;
        }

        return Path.Combine(rootPath, relativePath[..separatorIndex]);
    }

    private string GetNormalizedRootPath()
    {
        return Path.GetFullPath(_storageService.GetApplicationScratchFolderPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private void TryDeleteEntry(string entry)
    {
        try
        {
            if (_fileSystem.DirectoryExists(entry))
            {
                _fileSystem.DeleteDirectory(entry, true);
            }
            else if (_fileSystem.FileExists(entry))
            {
                _fileSystem.DeleteFile(entry);
            }
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, $"Failed to delete scratch artifact: {entry}");
        }
    }

    private static string NormalizeOwnerName(string owner)
    {
        string value = string.IsNullOrWhiteSpace(owner) ? "artifact" : owner.Trim();
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidCharacter, '-');
        }

        return value;
    }
}
