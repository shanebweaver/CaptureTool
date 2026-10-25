using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Capture;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Analysis;

internal sealed partial class CaptureMemoryService
{
    public Task AnalyzeAsync(string path, AnalysisCapability capability, CancellationToken cancellationToken = default)
        => RequestAnalysisAsync(path, capability, scanText: false, cancellationToken);

    public Task ScanTextAsync(string path, CancellationToken cancellationToken = default)
        => RequestAnalysisAsync(path, AnalysisCapability.TextRecognition, scanText: true, cancellationToken);

    private Task RequestAnalysisAsync(string path, AnalysisCapability capability, bool scanText, CancellationToken cancellationToken)
    {
        long epoch = Interlocked.Read(ref _epoch);
        return RunSafelyAsync(
            ct => RunCommandAsync(() => RequestAnalysisCoreAsync(path, capability, scanText, epoch, ct), ct),
            cancellationToken);
    }

    // Called under the command gate; consent is rechecked after each admission boundary.
    private async Task RequestAnalysisCoreAsync(string path, AnalysisCapability capability, bool scanText, long epoch, CancellationToken ct)
    {
        // Admit only the requested output and its prerequisites. The worker reuses
        // cached results and briefly retains the model for subsequent requests.
        if (!IsCurrentEpoch(epoch) || !_authorization.IsAllowed || Volatile.Read(ref _deleting) != 0 ||
            !Path.IsPathFullyQualified(path) || !_files.FileExists(path))
        {
            return;
        }

        CaptureFileType media = CaptureFileTypeDetector.DetectFileType(path);
        if (media is not (CaptureFileType.Image or CaptureFileType.Audio or CaptureFileType.Video))
        {
            return;
        }

        if (scanText && media == CaptureFileType.Audio)
        {
            return;
        }
        var kind = media switch
        {
            CaptureFileType.Image => AnalysisMediaKind.Image,
            CaptureFileType.Audio => AnalysisMediaKind.Audio,
            _ => AnalysisMediaKind.Video
        };
        if (!await IsRequestSupportedAsync(kind, capability, scanText, ct).ConfigureAwait(false))
        {
            return;
        }
        var assets = await _catalog.ReadAllAsync(ct).ConfigureAwait(false);
        CaptureAsset? asset = assets.FirstOrDefault(item => SamePath(item.SourcePath, path)) ??
            assets.FirstOrDefault(item => SamePath(item.PreferredPath, path));
        AnalysisWorkItem? prior = asset == null ? null : await _store.GetWorkAsync(asset.Id, ct).ConfigureAwait(false);
        if (prior?.Run.IsPending == true)
        {
            return;
        }

        if (!IsCurrentEpoch(epoch) || !_authorization.IsAllowed)
        {
            return;
        }

        if (asset == null)
        {
            asset = new(CaptureId.New(), media, null, path, CaptureSourceOwnership.External);
            await _catalog.RegisterAsync(asset, ct).ConfigureAwait(false);
        }
        AnalysisAdmissionScope scope = await _store.GetAdmissionScopeAsync(ct).ConfigureAwait(false);
        if (!IsCurrentEpoch(epoch) || !_authorization.IsAllowed)
        {
            return;
        }

        await AdmitAsync(asset, path, scope.Generation, _authorization.Policy.Revision, prior?.Run.Id, capability, scanText, ct).ConfigureAwait(false);
        await InitializeStorageAsync(ct).ConfigureAwait(false);
    }

    private async Task<bool> IsRequestSupportedAsync(AnalysisMediaKind kind, AnalysisCapability capability, bool scanText, CancellationToken ct)
    {
        if (_availability != null)
        {
            // An explicit request can recover after a system/model update.
            _availability.Invalidate();
            if (await _availability.IsSupportedAsync(kind, capability, ct).ConfigureAwait(false))
            {
                return true;
            }

            return scanText && await _availability.IsSupportedAsync(kind, AnalysisCapability.QrCodeDetection, ct).ConfigureAwait(false);
        }

        // Check the requested semantic output before scheduling expensive prerequisites.
        // Probes cannot download/load models or inspect the capture.
        IMetadataProcessor[] candidates = _processors
            .Where(processor => processor.Descriptor.Capability == capability)
            .ToArray();
        if (candidates.Length == 0)
        {
            return true;
        }

        foreach (IMetadataProcessor candidate in candidates)
        {
            AnalyzerAvailability readiness;
            try
            {
                readiness = await candidate.GetAvailabilityAsync(ct).AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                continue;
            }

            if (readiness is AnalyzerAvailability.Ready or AnalyzerAvailability.PreparationRequired)
            {
                return true;
            }
        }

        SetFailure("model-unavailable");
        return false;
    }

    private async Task AdmitAsync(CaptureAsset asset, string path, Guid generation, Guid authorization, Guid? prior, AnalysisCapability capability, bool scanText, CancellationToken ct)
    {
        if (_runner != null && _worker.Progress.Activity == AnalysisActivity.StorageUnavailable)
        {
            await _runner.ConfigureAwait(false);
        }

        AnalysisMediaKind media = asset.MediaType switch
        {
            CaptureFileType.Image => AnalysisMediaKind.Image,
            CaptureFileType.Audio => AnalysisMediaKind.Audio,
            CaptureFileType.Video => AnalysisMediaKind.Video,
            _ => throw new InvalidOperationException("Unsupported capture media.")
        };
        if (await _worker.EnqueueAsync(new(asset.Id, media, path, Guid.NewGuid(), generation, prior,
            ExpectedAuthorizationId: authorization, Capabilities: scanText
                ? [AnalysisCapability.QrCodeDetection, AnalysisCapability.TextRecognition]
                : CaptureAnalysisConfiguration.ForAction(media, capability), ReuseExisting: true,
            UsePreferredPath: !SamePath(asset.SourcePath, path) && SamePath(asset.PreferredPath, path)), ct).ConfigureAwait(false))
        {
            _storage = _storage with
            {
                HasData = true
            };
            Publish();
            if (_runner == null || _runner.IsCompleted)
            {
                _runner = ObserveRunnerAsync();
            }
        }
    }
}
