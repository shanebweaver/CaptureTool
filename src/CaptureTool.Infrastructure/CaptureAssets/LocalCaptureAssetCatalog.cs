using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.CaptureAssets.Serialization;
using CaptureTool.Infrastructure.Persistence;

namespace CaptureTool.Infrastructure.CaptureAssets;

internal sealed class LocalCaptureAssetCatalog : ICaptureAssetCatalog, IDisposable
{
    private readonly string _path;
    private readonly ProtectedDocumentFile _documents;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalCaptureAssetCatalog(IStorageService storage, IUserDataProtector protector)
        : this(storage, protector, new LocalProtectedFileSystem()) { }

    internal LocalCaptureAssetCatalog(IStorageService storage, IUserDataProtector protector, IProtectedFileSystem files)
    {
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
            List<CaptureAsset> assets = await LoadAsync(cancellationToken).ConfigureAwait(false);
            CaptureAsset? existing = assets.Find(item => item.Id == normalized.Id);
            if (existing != null)
            {
                if (existing.MediaType != normalized.MediaType || existing.CapturedAt != normalized.CapturedAt ||
                    existing.SourceOwnership != normalized.SourceOwnership ||
                    !string.Equals(existing.SourcePath, normalized.SourcePath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Capture identity is already registered differently.");
                return;
            }

            EnsureLocationAvailable(assets, normalized);
            assets.Add(normalized);
            await SaveAsync(assets, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<CaptureAsset?> GetAsync(CaptureId id, CancellationToken cancellationToken = default)
    {
        ValidateId(id);
        IReadOnlyList<CaptureAsset> assets = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
        return assets.FirstOrDefault(asset => asset.Id == id);
    }

    public async Task<IReadOnlyList<CaptureAsset>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return (await LoadAsync(cancellationToken).ConfigureAwait(false)).AsReadOnly(); }
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
            List<CaptureAsset> assets = await LoadAsync(cancellationToken).ConfigureAwait(false);
            int index = assets.FindIndex(asset => asset.Id == id);
            if (index < 0) throw new KeyNotFoundException("Capture is not registered.");
            CaptureAsset updated = Normalize(update(assets[index]));
            EnsureLocationAvailable(assets, updated);
            assets[index] = updated;
            await SaveAsync(assets, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task<List<CaptureAsset>> LoadAsync(CancellationToken cancellationToken)
    {
        CaptureCatalogDocument? document = await _documents.ReadAsync(_path,
            CaptureCatalogJsonContext.Default.CaptureCatalogDocument, cancellationToken).ConfigureAwait(false);
        if (document == null) return [];
        if (document.Version != 1 || document.Assets == null) throw new InvalidDataException("Unsupported or invalid capture catalog.");
        try
        {
            List<CaptureAsset> assets = document.Assets.Select(asset => asset == null
                ? throw new InvalidDataException("Missing capture asset.")
                : Normalize(new CaptureAsset(new CaptureId(asset.Id), (CaptureFileType)asset.MediaType,
                    asset.CapturedAt, asset.SourcePath, (CaptureSourceOwnership)asset.SourceOwnership, asset.PreferredPath))).ToList();
            if (assets.Select(asset => asset.Id).Distinct().Count() != assets.Count ||
                assets.Select(asset => asset.SourcePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != assets.Count)
                throw new InvalidDataException("Duplicate capture identity or source location.");
            return assets;
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Invalid capture catalog.", exception);
        }
    }

    private Task SaveAsync(List<CaptureAsset> assets, CancellationToken cancellationToken) =>
        _documents.WriteAsync(_path, new CaptureCatalogDocument(1, assets.Select(asset =>
            new CaptureAssetDocument(asset.Id.Value, (int)asset.MediaType, asset.CapturedAt,
                asset.SourcePath, (int)asset.SourceOwnership, asset.PreferredPath)).ToArray()),
            CaptureCatalogJsonContext.Default.CaptureCatalogDocument, cancellationToken);

    private static CaptureAsset Normalize(CaptureAsset asset) => new(asset.Id, asset.MediaType, asset.CapturedAt,
        NormalizePath(asset.SourcePath), asset.SourceOwnership, asset.PreferredPath == null ? null : NormalizePath(asset.PreferredPath));

    private static string NormalizePath(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Capture locations must be absolute.", nameof(path));
        return Path.GetFullPath(path);
    }

    private static void EnsureLocationAvailable(List<CaptureAsset> assets, CaptureAsset updated)
    {
        if (assets.Any(asset => asset.Id != updated.Id && string.Equals(asset.SourcePath, updated.SourcePath, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Source location belongs to another capture.");
    }

    private static void ValidateId(CaptureId id)
    {
        if (id.IsEmpty) throw new ArgumentException("Capture identity is required.", nameof(id));
    }

    public void Dispose() => _gate.Dispose();
}
