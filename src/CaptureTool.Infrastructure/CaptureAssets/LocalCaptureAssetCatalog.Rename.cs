using CaptureTool.Domain;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.CaptureAssets.Serialization;

namespace CaptureTool.Infrastructure.CaptureAssets;

internal sealed partial class LocalCaptureAssetCatalog
{
    public async Task<string> RenameFileAsync(CaptureId id, string path, string fileName, CancellationToken cancellationToken = default)
    {
        path = NormalizePath(path);
        if (Path.GetFileName(fileName) != fileName || string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(Path.GetExtension(path), Path.GetExtension(fileName), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Rename must preserve the extension and directory.", nameof(fileName));
        string destination = Path.Combine(Path.GetDirectoryName(path)!, fileName);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Catalog state = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var rename = new CaptureRenameDocument(id.Value, path, destination);
            Catalog updated = ApplyRename(state, rename);
            if (!File.Exists(path)) throw new FileNotFoundException("Capture file is unavailable.");
            if (string.Equals(path, destination, StringComparison.Ordinal))
            {
                await SaveAsync(updated, cancellationToken).ConfigureAwait(false);
                return path;
            }
            if (!string.Equals(path, destination, StringComparison.OrdinalIgnoreCase) && (File.Exists(destination) || Directory.Exists(destination))) throw new IOException("The destination already exists.");
            // Persist intent before the only filesystem mutation. Recovery never repeats a move
            // or overwrites a file; it reconciles the location that survived an interruption.
            await SaveAsync(state with { Rename = rename }, cancellationToken).ConfigureAwait(false);
            try { File.Move(path, destination, overwrite: false); }
            catch
            {
                await SaveAsync(state, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
            try
            {
                _recents?.ReplacePath(path, destination);
                await SaveAsync(updated, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The durable intent lets the next catalog read finish publication. The file
                // has moved, so callers must receive its actual path even if storage went away.
            }
            return destination;
        }
        finally { _gate.Release(); }
    }

    private static Catalog ApplyRename(Catalog state, CaptureRenameDocument rename)
    {
        string oldPath = NormalizePath(rename.OldPath), newPath = NormalizePath(rename.NewPath);
        if (!string.Equals(Path.GetDirectoryName(oldPath), Path.GetDirectoryName(newPath), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetExtension(oldPath), Path.GetExtension(newPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid rename intent.");
        int index = state.Entries.FindIndex(entry => entry.Asset.Id.Value == rename.Id);
        if (index < 0) throw new InvalidDataException("Rename capture is missing.");
        var entry = state.Entries[index];
        if (state.Entries.Any(other => other.Asset.Id != entry.Asset.Id &&
            (string.Equals(other.Asset.SourcePath, newPath, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(other.Asset.PreferredPath, newPath, StringComparison.OrdinalIgnoreCase))))
            throw new IOException("The destination belongs to another capture.");
        bool source = string.Equals(entry.Asset.SourcePath, oldPath, StringComparison.OrdinalIgnoreCase);
        bool preferred = string.Equals(entry.Asset.PreferredPath, oldPath, StringComparison.OrdinalIgnoreCase);
        if (!source && !preferred) throw new InvalidDataException("Rename source no longer matches.");
        var asset = new CaptureAsset(entry.Asset.Id, entry.Asset.MediaType, entry.Asset.CapturedAt,
            source ? newPath : entry.Asset.SourcePath, entry.Asset.SourceOwnership,
            preferred ? newPath : entry.Asset.PreferredPath, new CaptureName(Path.GetFileNameWithoutExtension(newPath), false));
        EnsureLocationAvailable(state.Entries, asset);
        var entries = state.Entries.ToList();
        entries[index] = entry with { Asset = asset, NamingEpoch = null };
        return state with { Entries = entries, Rename = null };
    }

    private async Task<Catalog> RecoverRenameAsync(Catalog state, CancellationToken ct)
    {
        if (state.Rename is not { } rename) return state;
        Catalog renamed = ApplyRename(state, rename); // Validate intent even when the move did not happen.
        bool moved = !File.Exists(rename.OldPath) && File.Exists(rename.NewPath);
        if (string.Equals(rename.OldPath, rename.NewPath, StringComparison.OrdinalIgnoreCase))
            moved = Directory.Exists(Path.GetDirectoryName(rename.NewPath)) && Directory.EnumerateFiles(Path.GetDirectoryName(rename.NewPath)!).Any(path => string.Equals(path, rename.NewPath, StringComparison.Ordinal));
        Catalog recovered = moved ? renamed : state with { Rename = null };
        if (moved) _recents?.ReplacePath(rename.OldPath, rename.NewPath);
        await SaveAsync(recovered, ct).ConfigureAwait(false);
        return recovered;
    }
}
