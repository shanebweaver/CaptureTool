using CaptureTool.Application.Abstractions.Capture.Assets;
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
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalCaptureAssetCatalog(IStorageService storage, IUserDataProtector protector)
        : this(storage, protector, new LocalProtectedFileSystem()) { }

    internal LocalCaptureAssetCatalog(IStorageService storage, IUserDataProtector protector, IProtectedFileSystem files)
    {
        _path = Path.Combine(storage.GetApplicationDataFolderPath(), "CaptureAssets", "catalog.bin");
        _documents = new(protector, files);
    }

    public Task RegisterAsync(CaptureAsset asset, CancellationToken cancellationToken = default) =>
        RegisterForAnalysisAsync(asset, null, cancellationToken);

    public async Task RegisterForAnalysisAsync(CaptureAsset asset, Guid? automaticAuthorization, CancellationToken cancellationToken = default, Guid? namingEpoch = null)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (automaticAuthorization == Guid.Empty) throw new ArgumentException("Authorization must be nonempty.", nameof(automaticAuthorization));
        CaptureAsset normalized = Normalize(asset);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Catalog state = await LoadAsync(cancellationToken).ConfigureAwait(false);
            CaptureAsset? existing = state.Entries.Find(item => item.Asset.Id == normalized.Id)?.Asset;
            if (existing != null)
            {
                if (existing.MediaType != normalized.MediaType || existing.CapturedAt != normalized.CapturedAt ||
                    existing.SourceOwnership != normalized.SourceOwnership ||
                    !string.Equals(existing.SourcePath, normalized.SourcePath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Capture identity is already registered differently.");
                return; // Replays cannot change intake eligibility or undo auto-save.
            }
            EnsureLocationAvailable(state.Entries, normalized);
            long sequence = checked(state.Sequence + 1);
            state.Entries.Add(new(normalized, sequence, automaticAuthorization,
                automaticAuthorization != null && namingEpoch != null && namingEpoch == state.NamingEpoch ? namingEpoch : null));
            await SaveAsync(state with { Sequence = sequence }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<CaptureAsset?> GetAsync(CaptureId id, CancellationToken cancellationToken = default)
    {
        ValidateId(id);
        return (await ReadAllAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(asset => asset.Id == id);
    }

    public async Task<IReadOnlyList<CaptureAsset>> ReadAllAsync(CancellationToken cancellationToken = default) =>
        (await ReadRegistrationsAsync(cancellationToken).ConfigureAwait(false)).Select(entry => entry.Asset).ToArray();

    public async Task<IReadOnlyList<CaptureRegistration>> ReadRegistrationsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return (await LoadAsync(cancellationToken).ConfigureAwait(false)).Entries.AsReadOnly(); }
        finally { _gate.Release(); }
    }

    public async Task<long> GetBoundaryAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return (await LoadAsync(cancellationToken).ConfigureAwait(false)).Sequence; }
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
            int index = state.Entries.FindIndex(entry => entry.Asset.Id == id);
            if (index < 0) throw new KeyNotFoundException("Capture is not registered.");
            CaptureAsset updated = Normalize(update(state.Entries[index].Asset));
            EnsureLocationAvailable(state.Entries, updated);
            state.Entries[index] = state.Entries[index] with { Asset = updated };
            await SaveAsync(state, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task<Catalog> LoadAsync(CancellationToken cancellationToken)
    {
        CaptureCatalogDocument? document = await _documents.ReadAsync(_path,
            CaptureCatalogJsonContext.Default.CaptureCatalogDocument, cancellationToken).ConfigureAwait(false);
        if (document == null) return new(0, []);
        if (document.Version is not (1 or 2 or 3 or 4) || document.Assets == null) throw new InvalidDataException("Unsupported or invalid capture catalog.");
        Catalog state;
        try
        {
            var entries = document.Assets.Select((asset, index) => asset == null
                ? throw new InvalidDataException("Missing capture asset.")
                : new CaptureRegistration(Normalize(new CaptureAsset(new CaptureId(asset.Id), (CaptureFileType)asset.MediaType,
                    document.Version < 3 && asset.SourceOwnership == (int)CaptureSourceOwnership.External ? null : asset.CapturedAt,
                    asset.SourcePath, (CaptureSourceOwnership)asset.SourceOwnership, asset.PreferredPath,
                    document.Version >= 4 && asset.Name != null ? new CaptureName(asset.Name, asset.AutomaticName) : null)),
                    document.Version == 1 ? index + 1 : asset.Sequence,
                    document.Version == 1 ? null : asset.AutomaticAuthorization, document.Version >= 4 ? asset.NamingEpoch : null)).ToList();
            long sequence = document.Version == 1 ? entries.Count : document.Sequence;
            if (sequence < 0 || entries.Any(entry => entry.Sequence <= 0 || entry.Sequence > sequence || entry.AutomaticAuthorization == Guid.Empty) ||
                entries.Select(entry => entry.Sequence).Distinct().Count() != entries.Count ||
                entries.Select(entry => entry.Asset.Id).Distinct().Count() != entries.Count ||
                entries.Select(entry => entry.Asset.SourcePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Count)
                throw new InvalidDataException("Duplicate or invalid capture registration.");
            if (document.NamingEpoch == Guid.Empty || entries.Any(entry => entry.NamingEpoch == Guid.Empty))
                throw new InvalidDataException("Invalid naming enrollment.");
            state = new(sequence, entries, document.Version >= 4 ? document.NamingEpoch : null);
        }
        catch (ArgumentException exception) { throw new InvalidDataException("Invalid capture catalog.", exception); }
        // V1 gains stable registration order. Before v3, imported dates came from recent activity,
        // not capture provenance; preserve identity/eligibility while dropping that unreliable fact.
        if (document.Version < 3) await SaveAsync(state, cancellationToken).ConfigureAwait(false);
        return state;
    }

    private Task SaveAsync(Catalog state, CancellationToken cancellationToken) =>
        _documents.WriteAsync(_path, new CaptureCatalogDocument(4, state.Entries.Select(entry =>
            new CaptureAssetDocument(entry.Asset.Id.Value, (int)entry.Asset.MediaType, entry.Asset.CapturedAt,
                entry.Asset.SourcePath, (int)entry.Asset.SourceOwnership, entry.Asset.PreferredPath, entry.Sequence, entry.AutomaticAuthorization,
                entry.Asset.Name?.Text, entry.Asset.Name?.IsAutomatic ?? false, entry.NamingEpoch)).ToArray(), state.Sequence, state.NamingEpoch),
            CaptureCatalogJsonContext.Default.CaptureCatalogDocument, cancellationToken);

    private static CaptureAsset Normalize(CaptureAsset asset) => new(asset.Id, asset.MediaType, asset.CapturedAt,
        NormalizePath(asset.SourcePath), asset.SourceOwnership, asset.PreferredPath == null ? null : NormalizePath(asset.PreferredPath), asset.Name);
    private static string NormalizePath(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Capture locations must be absolute.", nameof(path));
        return Path.GetFullPath(path);
    }
    private static void EnsureLocationAvailable(List<CaptureRegistration> entries, CaptureAsset updated)
    {
        if (entries.Any(entry => entry.Asset.Id != updated.Id && string.Equals(entry.Asset.SourcePath, updated.SourcePath, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Source location belongs to another capture.");
    }
    private static void ValidateId(CaptureId id)
    {
        if (id.IsEmpty) throw new ArgumentException("Capture identity is required.", nameof(id));
    }
    public void Dispose() => _gate.Dispose();
    private sealed record Catalog(long Sequence, List<CaptureRegistration> Entries, Guid? NamingEpoch = null);
}
