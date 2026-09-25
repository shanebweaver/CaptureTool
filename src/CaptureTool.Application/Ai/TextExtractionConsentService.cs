using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Settings;

namespace CaptureTool.Application.Ai;

internal sealed class TextExtractionConsentService : ITextExtractionConsentService, IDisposable
{
    private readonly ISettingsService _settings;
    private readonly ITextExtractionConsentPrompt _prompt;
    private readonly SemaphoreSlim _requests = new(1, 1);
    private readonly object _gate = new();
    private CancellationTokenSource _revoked = new();
    private readonly List<CancellationTokenSource> _retired = [];
    private bool _allowed;
    private int _blocked;
    private long _revocationVersion;
    public event Action? StateChanged;
    public TextExtractionConsentService(ISettingsService settings, ITextExtractionConsentPrompt prompt)
    {
        _settings = settings; _prompt = prompt;
        _settings.SettingsChanged += OnSettingsChanged;
        OnSettingsChanged([]);
    }
    public AiFeatureConsentState State => Volatile.Read(ref _blocked) != 0 ? AiFeatureConsentState.Denied :
        !_settings.IsSet(CaptureToolSettings.Settings_AiConsent_TextExtraction)
        ? AiFeatureConsentState.Unknown : _settings.Get(CaptureToolSettings.Settings_AiConsent_TextExtraction)
        ? AiFeatureConsentState.Granted : AiFeatureConsentState.Denied;
    public CancellationToken Revoked { get { lock (_gate) return _revoked.Token; } }
    public async Task<bool> EnsureConsentAsync(CancellationToken cancellationToken = default)
    {
        await _requests.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State == AiFeatureConsentState.Granted) return true;
            long version = Interlocked.Read(ref _revocationVersion);
            bool allowed = await _prompt.ConfirmAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (version != Interlocked.Read(ref _revocationVersion)) return false;
            var saved = await _settings.TrySetAndSaveAsync(CaptureToolSettings.Settings_AiConsent_TextExtraction, allowed, cancellationToken).ConfigureAwait(false);
            lock (_gate)
                if (saved.Succeeded && allowed && version == _revocationVersion) Volatile.Write(ref _blocked, 0);
            OnSettingsChanged([]);
            return saved.Succeeded && allowed && State == AiFeatureConsentState.Granted;
        }
        finally { _requests.Release(); }
    }
    public async Task<bool> RevokeConsentAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            Interlocked.Increment(ref _revocationVersion);
            Volatile.Write(ref _blocked, 1);
        }
        OnSettingsChanged([]);
        await _requests.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var saved = await _settings.TrySetAndSaveAsync(CaptureToolSettings.Settings_AiConsent_TextExtraction, false, cancellationToken).ConfigureAwait(false);
            OnSettingsChanged([]);
            return saved.Succeeded;
        }
        finally { _requests.Release(); }
    }
    private void OnSettingsChanged(ISettingDefinition[] changes)
    {
        bool changed;
        lock (_gate)
        {
            bool allowed = State == AiFeatureConsentState.Granted;
            changed = allowed != _allowed;
            if (allowed && !_allowed) { _retired.Add(_revoked); _revoked = new(); }
            else if (!allowed) _ = CancelAsync(_revoked);
            _allowed = allowed;
        }
        if (changed && StateChanged is { } observers)
            foreach (Action observer in observers.GetInvocationList()) try { observer(); } catch (Exception) { }
    }
    private static async Task CancelAsync(CancellationTokenSource source)
    {
        try { await source.CancelAsync().ConfigureAwait(false); } catch (Exception) { }
    }
    public void Dispose()
    {
        _settings.SettingsChanged -= OnSettingsChanged;
        _revoked.Dispose(); foreach (var source in _retired) source.Dispose(); _requests.Dispose();
    }
}
