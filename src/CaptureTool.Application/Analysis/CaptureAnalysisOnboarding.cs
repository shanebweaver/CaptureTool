using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Settings;

namespace CaptureTool.Application.Analysis;

internal sealed class CaptureAnalysisOnboarding(ICaptureMemoryService memory, ISettingsService settings) : ICaptureAnalysisOnboarding, IDisposable
{
    private readonly SemaphoreSlim _requests = new(1, 1);
    private long _attempt;
    private bool _shown;
    public async Task ShowOnFirstLaunchAsync(CancellationToken cancellationToken = default) =>
        await ShowAsync(true, cancellationToken).ConfigureAwait(false);
    public Task<bool> EnableAsync(CancellationToken cancellationToken = default) => ShowAsync(false, cancellationToken);
    private async Task<bool> ShowAsync(bool firstLaunch, CancellationToken ct)
    {
        long attempt = Interlocked.Read(ref _attempt);
        await _requests.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await memory.InitializeAsync(ct).ConfigureAwait(false);
            if (!memory.State.PolicyAvailable) return false;
            if (memory.State.Policy.IsAllowed) return true;
            if (attempt != Interlocked.Read(ref _attempt)) return memory.State.Policy.IsAllowed;
            if (firstLaunch && (_shown || settings.Get(CaptureToolSettings.Settings_CaptureAnalysis_OnboardingSeen) || memory.State.Policy.ConsentGranted)) return false;
            await memory.SetScanningAsync(true, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            _shown = true;
            Interlocked.Increment(ref _attempt);
            await settings.TrySetAndSaveAsync(CaptureToolSettings.Settings_CaptureAnalysis_OnboardingSeen, true, ct).ConfigureAwait(false);
            return memory.State.Policy.IsAllowed;
        }
        finally { _requests.Release(); }
    }
    public void Dispose() => _requests.Dispose();
}
