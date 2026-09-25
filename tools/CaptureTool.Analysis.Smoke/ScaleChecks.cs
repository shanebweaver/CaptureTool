using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.Analysis.Persistence;
using CaptureTool.Infrastructure.Analysis.Persistence.Serialization;
using CaptureTool.Infrastructure.CaptureAssets;
using CaptureTool.Infrastructure.CaptureAssets.Serialization;
using CaptureTool.Infrastructure.Persistence;
using CaptureTool.Infrastructure.Windows.Security;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

// A repeatable local measurement, not a replacement queue/index or benchmark framework.
internal static class ScaleChecks
{
    public static async Task<int> RunAsync(string output)
    {
        string root = Path.Combine(output, "scale-" + Guid.NewGuid().ToString("N"));
        var storage = new SmokeStorage(root);
        var protector = new WindowsUserDataProtector();
        var files = new ObservedFiles();
        var documents = new ProtectedDocumentFile(protector, files);
        using var store = new LocalCaptureAnalysisStore(storage, protector, files);
        using var catalog = new LocalCaptureAssetCatalog(storage, protector, files);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var ct = timeout.Token;
        var scope = await store.GetAdmissionScopeAsync(ct);
        string data = storage.GetApplicationDataFolderPath();
        string generation = Path.Combine(data, "CaptureAnalysis", "records", scope.Generation.ToString("N"));
        var capability = new CapabilityDocument(AnalysisCapability.TextRecognition.Name, 1);
        var revision = new SourceRevision(new string('a', 64));
        var producer = new AnalyzerProvenance("scale-fixture", "synthetic-local", "fixture", "1");
        var payload = new TextRecognitionMetadata(Enumerable.Range(0, 200).Select(index =>
            new RecognizedText("Synthetic word " + index, new(.02 + index % 10 * .09, .02 + index / 10 * .045, .08, .03))));
        List<CaptureAssetDocument> assets = [];
        List<ScaleSample> samples = [];
        for (int index = 1; index <= 10000; index++)
        {
            var id = CaptureId.New();
            var run = Guid.NewGuid();
            var result = new AnalysisResult(payload, producer, DateTimeOffset.UtcNow, "scale-v1", run);
            var record = new CaptureAnalysisRecord(id, AnalysisMediaKind.Image, revision, "scale-v1", run, [result]);
            string source = Path.Combine(root, "sources", id + ".png");
            bool queued = index == 10000;
            var document = AnalysisDocumentMapper.ToDocument(record) with
            {
                Version = 2,
                Run = new(queued ? Guid.NewGuid() : run, Guid.NewGuid(), index, "scale-v1", source, "en",
                    queued ? null : revision.Sha256, (int)(queued ? AnalysisRunStatus.Queued : AnalysisRunStatus.Completed), [capability],
                    queued ? [] : [new(capability, (int)AnalyzerOutcomeKind.Succeeded, null)])
            };
            await documents.WriteAsync(Path.Combine(generation, id + ".analysis"), document, AnalysisJsonContext.Default.AnalysisDocument, ct);
            assets.Add(new(id.Value, (int)CaptureFileType.Image, DateTimeOffset.UtcNow, source, (int)CaptureSourceOwnership.Application, null, index));
            if (index % 1000 == 0) Console.WriteLine($"Seeded {index} protected metadata documents.");
            if (index is not (100 or 1000 or 10000)) continue;

            await documents.WriteAsync(Path.Combine(data, "CaptureAssets", "catalog.bin"), new CaptureCatalogDocument(3, assets.ToArray(), index),
                CaptureCatalogJsonContext.Default.CaptureCatalogDocument, ct);
            await documents.WriteAsync(Path.Combine(data, "CaptureAnalysis", "control.bin"), new AnalysisControlDocument(2, scope.Generation, index),
                AnalysisJsonContext.Default.AnalysisControlDocument, ct);
            GC.Collect();
            long allocated = GC.GetTotalAllocatedBytes(precise: true);
            var timer = Stopwatch.StartNew();
            var pending = await store.ReadPendingAsync(ct);
            double discoveryMs = timer.Elapsed.TotalMilliseconds;
            long discoveryAllocated = GC.GetTotalAllocatedBytes(precise: true) - allocated;
            if (pending.Count != (queued ? 1 : 0)) throw new InvalidOperationException("Scale fixture queue state is incorrect.");
            timer.Restart();
            if ((await catalog.ReadRegistrationsAsync(ct)).Count != index) throw new InvalidOperationException("Scale catalog count is incorrect.");
            double catalogMs = timer.Elapsed.TotalMilliseconds;

            files.Reset();
            Task discovery = store.ReadPendingAsync(ct);
            await files.FirstRead.Task.WaitAsync(ct);
            timer.Restart();
            await store.GetStorageStatusAsync(ct);
            double statusMs = timer.Elapsed.TotalMilliseconds;
            await discovery;

            files.Reset();
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Task cancelled = store.ReadPendingAsync(cancellation.Token);
            await files.FirstRead.Task.WaitAsync(ct);
            timer.Restart();
            await cancellation.CancelAsync();
            try { await cancelled; throw new InvalidOperationException("Discovery completed without observing cancellation."); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            double cancellationMs = timer.Elapsed.TotalMilliseconds;

            double? clearMs = null;
            if (queued)
            {
                files.Reset();
                Task duringClear = store.ReadPendingAsync(ct);
                await files.FirstRead.Task.WaitAsync(ct);
                timer.Restart();
                var cleared = await store.ClearAsync(index, ct);
                clearMs = timer.Elapsed.TotalMilliseconds;
                await duringClear;
                if (!cleared.Completed || (await store.ReadPendingAsync(ct)).Count != 0) throw new InvalidOperationException("Clear did not complete.");
            }
            samples.Add(new(index, discoveryMs, catalogMs, statusMs, cancellationMs, clearMs, discoveryAllocated,
                Process.GetCurrentProcess().PeakWorkingSet64));
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, "scale-results.json"),
                JsonSerializer.Serialize(samples.ToArray(), ScaleJsonContext.Default.ScaleSampleArray), ct);
            Console.WriteLine($"Scale {index}: discovery={discoveryMs:F0}ms, status={statusMs:F0}ms, cancel={cancellationMs:F0}ms, clear={clearMs:F0}ms");
        }
        return 0;
    }

    private sealed class ObservedFiles : IProtectedFileSystem
    {
        private readonly LocalProtectedFileSystem _inner = new();
        public TaskCompletionSource FirstRead { get; private set; } = NewSignal();
        public void Reset() => FirstRead = NewSignal();
        private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<byte[]?> ReadAsync(string path, CancellationToken ct)
        {
            if (path.EndsWith(".analysis", StringComparison.Ordinal)) FirstRead.TrySetResult();
            return _inner.ReadAsync(path, ct);
        }
        public Task WriteAtomicallyAsync(string path, byte[] ciphertext, CancellationToken ct) => _inner.WriteAtomicallyAsync(path, ciphertext, ct);
        public string[] GetFiles(string path) => _inner.GetFiles(path);
        public string[] GetDirectories(string path) => _inner.GetDirectories(path);
        public void DeleteFile(string path) => _inner.DeleteFile(path);
        public void DeleteDocumentDirectory(string path) => _inner.DeleteDocumentDirectory(path);
    }
}

internal sealed record ScaleSample(int Captures, double DiscoveryMilliseconds, double CatalogMilliseconds,
    double SettingsStatusWaitMilliseconds, double CancellationMilliseconds, double? ClearMilliseconds,
    long DiscoveryAllocatedBytes, long PeakWorkingSetBytes);
[JsonSerializable(typeof(ScaleSample[]))]
internal partial class ScaleJsonContext : JsonSerializerContext;
