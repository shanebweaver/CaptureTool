using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.CaptureAssets.Serialization;
using CaptureTool.Infrastructure.Persistence;

namespace CaptureTool.Infrastructure.CaptureAssets;

internal sealed partial class LocalCaptureAssetCatalog : ICaptureAssetCatalog, ICaptureNameStore, IDisposable
{
    private readonly string _path;
    private readonly ProtectedDocumentFile _documents;
    private readonly IRecentCaptureCatalog? _recents;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalCaptureAssetCatalog(IStorageService storage, IUserDataProtector protector, IRecentCaptureCatalog? recents = null)
        : this(storage, protector, new LocalProtectedFileSystem(), recents) { }

    internal LocalCaptureAssetCatalog(IStorageService storage, IUserDataProtector protector, IProtectedFileSystem files, IRecentCaptureCatalog? recents = null)
    {
        _recents = recents;
        _path = Path.Combine(storage.GetApplicationDataFolderPath(), "CaptureAssets", "catalog.bin");
        _documents = new(protector, files);
    }

    public async Task RegisterAsync(CaptureAsset asset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        CaptureAsset normalized = Normalize(asset);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Catalog state = await LoadAsync(cancellationToken).ConfigureAwait(false);
            CaptureAsset? existing = state.Entries.Find(item => item.Id == normalized.Id);
            if (existing != null)
            {
                if (existing.MediaType != normalized.MediaType || existing.CapturedAt != normalized.CapturedAt ||
                    existing.SourceOwnership != normalized.SourceOwnership ||
                    !string.Equals(existing.SourcePath, normalized.SourcePath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Capture identity is already registered differently.");
                return; // Replays cannot undo a saved location or accepted name.
            }
            EnsureLocationAvailable(state.Entries, normalized);
            state.Entries.Add(normalized);
            await SaveAsync(state, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<CaptureAsset?> GetAsync(CaptureId id, CancellationToken cancellationToken = default)
    {
        ValidateId(id);
        return (await ReadAllAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(asset => asset.Id == id);
    }

    public async Task<IReadOnlyList<CaptureAsset>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return (await LoadAsync(cancellationToken).ConfigureAwait(false)).Entries.AsReadOnly(); }
        finally { _gate.Release(); }
    }

    public Task SetPreferredPathAsync(CaptureId id, string? path, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, asset => asset.WithPreferredPath(path), cancellationToken);
    public Task RelocateSourceAsync(CaptureId id, string path, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, asset => asset.RelocateSource(path), cancellationToken);

    private async Task UpdateAsync(CaptureId id, Func<CaptureAsset, CaptureAsset> update, CancellationToken cancellationToken)
    {
        ValidateId(id);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Catalog state = await LoadAsync(cancellationToken).ConfigureAwait(false);
            int index = state.Entries.FindIndex(entry => entry.Id == id);
            if (index < 0) throw new KeyNotFoundException("Capture is not registered.");
            CaptureAsset updated = Normalize(update(state.Entries[index]));
            EnsureLocationAvailable(state.Entries, updated);
            state.Entries[index] = updated;
            await SaveAsync(state, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task<Catalog> LoadAsync(CancellationToken cancellationToken)
    {
        CaptureCatalogDocument? document = await _documents.ReadAsync(_path,
            CaptureCatalogJsonContext.Default.CaptureCatalogDocument, cancellationToken).ConfigureAwait(false);
        if (document == null) return new([]);
        if (document.Version != 6 || document.Assets == null) throw new InvalidDataException("Unsupported or invalid capture catalog.");
        Catalog state;
        try
        {
            var entries = document.Assets.Select(asset => asset == null
                ? throw new InvalidDataException("Missing capture asset.")
                : Normalize(new CaptureAsset(new CaptureId(asset.Id), (CaptureFileType)asset.MediaType,
                    asset.CapturedAt, asset.SourcePath, (CaptureSourceOwnership)asset.SourceOwnership,
                    asset.PreferredPath, asset.Name == null ? null : new CaptureName(asset.Name)))).ToList();
            if (entries.Select(entry => entry.Id).Distinct().Count() != entries.Count ||
                entries.Select(entry => entry.SourcePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Count)
                throw new InvalidDataException("Duplicate capture registration.");
            state = new(entries, document.Rename);
        }
        catch (ArgumentException exception) { throw new InvalidDataException("Invalid capture catalog.", exception); }
        return await RecoverRenameAsync(state, cancellationToken).ConfigureAwait(false);
    }

    private Task SaveAsync(Catalog state, CancellationToken cancellationToken) =>
        _documents.WriteAsync(_path, new CaptureCatalogDocument(6, state.Entries.Select(asset =>
            new CaptureAssetDocument(asset.Id.Value, (int)asset.MediaType, asset.CapturedAt,
                asset.SourcePath, (int)asset.SourceOwnership, asset.PreferredPath, asset.Name?.Text)).ToArray(), state.Rename),
            CaptureCatalogJsonContext.Default.CaptureCatalogDocument, cancellationToken);

    private static CaptureAsset Normalize(CaptureAsset asset) => new(asset.Id, asset.MediaType, asset.CapturedAt,
        NormalizePath(asset.SourcePath), asset.SourceOwnership, asset.PreferredPath == null ? null : NormalizePath(asset.PreferredPath), asset.Name);
    private static string NormalizePath(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Capture locations must be absolute.", nameof(path));
        return Path.GetFullPath(path);
    }
    private static void EnsureLocationAvailable(List<CaptureAsset> entries, CaptureAsset updated)
    {
        if (entries.Any(entry => entry.Id != updated.Id && string.Equals(entry.SourcePath, updated.SourcePath, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Source location belongs to another capture.");
    }
    private static void ValidateId(CaptureId id)
    {
        if (id.IsEmpty) throw new ArgumentException("Capture identity is required.", nameof(id));
    }
    public void Dispose() => _gate.Dispose();
    private sealed record Catalog(List<CaptureAsset> Entries, CaptureRenameDocument? Rename = null);
}
