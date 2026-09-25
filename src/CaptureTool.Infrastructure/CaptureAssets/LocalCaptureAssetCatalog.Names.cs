using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Infrastructure.CaptureAssets;

internal sealed partial class LocalCaptureAssetCatalog
{
    public async Task<CaptureNamingSnapshot> ReadNamingAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await LoadAsync(cancellationToken).ConfigureAwait(false);
            return new(state.NamingEpoch, state.Entries.Where(entry => state.NamingEpoch != null &&
                entry.NamingEpoch == state.NamingEpoch && entry.Asset.Name == null).ToArray());
        }
        finally { _gate.Release(); }
    }

    public Task SetAutomaticNamingAsync(bool enabled, CancellationToken cancellationToken = default) =>
        UpdateNamingAsync(state => state with { AutomaticNamingEnabled = enabled,
            NamingEpoch = enabled ? state.NamingEpoch ?? Guid.NewGuid() : null,
            Entries = enabled ? state.Entries : WithoutSuggestions(state) }, cancellationToken);

    public Task EnableAutomaticNamingByDefaultAsync(CancellationToken cancellationToken = default) =>
        UpdateNamingAsync(state => state.AutomaticNamingEnabled != null ? state :
            state with { AutomaticNamingEnabled = true, NamingEpoch = Guid.NewGuid() }, cancellationToken);

    public Task InvalidatePendingNamesAsync(CancellationToken cancellationToken = default) =>
        UpdateNamingAsync(state => state with { NamingEpoch = state.NamingEpoch == null ? null : Guid.NewGuid(),
            Entries = WithoutSuggestions(state) }, cancellationToken);

    private static List<CaptureRegistration> WithoutSuggestions(Catalog state) => state.Entries.Select(entry =>
        entry.Asset.Name?.IsAutomatic == true ? entry with { Asset = entry.Asset.WithName(null), NamingEpoch = null } : entry).ToList();

    private async Task UpdateNamingAsync(Func<Catalog, Catalog> update, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var state = await LoadAsync(ct).ConfigureAwait(false);
            var next = update(state);
            if (next != state) await SaveAsync(next, ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> TryApplyAutomaticNameAsync(CaptureId id, string name, Guid epoch, string expectedSourcePath, CancellationToken cancellationToken = default)
    {
        var chosen = new CaptureName(name, true);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await LoadAsync(cancellationToken).ConfigureAwait(false);
            int index = state.Entries.FindIndex(entry => entry.Asset.Id == id);
            if (epoch == Guid.Empty || epoch != state.NamingEpoch || index < 0 ||
                state.Entries[index].NamingEpoch != epoch || state.Entries[index].Asset.Name != null ||
                !string.Equals(state.Entries[index].Asset.SourcePath, expectedSourcePath, StringComparison.OrdinalIgnoreCase)) return false;
            var entry = state.Entries[index];
            state.Entries[index] = entry with { Asset = entry.Asset.WithName(chosen), NamingEpoch = null };
            await SaveAsync(state, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally { _gate.Release(); }
    }
}
