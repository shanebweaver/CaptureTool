using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Analysis;
using CaptureTool.Application.DependencyInjection;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.Analysis.Persistence;
using CaptureTool.Infrastructure.Analysis.Persistence.Serialization;
using CaptureTool.Infrastructure.DependencyInjection;
using CaptureTool.Infrastructure.Persistence;
using CaptureTool.Infrastructure.Windows.Security;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// Opt-in process fault injection around production policy, worker, catalog, and
// atomic storage. Only model outputs and the recents/dialog ports are synthetic.
internal static class RecoveryChecks
{
    private static readonly CaptureId Id = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private const string Source = "CaptureTool synthetic retained source";
    private const string Derived = "CAPTURETOOL-SYNTHETIC-PRIVATE-DERIVED";
    private static readonly SourceRevision Revision = new(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Source))));

    public static async Task<int> RunAsync(string output)
    {
        string root = Path.Combine(output, "recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        List<RecoveryResult> results = [];
        foreach (string stage in new[] { "registration", "admission", "preparation", "execution", "publication", "committed", "metadata-execution", "metadata-publication", "metadata-committed", "deletion" })
        {
            string directory = Path.Combine(root, stage);
            Directory.CreateDirectory(directory);
            using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var elapsed = Stopwatch.StartNew();
            try
            {
                using (Process child = Start(directory, "--recovery-child", stage))
                {
                    try
                    {
                        string marker = Path.Combine(directory, "checkpoint.json");
                        while (!File.Exists(marker))
                        {
                            if (child.HasExited) throw new InvalidOperationException($"Child exited before {stage}: {child.ExitCode}.");
                            await Task.Delay(20, budget.Token);
                        }
                        Require(!child.HasExited, "Checkpoint must belong to a live process.");
                        child.Kill(entireProcessTree: true);
                        await child.WaitForExitAsync(budget.Token);
                    }
                    finally { if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); } }
                }
                using (Process restarted = Start(directory, "--recovery-resume", stage))
                {
                    try { await restarted.WaitForExitAsync(budget.Token); }
                    finally { if (!restarted.HasExited) { restarted.Kill(entireProcessTree: true); await restarted.WaitForExitAsync(); } }
                    Require(restarted.ExitCode == 0, $"Restart failed at {stage}; see {Path.Combine(directory, "error.txt")}.");
                }
                results.Add(new(stage, true, elapsed.ElapsedMilliseconds, null));
            }
            catch (Exception exception) { results.Add(new(stage, false, elapsed.ElapsedMilliseconds, exception.Message)); }
            await File.WriteAllTextAsync(Path.Combine(output, "recovery-results.json"),
                JsonSerializer.Serialize(results.ToArray(), RecoveryJsonContext.Default.RecoveryResultArray));
            Console.WriteLine($"Recovery {stage}: {results[^1].Passed} ({elapsed.ElapsedMilliseconds} ms)");
        }
        return results.All(result => result.Passed) ? 0 : 1;
    }

    private static Process Start(string root, string mode, string stage)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add(root);
        info.ArgumentList.Add(mode);
        info.ArgumentList.Add(stage);
        return Process.Start(info) ?? throw new InvalidOperationException("Could not start recovery process.");
    }

    public static async Task<int> ChildAsync(string root, string stage, bool resume)
    {
        try
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            CancellationToken ct = budget.Token;
            var storage = new SmokeStorage(root);
            var protector = new WindowsUserDataProtector();
            var faults = new CheckpointFiles(root, resume ? null : stage, protector);
            var plan = new MediaAnalysisPlan(AnalysisMediaKind.Image, "recovery-v1", [
                new(AnalysisCapability.Description, ["description"], TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1)),
                new(AnalysisCapability.TextRecognition, ["text"], TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1)),
                new(AnalysisCapability.CaptureSynopsis, ["synopsis"], TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1)),
            ]);
            var services = new ServiceCollection().AddGenericServices().AddApplicationServices()
                .AddSingleton<IStorageService>(storage).AddSingleton<IUserDataProtector>(protector)
                .AddSingleton(new LocalCaptureAnalysisStore(storage, protector, faults))
                .AddSingleton<IRecentCaptureCatalog>(new NoRecents()).AddSingleton<ICaptureMemoryPrompts>(new Prompts())
                .AddSingleton(new CaptureAnalysisConfiguration([plan]))
                .AddSingleton<IMediaAnalyzer>(new Analyzer("description", AnalysisCapability.Description, faults))
                .AddSingleton<IMediaAnalyzer>(new Analyzer("text", AnalysisCapability.TextRecognition, faults))
                .AddSingleton<IMetadataProcessor>(new Processor(faults));
            // Capture registration uses the same production file boundary as metadata.
            services.AddSingleton<ICaptureAssetCatalog>(new CaptureTool.Infrastructure.CaptureAssets.LocalCaptureAssetCatalog(storage, protector, faults));
            using ServiceProvider provider = services.BuildServiceProvider();
            var memory = provider.GetRequiredService<ICaptureMemoryService>();
            var store = provider.GetRequiredService<IAnalysisExecutionStore>();
            var catalog = provider.GetRequiredService<ICaptureAssetCatalog>();
            string sourcePath = Path.Combine(root, "capture.png");
            await memory.InitializeAsync(ct);
            try
            {
                if (!resume)
                {
                    await memory.SetScanningAsync(true, ct);
                    await File.WriteAllTextAsync(sourcePath, Source, ct);
                    faults.Armed = true;
                    await memory.RegisterCaptureAsync(new(Id, CaptureFileType.Image, DateTimeOffset.UtcNow, sourcePath,
                        CaptureSourceOwnership.Application), memory.CaptureAuthorization, ct);
                    await CompletedAsync(store, Id, ct);
                    if (stage == "deletion") await memory.DeleteMetadataAsync(ct);
                    throw new InvalidOperationException("Expected crash checkpoint was not reached.");
                }

                Checkpoint checkpoint = JsonSerializer.Deserialize(await File.ReadAllTextAsync(Path.Combine(root, "checkpoint.json"), ct),
                    RecoveryJsonContext.Default.Checkpoint)!;
                if (stage == "deletion")
                {
                    Require(await store.GetWorkAsync(Id, ct) == null, "Clear must not resurrect an old request.");
                    Require(await store.GetAsync(Id, cancellationToken: ct) == null, "Cleared metadata remains unreadable.");
                    Require((await store.ReadPendingAsync(ct)).Count == 0, "Cleared history must not be rescheduled.");
                    var stale = new AnalysisRunToken(Id, checkpoint.Generation, checkpoint.RunId);
                    Require(!await store.CommitStepAsync(stale, new(AnalysisCapability.Description, AnalyzerOutcomeKind.Succeeded, null),
                        new(new DescriptionMetadata([new(Derived)]), Producer("description"), DateTimeOffset.UtcNow, plan.Version, stale.RunId), ct),
                        "An old process cannot publish after clear.");
                    Require((await catalog.ReadAllAsync(ct)).Count == 1 && memory.State.Policy.IsAllowed, "Clear must keep catalog and policy.");
                    await memory.ScanExistingAsync(ct);
                    var replacement = await CompletedAsync(store, Id, ct);
                    Require(replacement.Token.RunId != checkpoint.RunId, "Explicit reanalysis must use a new run.");
                }
                else
                {
                    var completed = await CompletedAsync(store, Id, ct);
                    if (stage != "registration") Require(completed.Token.RunId == checkpoint.RunId, "Restart must retain the admitted run identity.");
                    var record = await store.GetAsync(Id, Revision, ct);
                    Require(record?.Results.Count == 3, "Basic and derived steps must be available after recovery.");
                    Require(record.Results.Single(result => result.Payload is CaptureSynopsisMetadata).Derivation!.Matches(record.Results), "Recovered insights must retain current input identities.");
                    Require(record.Results.All(result => result.ProducingRunId == completed.Token.RunId), "Results must belong to the resumed run.");
                    Require((await store.ReadPendingAsync(ct)).Count == 0, "Completed work must leave the queue.");
                    if (stage == "committed") Require(File.ReadAllLines(Path.Combine(root, "description-calls.txt")).Length == 1,
                        "A committed step must not execute twice after restart.");
                    if (stage.StartsWith("metadata-", StringComparison.Ordinal))
                        Require(File.ReadAllLines(Path.Combine(root, "text-calls.txt")).Length == 1, "Enrichment restart must not repeat basic scanning.");
                    if (stage == "metadata-committed")
                        Require(File.ReadAllLines(Path.Combine(root, "synopsis-calls.txt")).Length == 1, "A committed insight must not execute twice.");
                }
                Require(await File.ReadAllTextAsync(sourcePath, ct) == Source, "Source media was modified.");
                Require(Directory.GetFiles(storage.GetApplicationDataFolderPath(), "*.tmp", SearchOption.AllDirectories).Length == 0,
                    "Interrupted encrypted publication must not accumulate orphan temporary files.");
                foreach (string file in Directory.GetFiles(storage.GetApplicationDataFolderPath(), "*", SearchOption.AllDirectories))
                    Require(!Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file, ct)).Contains(Derived, StringComparison.Ordinal),
                        "Derived text reached a plaintext storage file.");
                while (memory.State.IsLoading) await Task.Delay(10, ct);
                Require(memory.State.FailureCode == null, "Recovery must not leave a storage failure.");
            }
            finally { await memory.StopAsync(); }
            return 0;
        }
        catch (Exception exception)
        {
            await File.WriteAllTextAsync(Path.Combine(root, "error.txt"), exception.ToString());
            return 1;
        }
    }

    private static async Task<AnalysisWorkItem> CompletedAsync(IAnalysisExecutionStore store, CaptureId id, CancellationToken ct)
    {
        while (true)
        {
            AnalysisWorkItem? work = await store.GetWorkAsync(id, ct);
            if (work?.Run.Status == AnalysisRunStatus.Completed) return work;
            if (work != null && !work.Run.IsPending) throw new InvalidOperationException($"Unexpected terminal state: {work.Run.Status}.");
            await Task.Delay(10, ct);
        }
    }

    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static AnalyzerProvenance Producer(string id) => new(id, "synthetic-local", id, "1");

    private sealed class Analyzer(string id, AnalysisCapability capability, CheckpointFiles faults) : IMediaAnalyzer
    {
        public MediaAnalyzerDescriptor Descriptor { get; } = new(id, capability, [AnalysisMediaKind.Image]);
        public ValueTask<AnalyzerAvailability> GetAvailabilityAsync(AnalysisMediaKind kind, string? language, CancellationToken ct) =>
            ValueTask.FromResult(faults.Stage == "preparation" ? AnalyzerAvailability.PreparationRequired : AnalyzerAvailability.Ready);
        public async Task<AnalyzerAvailability> PrepareAsync(IProgress<AnalysisProgress>? progress, CancellationToken ct)
        {
            await faults.PauseAsync("preparation", ct);
            return AnalyzerAvailability.Ready;
        }
        public async Task<AnalyzerOutcome> AnalyzeAsync(AnalysisInput input, IProgress<AnalysisProgress>? progress, CancellationToken ct)
        {
            await File.AppendAllTextAsync(Path.Combine(faults.Root, id + "-calls.txt"), "called\n", ct);
            await faults.PauseAsync("execution", ct);
            AnalysisPayload payload = capability == AnalysisCapability.Description
                ? new DescriptionMetadata([new(Derived)]) : new TextRecognitionMetadata([new(Derived)]);
            return AnalyzerOutcome.Success(payload, Producer(id));
        }
    }

    private sealed class CheckpointFiles(string root, string? stage, IUserDataProtector protector) : IProtectedFileSystem
    {
        private readonly LocalProtectedFileSystem _inner = new();
        private Checkpoint _latest = new(Guid.Empty, Guid.Empty);
        public string Root => root;
        public string? Stage => stage;
        public bool Armed { get; set; }
        public Task<byte[]?> ReadAsync(string path, CancellationToken ct) => _inner.ReadAsync(path, ct);
        public string[] GetFiles(string path) => _inner.GetFiles(path);
        public string[] GetDirectories(string path) => _inner.GetDirectories(path);
        public void DeleteFile(string path) => _inner.DeleteFile(path);
        public void DeleteDocumentDirectory(string path)
        {
            PauseAsync("deletion", CancellationToken.None).GetAwaiter().GetResult();
            _inner.DeleteDocumentDirectory(path);
        }
        public async Task WriteAtomicallyAsync(string path, byte[] ciphertext, CancellationToken ct)
        {
            AnalysisDocument? document = null;
            if (path.EndsWith(".analysis", StringComparison.Ordinal))
            {
                byte[] plaintext = protector.Unprotect(ciphertext);
                try { document = JsonSerializer.Deserialize(plaintext, AnalysisJsonContext.Default.AnalysisDocument); }
                finally { CryptographicOperations.ZeroMemory(plaintext); }
                _latest = new(Guid.ParseExact(Path.GetFileName(Path.GetDirectoryName(path))!, "N"), document!.Run!.Id);
                if (Armed && (stage == "publication" && document.Run.CompletedSteps.Length == 1 ||
                    stage == "metadata-publication" && document.Run.CompletedSteps.Length == 3))
                {
                    // Simulate an interrupted encrypted temporary write before atomic replacement.
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    using (var partial = new FileStream(path + "." + Guid.NewGuid().ToString("N") + ".tmp", FileMode.CreateNew))
                    { partial.Write(ciphertext, 0, ciphertext.Length / 2); partial.Flush(true); }
                    await PauseAsync(stage, ct);
                }
            }
            await _inner.WriteAtomicallyAsync(path, ciphertext, ct);
            if (path.EndsWith("catalog.bin", StringComparison.Ordinal)) await PauseAsync("registration", ct);
            if (document?.Run is { } run)
            {
                if (run.SourceSha256 == null) await PauseAsync("admission", ct);
                if (run.CompletedSteps.Length == 1) await PauseAsync("committed", ct);
                if (run.CompletedSteps.Length == 3) await PauseAsync("metadata-committed", ct);
            }
        }
        public async Task PauseAsync(string point, CancellationToken ct)
        {
            if (!Armed || stage != point) return;
            string temporary = Path.Combine(root, "checkpoint.tmp");
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(_latest, RecoveryJsonContext.Default.Checkpoint), ct);
            File.Move(temporary, Path.Combine(root, "checkpoint.json"));
            await Task.Delay(Timeout.Infinite, ct);
        }
    }
    private sealed class Processor(CheckpointFiles faults) : IMetadataProcessor
    {
        public MetadataProcessorDescriptor Descriptor { get; } = new("synopsis", "1", AnalysisCapability.CaptureSynopsis,
            [AnalysisCapability.TextRecognition], new(10, 1000, 1000, TimeSpan.FromMinutes(1)));
        public async Task<AnalyzerOutcome> ProcessAsync(MetadataProcessorInput input, CancellationToken ct)
        {
            await File.AppendAllTextAsync(Path.Combine(faults.Root, "synopsis-calls.txt"), "called\n", ct);
            await faults.PauseAsync("metadata-execution", ct);
            var entry = input.Entries[0];
            return AnalyzerOutcome.Success(new CaptureSynopsisMetadata(new(Derived, [new(entry.ResultId, entry.EntryIndex, 0, entry.Text.Length)]), [], input.Coverage), Producer("synopsis"));
        }
    }
    private sealed class Prompts : ICaptureMemoryPrompts
    {
        public Task<bool> ConfirmAsync(CaptureMemoryPrompt prompt, CancellationToken ct) => Task.FromResult(prompt != CaptureMemoryPrompt.ScanExisting);
    }
    private sealed class NoRecents : IRecentCaptureCatalog
    {
        public IReadOnlyList<RecentCaptureCatalogEntry> GetEntries() => [];
        public void RecordCaptured(string path, CaptureFileType type) => throw new NotSupportedException();
        public void RecordOpened(string path, CaptureFileType type) => throw new NotSupportedException();
        public void ReplacePath(string oldPath, string newPath) => throw new NotSupportedException();
        public void Touch(string path) => throw new NotSupportedException();
        public bool Remove(string path) => throw new NotSupportedException();
        public int RemoveRange(IEnumerable<string> paths) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
    }
}

internal sealed record Checkpoint(Guid Generation, Guid RunId);
internal sealed record RecoveryResult(string Stage, bool Passed, long ElapsedMilliseconds, string? Error);
[JsonSerializable(typeof(Checkpoint))]
[JsonSerializable(typeof(RecoveryResult[]))]
internal partial class RecoveryJsonContext : JsonSerializerContext;
