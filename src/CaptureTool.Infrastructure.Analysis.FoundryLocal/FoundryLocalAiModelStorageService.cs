using CaptureTool.Application.Abstractions.Analysis.Models;
using CaptureTool.Application.Abstractions.Storage;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal;

internal sealed class FoundryLocalAiModelStorageService : IAiModelStorageService
{
    private readonly IFoundryLocalSdkClient _sdkClient;
    private readonly IReadOnlyList<IFoundryLocalSpeechModelMaintenanceLeaseSource>
        _maintenanceLeaseSources;
    private readonly IFoundryLocalModelProvenanceStore _provenanceStore;
    private readonly string _modelCachePath;

    public FoundryLocalAiModelStorageService(
        IFoundryLocalSdkClient sdkClient,
        IEnumerable<IFoundryLocalSpeechModelMaintenanceLeaseSource> maintenanceLeaseSources,
        IFoundryLocalModelProvenanceStore provenanceStore,
        IApplicationLocalCachePathProvider cachePathProvider)
    {
        ArgumentNullException.ThrowIfNull(sdkClient);
        ArgumentNullException.ThrowIfNull(maintenanceLeaseSources);
        ArgumentNullException.ThrowIfNull(provenanceStore);
        ArgumentNullException.ThrowIfNull(cachePathProvider);

        _sdkClient = sdkClient;
        _maintenanceLeaseSources = maintenanceLeaseSources.ToArray();
        _provenanceStore = provenanceStore;
        _modelCachePath = Path.Combine(
            cachePathProvider.GetApplicationLocalCacheFolderPath(),
            "CaptureAnalysis",
            "FoundryLocal",
            "models");
    }

    public async ValueTask<AiModelStorageSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(
            () => MeasureModelStorage(cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<AiModelStorageRemovalResult> RemoveDownloadedModelsAsync(
        CancellationToken cancellationToken = default)
    {
        AiModelStorageSnapshot before = await GetSnapshotAsync(cancellationToken)
            .ConfigureAwait(false);
        var leases = new List<IAsyncDisposable>(_maintenanceLeaseSources.Count);
        try
        {
            foreach (IFoundryLocalSpeechModelMaintenanceLeaseSource source in
                _maintenanceLeaseSources)
            {
                leases.Add(await source
                    .AcquireModelMaintenanceLeaseAsync(cancellationToken)
                    .ConfigureAwait(false));
            }

            await _sdkClient.InitializeAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<IFoundryLocalSdkModel> cachedModels = await _sdkClient
                .GetCachedModelsAsync(cancellationToken)
                .ConfigureAwait(false);
            var removedModelCount = 0;
            var failedModelCount = 0;
            foreach (IFoundryLocalSdkModel model in cachedModels)
            {
                try
                {
                    await model.RemoveFromCacheAsync(cancellationToken).ConfigureAwait(false);
                    removedModelCount++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    failedModelCount++;
                }
            }

            DeleteSpeechModelProvenance();
            AiModelStorageSnapshot after = await GetSnapshotAsync(cancellationToken)
                .ConfigureAwait(false);
            long reclaimedBytes = before.MeasurementSucceeded && after.MeasurementSucceeded
                ? Math.Max(0, before.DownloadedByteCount - after.DownloadedByteCount)
                : 0;
            return new AiModelStorageRemovalResult(
                removedModelCount,
                reclaimedBytes,
                after.DownloadedByteCount,
                failedModelCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            AiModelStorageSnapshot after = await GetSnapshotAsync(CancellationToken.None)
                .ConfigureAwait(false);
            return new AiModelStorageRemovalResult(
                removedModelCount: 0,
                reclaimedByteCount: 0,
                remainingByteCount: after.DownloadedByteCount,
                failedModelCount: 1);
        }
        finally
        {
            for (int index = leases.Count - 1; index >= 0; index--)
            {
                await leases[index].DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private AiModelStorageSnapshot MeasureModelStorage(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_modelCachePath))
        {
            return new AiModelStorageSnapshot(0);
        }

        try
        {
            long byteCount = 0;
            foreach (string filePath in Directory.EnumerateFiles(
                _modelCachePath,
                "*",
                SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                byteCount = checked(byteCount + new FileInfo(filePath).Length);
            }

            return new AiModelStorageSnapshot(byteCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new AiModelStorageSnapshot(0, measurementSucceeded: false);
        }
    }

    private void DeleteSpeechModelProvenance()
    {
        _provenanceStore.Delete(FoundryLocalSpeechModelConfiguration.Whisper.ModelAlias);
        _provenanceStore.Delete(
            FoundryLocalSpeechModelConfiguration.NemotronMultilingual.ModelAlias);
    }
}
