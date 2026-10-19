using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Analysis;

public sealed partial class CaptureAnalysisWorker : ICaptureAnalysisWorker, IDisposable
{
    private readonly IAnalysisExecutionStore _store;
    private readonly IAnalysisAuthorization _authorization;
    private readonly IAnalysisSource _source;
    private readonly ICaptureAssetCatalog _catalog;
    private readonly CaptureAnalysisConfiguration _configuration;
    private readonly AnalysisProviderSession _providers;
    private readonly AnalysisStepExecutor _steps;
    private readonly ILogService? _log;
    private readonly TimeProvider _time;
    private int _releaseRequested;
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly SemaphoreSlim _commands = new(1, 1);
    private readonly SemaphoreSlim _running = new(1, 1);
    private readonly object _activeLock = new();
    private readonly object _progressLock = new();
    private CancellationTokenSource? _activeCancellation;
    private AnalysisRunToken? _activeToken;
    private long _progressVersion;
    private bool _publishingProgress;
    private AnalysisActivitySnapshot _progress = new(AnalysisActivity.Idle);

    public CaptureAnalysisWorker(IAnalysisExecutionStore store, IAnalysisAuthorization authorization, IAnalysisSource source,
        CaptureAnalysisConfiguration configuration, IEnumerable<IMediaAnalyzer> analyzers, ICaptureAssetCatalog catalog,
        IEnumerable<IMetadataProcessor>? processors = null, IEnumerable<IAnalysisResources>? resources = null, ILogService? log = null,
        TimeProvider? time = null)
    {
        _store = store;
        _authorization = authorization;
        _source = source;
        _catalog = catalog;
        _configuration = configuration;
        _log = log;
        _time = time ?? TimeProvider.System;
        IMediaAnalyzer[] adapters = analyzers.ToArray();
        IMetadataProcessor[] metadata = processors?.ToArray() ?? [];
        configuration.ValidateAnalyzers(adapters.Select(analyzer => analyzer.Descriptor), metadata.Select(processor => processor.Descriptor));
        _providers = new AnalysisProviderSession(resources ?? [], log);
        _steps = new AnalysisStepExecutor(adapters, metadata, _providers, log);
    }

    public AnalysisActivitySnapshot Progress => Volatile.Read(ref _progress);
    public event Action<AnalysisActivitySnapshot>? ProgressChanged;
    public event Action<CaptureId, AnalysisCapability>? ResultCommitted;

    public async Task<bool> EnqueueAsync(AnalysisRequest request, CancellationToken cancellationToken = default)
    {
        await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using IAnalysisAuthorizationLease grant = await _authorization.AcquireAsync(cancellationToken).ConfigureAwait(false);
            if (!grant.IsAllowed || grant.Revision == Guid.Empty || grant.Revoked.IsCancellationRequested ||
                request.ExpectedAuthorizationId != grant.Revision)
            {
                return false;
            }

            MediaAnalysisPlan plan = _configuration.SelectPlan(request.MediaKind, request.Capabilities);
            // Pin the selected file before queuing without reading its contents. Replays retain
            // their original identity, even if the path has since been renamed or reused.
            AnalysisWorkItem? prior = await _store.GetWorkAsync(request.CaptureId, cancellationToken).ConfigureAwait(false);
            AnalysisSourceIdentity? identity = null;
            if (prior?.Token == new AnalysisRunToken(request.CaptureId, request.Generation, request.RequestId))
            {
                identity = prior.SourceIdentity;
            }
            else
            {
                try
                {
                    identity = await _source.GetIdentityAsync(request.SourcePath, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException) { }
            }
            request = request with
            {
                SourceIdentity = identity
            };
            bool accepted = await _store.AdmitAsync(request, grant.Revision, plan, cancellationToken).ConfigureAwait(false);
            if (accepted)
            {
                if (request.ExpectedRunId is { } priorRun)
                {
                    CancelActive(new(request.CaptureId, request.Generation, priorRun));
                }

                Signal();
            }
            return accepted;
        }
        finally
        {
            _commands.Release();
        }
    }

    public async Task CancelAsync(CaptureId captureId, CancellationToken cancellationToken = default)
    {
        await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisWorkItem? work = await _store.GetWorkAsync(captureId, cancellationToken).ConfigureAwait(false);
            if (work != null)
            {
                await _store.FinishAsync(work.Token, AnalysisRunStatus.Cancelled, cancellationToken).ConfigureAwait(false);
                CancelActive(work.Token);
            }
            Interlocked.Exchange(ref _releaseRequested, 1);
            Signal();
        }
        finally
        {
            _commands.Release();
        }
    }

    public async Task<AnalysisCleanupResult> ClearAsync(CancellationToken cancellationToken = default)
    {
        await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisCleanupResult result = await _store.ClearAsync(cancellationToken).ConfigureAwait(false);
            CancelActive(null);
            Interlocked.Exchange(ref _releaseRequested, 1);
            Signal();
            return result;
        }
        finally
        {
            _commands.Release();
        }
    }

    private static bool IsAuthorized(IAnalysisAuthorizationLease grant, AnalysisWorkItem work) =>
        grant.IsAllowed && grant.Revision == work.Run.AuthorizationId && !grant.Revoked.IsCancellationRequested;

    private void Signal()
    {
        // Serialize producers; the consumer can only lower the count. Coalesce wakeups
        // without throwing a first-chance exception for an already pending signal.
        lock (_signal)
        {
            if (_signal.CurrentCount == 0)
            {
                _signal.Release();
            }
        }
    }

    public void Dispose()
    {
        _signal.Dispose();
        _commands.Dispose();
        _running.Dispose();
    }

    private sealed class SourceUnavailableException : Exception;
}
