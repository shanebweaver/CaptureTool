using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Domain.Analysis;
using System.Collections.Concurrent;

namespace CaptureTool.Application.Analysis;

internal sealed class AnalysisFeatureAvailability(CaptureAnalysisConfiguration configuration,
    IEnumerable<IMediaAnalyzer> analyzers, IEnumerable<IMetadataProcessor> processors,
    ILogService? log = null) : IAnalysisFeatureAvailability
{
    private readonly Dictionary<string, IMediaAnalyzer> _analyzers = analyzers.ToDictionary(item => item.Descriptor.Id);
    private readonly Dictionary<string, IMetadataProcessor> _processors = processors.ToDictionary(item => item.Descriptor.Id);
    private readonly ConcurrentDictionary<(AnalysisMediaKind, AnalysisCapability), CachedProbe> _cache = new();
    private readonly ConcurrentDictionary<(string, AnalysisMediaKind), bool> _knownSupport = new();
    private readonly ConcurrentDictionary<(string, AnalysisMediaKind), string> _reported = new();
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    public void Invalidate() => _cache.Clear();

    public async Task<bool> IsSupportedAsync(AnalysisMediaKind kind, AnalysisCapability capability,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = (kind, capability);
        if (_cache.TryGetValue(key, out var previous) && previous.Expires <= DateTimeOffset.UtcNow)
            _cache.TryRemove(new KeyValuePair<(AnalysisMediaKind, AnalysisCapability), CachedProbe>(key, previous));
        var probe = _cache.GetOrAdd(key, _ => new(new(() => CheckAsync(kind, capability)), DateTimeOffset.UtcNow.AddMinutes(1)));
        return await probe.Value.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> CheckAsync(AnalysisMediaKind kind, AnalysisCapability capability)
    {
        var plan = configuration.Plans.FirstOrDefault(item => item.MediaKind == kind);
        var step = plan?.Steps.FirstOrDefault(item => item.Capability == capability);
        if (step == null) return false;
        foreach (string id in step.Candidates)
        {
            if (_analyzers.TryGetValue(id, out var analyzer) && analyzer.Descriptor.SupportedMedia.Contains(kind) &&
                await ProbeAsync(id, kind, ct => analyzer.GetAvailabilityAsync(kind, null, ct)).ConfigureAwait(false)) return true;
            if (_processors.TryGetValue(id, out var processor) &&
                await ProbeAsync(id, kind, processor.GetAvailabilityAsync).ConfigureAwait(false))
            {
                // Outputs declare their own prerequisites. A useful source is sufficient;
                // OCR, description, and speech need not all be supported on the same device.
                foreach (var input in processor.Descriptor.Inputs)
                    if (await IsSupportedAsync(kind, input).ConfigureAwait(false)) return true;
            }
        }
        return false;
    }

    private async Task<bool> ProbeAsync(string id, AnalysisMediaKind kind, Func<CancellationToken, ValueTask<AnalyzerAvailability>> query)
    {
        var key = (id, kind);
        using var timeout = new CancellationTokenSource();
        try
        {
            // A provider may have a synchronous native probe. Never block the UI thread on it.
            var state = await Task.Run(async () => await query(timeout.Token).ConfigureAwait(false))
                .WaitAsync(ProbeTimeout).ConfigureAwait(false);
            bool supported = state is AnalyzerAvailability.Ready or AnalyzerAvailability.PreparationRequired or AnalyzerAvailability.LanguageResourcesRequired;
            if (state != AnalyzerAvailability.TemporarilyUnavailable) _knownSupport[key] = supported;
            Report(state.ToString());
            return supported || state == AnalyzerAvailability.TemporarilyUnavailable && _knownSupport.GetValueOrDefault(key);
        }
        catch (Exception exception)
        {
            await timeout.CancelAsync().ConfigureAwait(false);
            // Keep a known supported feature visible during a transient failure. A first
            // inconclusive probe remains hidden. Never log SDK messages or user content.
            Report($"probe-failed-{exception.HResult:X8}");
            return _knownSupport.GetValueOrDefault(key);
        }

        void Report(string reason)
        {
            if (_reported.TryGetValue(key, out string? prior) && prior == reason) return;
            _reported[key] = reason;
            log?.LogInformation($"Analysis provider {id}, media {kind}: {reason}; OS {Environment.OSVersion.Version}; process {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}.");
        }
    }

    private sealed record CachedProbe(Lazy<Task<bool>> Value, DateTimeOffset Expires);
}
