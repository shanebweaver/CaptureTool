using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Analysis;
using CaptureTool.Application.DependencyInjection;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.Analysis.Windows.DependencyInjection;
using CaptureTool.Infrastructure.DependencyInjection;
using CaptureTool.Infrastructure.Windows.Security;
using Microsoft.Extensions.DependencyInjection;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Exercises real OCR through consent, registration, the worker, DPAPI and a restart.
// Fixtures and saved state are synthetic and confined to a fresh output directory.
internal static class StartupOcrChecks
{
    public static async Task<int> RunAsync(string root)
    {
        Directory.CreateDirectory(root);
        var storage = new SmokeStorage(root);
        var protector = new WindowsUserDataProtector();
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        CancellationToken ct = budget.Token;
        var existing = CreateCapture("existing");
        Guid originalGeneration;
        Guid originalResult;
        await using (var app = new App(storage, protector))
        {
            await app.Memory.InitializeAsync(ct);
            await app.Memory.SetConsentAsync(true, ct);
            originalResult = (await ScanAsync(app, existing)).ResultId;
            originalGeneration = (await app.Store.GetAdmissionScopeAsync(ct)).Generation;
        }

        await ExtendAsync(Path.Combine(storage.GetApplicationDataFolderPath(), "CaptureAnalysis", "control.bin"),
            node => node["ReconciliationBoundary"] = 42);
        await ExtendAsync(Path.Combine(storage.GetApplicationDataFolderPath(), "CaptureAssets", "catalog.bin"), node =>
        {
            node["Sequence"] = 42;
            node["NamingEpoch"] = Guid.NewGuid();
            node["AutomaticNamingEnabled"] = false;
            node["Assets"]![0]!["Sequence"] = 42;
            node["Assets"]![0]!["AutomaticName"] = false;
            node["Assets"]![0]!["Name"] = "Accepted synthetic name";
        });

        var capture = CreateCapture("new-capture");
        await using (var app = new App(storage, protector))
        {
            await app.Memory.InitializeAsync(ct);
            Require(app.Memory.State.FailureCode == null && app.Memory.State.CanScan, "Startup failed.");
            Require((await app.Store.GetAdmissionScopeAsync(ct)).Generation == originalGeneration, "Startup reset the store.");
            Require((await app.Store.ReadPendingAsync(ct)).Count == 0, "Startup scheduled saved work.");
            Require((await app.Catalog.GetAsync(existing.Id, ct))?.Name?.Text == "Accepted synthetic name", "Startup lost the saved name.");
            Require((await app.Store.GetAsync(existing.Id, cancellationToken: ct))!.Results
                .Single(result => result.Payload is TextRecognitionMetadata).ResultId == originalResult, "Startup replaced saved OCR.");
            await ScanAsync(app, capture);
        }
        await using (var app = new App(storage, protector))
        {
            await app.Memory.InitializeAsync(ct);
            Require(app.Memory.State.FailureCode == null, "Second restart failed.");
            Require((await app.Catalog.ReadAllAsync(ct)).Count == 2, "A capture registration was lost.");
            Require((await app.Store.GetAsync(capture.Id, cancellationToken: ct))!.Results
                .Any(result => result.Payload is TextRecognitionMetadata), "New OCR did not survive restart.");
        }
        Console.WriteLine("PASS: startup with extra document fields, saved identity/name preservation, new capture OCR, and restart persistence.");
        return 0;

        CaptureAsset CreateCapture(string label)
        {
            string path = Path.Combine(root, label + ".png");
            using var bitmap = new Bitmap(1000, 300);
            using var graphics = Graphics.FromImage(bitmap);
            using var font = new Font("Arial", 48);
            graphics.Clear(Color.White);
            graphics.DrawString("Capture stability\nSmoke test 42", font, Brushes.Black, 35, 35);
            bitmap.Save(path, ImageFormat.Png);
            return new(CaptureId.New(), CaptureFileType.Image, DateTimeOffset.UtcNow, path, CaptureSourceOwnership.Application);
        }

        async Task<AnalysisResult> ScanAsync(App app, CaptureAsset asset)
        {
            await app.Memory.RegisterCaptureAsync(asset, ct);
            await app.Memory.ScanTextAsync(asset.SourcePath, ct);
            Require(app.Memory.State.FailureCode == null, "Capture registration or OCR admission failed.");
            while (true)
            {
                var work = await app.Store.GetWorkAsync(asset.Id, ct);
                Require(work != null, "OCR request was not admitted.");
                if (!work.Run.IsPending)
                {
                    Require(work.Run.Status == AnalysisRunStatus.Completed, "OCR did not complete.");
                    break;
                }
                await Task.Delay(20, ct);
            }
            var record = await app.Store.GetAsync(asset.Id, cancellationToken: ct);
            var result = record?.Results.SingleOrDefault(result => result.Payload is TextRecognitionMetadata);
            Require(result != null, "No OCR result was saved.");
            string text = string.Join(' ', ((TextRecognitionMetadata)result.Payload).Regions.Select(region => region.Text));
            Require(text.Contains("stability", StringComparison.OrdinalIgnoreCase) && text.Contains("42", StringComparison.Ordinal),
                "OCR did not recognize the synthetic fixture.");
            Console.WriteLine($"OCR passed: {result.Producer.AnalyzerId}");
            return result;
        }

        async Task ExtendAsync(string path, Action<JsonNode> update)
        {
            byte[] plaintext = protector.Unprotect(await File.ReadAllBytesAsync(path, ct));
            JsonNode node;
            try { node = JsonNode.Parse(plaintext)!; }
            finally { CryptographicOperations.ZeroMemory(plaintext); }
            update(node);
            byte[] updated = Encoding.UTF8.GetBytes(node.ToJsonString());
            try { await File.WriteAllBytesAsync(path, protector.Protect(updated), ct); }
            finally { CryptographicOperations.ZeroMemory(updated); }
        }
    }

    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class App : IAsyncDisposable
    {
        private readonly ServiceProvider _providers;
        public IAnalysisExecutionStore Store { get; }
        public ICaptureAssetCatalog Catalog { get; }
        public ICaptureMemoryService Memory { get; }
        public App(SmokeStorage storage, WindowsUserDataProtector protector)
        {
            var plan = CaptureAnalysisConfiguration.CreateDefault().SelectPlan(AnalysisMediaKind.Image,
                [AnalysisCapability.QrCodeDetection, AnalysisCapability.TextRecognition]);
            _providers = new ServiceCollection().AddGenericServices().AddApplicationServices()
                .AddSingleton<CaptureTool.Application.Abstractions.Storage.IStorageService>(storage)
                .AddSingleton<IUserDataProtector>(protector)
                .AddSingleton<IRecentCaptureCatalog>(new NoRecents())
                .AddSingleton<IRecentCapturesChangeNotifier>(new Notifier())
                .AddSingleton<ICaptureMemoryPrompts>(new Prompts())
                .AddSingleton(new CaptureAnalysisConfiguration([plan]))
                .AddWindowsAnalysisProviders().BuildServiceProvider();
            Store = _providers.GetRequiredService<IAnalysisExecutionStore>();
            Catalog = _providers.GetRequiredService<ICaptureAssetCatalog>();
            Memory = _providers.GetRequiredService<ICaptureMemoryService>();
        }
        public async ValueTask DisposeAsync()
        {
            await Memory.StopAsync();
            _providers.Dispose();
        }
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
    private sealed class Notifier : IRecentCapturesChangeNotifier
    {
        public event EventHandler? RecentCapturesChanged;
        public void NotifyRecentCapturesChanged() => RecentCapturesChanged?.Invoke(this, EventArgs.Empty);
    }
    private sealed class Prompts : ICaptureMemoryPrompts
    {
        public Task<bool> ConfirmAsync(CaptureMemoryPrompt prompt, CancellationToken ct) =>
            Task.FromResult(prompt == CaptureMemoryPrompt.Consent);
    }
}
