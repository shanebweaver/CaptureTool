using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Capture;

/// <summary>Explicit physical renames and saved names. Never schedules or processes AI work.</summary>
internal sealed class CaptureNamingService(ICaptureAssetCatalog catalog, ICaptureNameStore names,
    IRecentCapturesChangeNotifier recents, IFileSystem files) : ICaptureNamingService, IDisposable
{
    private readonly ICaptureAssetCatalog _catalog = catalog;
    private readonly ICaptureNameStore _names = names;
    private readonly IRecentCapturesChangeNotifier _recents = recents;
    private readonly IFileSystem _files = files;
    private readonly SemaphoreSlim _commands = new(1, 1);
    private bool _stopped;
    public event Action? Changed;

    public async Task<CaptureName?> GetNameAsync(string path, CancellationToken cancellationToken = default)
    {
        try { return (await ResolveAsync(path, cancellationToken).ConfigureAwait(false))?.Name; }
        catch (Exception) { return null; }
    }
    public async Task<CaptureNamingState> GetStateAsync(string path, CancellationToken cancellationToken = default) =>
        new(await GetNameAsync(path, cancellationToken).ConfigureAwait(false), false);

    internal async Task InvalidatePendingAsync(CancellationToken ct)
    {
        await _commands.WaitAsync(ct).ConfigureAwait(false);
        try { await _names.InvalidatePendingNamesAsync(ct).ConfigureAwait(false); }
        finally { _commands.Release(); Publish(); }
    }

    public async Task<CaptureFileRename?> RenameAsync(string path, CaptureFileType mediaType, string name, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _stopped)) return null;
        try
        {
            var chosen = new CaptureName(name, false);
            if (!Path.IsPathFullyQualified(path) || !_files.FileExists(path)) return null;
            string newPath;
            await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_files.FileExists(path)) return null;
                var matches = await MatchesAsync(path, cancellationToken).ConfigureAwait(false);
                if (matches.Length > 1) return null;
                var asset = matches.SingleOrDefault();
                if (asset == null)
                {
                    asset = new(CaptureId.New(), mediaType, null, path, CaptureSourceOwnership.External);
                    await _catalog.RegisterAsync(asset, cancellationToken).ConfigureAwait(false);
                }
                newPath = await _names.RenameFileAsync(asset.Id, path,
                    chosen.SuggestedFileName() + Path.GetExtension(path), cancellationToken).ConfigureAwait(false);
            }
            finally { _commands.Release(); }
            NotifyNamesChanged();
            return new(path, newPath);
        }
        catch (Exception) { return null; }
    }

    private async Task<CaptureAsset?> ResolveAsync(string path, CancellationToken ct)
    {
        var matches = await MatchesAsync(path, ct).ConfigureAwait(false);
        return matches.Length == 1 ? matches[0] : null;
    }
    private async Task<CaptureAsset[]> MatchesAsync(string path, CancellationToken ct) =>
        (await _catalog.ReadAllAsync(ct).ConfigureAwait(false)).Where(asset => SamePath(path, asset.SourcePath) || SamePath(path, asset.PreferredPath)).Take(2).ToArray();
    private static bool SamePath(string path, string? other) => other != null && Path.IsPathFullyQualified(path) &&
        string.Equals(Path.GetFullPath(path), Path.GetFullPath(other), StringComparison.OrdinalIgnoreCase);

    private void NotifyNamesChanged() { Publish(); _recents.NotifyRecentCapturesChanged(); }
    private void Publish()
    {
        if (Changed is { } changed)
            foreach (Action observer in changed.GetInvocationList()) try { observer(); } catch (Exception) { }
    }
    internal async Task StopAsync()
    {
        Volatile.Write(ref _stopped, true);
        await _commands.WaitAsync().ConfigureAwait(false);
        _commands.Release();
    }
    public void Dispose() => _commands.Dispose();
}
