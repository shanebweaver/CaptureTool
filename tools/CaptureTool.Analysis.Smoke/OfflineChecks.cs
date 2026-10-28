using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.DependencyInjection;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows;
using CaptureTool.Infrastructure.Analysis.Windows.Foundry;
using CaptureTool.Infrastructure.Analysis.Windows.Media;
using CaptureTool.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Drawing;
using System.Drawing.Imaging;

// Forces the application's offline decision; never changes the machine's network settings.
internal static class OfflineChecks
{
    public static async Task<int> RunAsync(string root)
    {
        Directory.CreateDirectory(root);
        var storage = new SmokeStorage(root);
        using var services = new ServiceCollection().AddGenericServices().AddApplicationServices()
            .AddSingleton<IStorageService>(storage).BuildServiceProvider();
        using var runtime = new FoundryRuntime(storage, downloads: new(() => true));
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        if (runtime.IsSupported)
        {
            try
            {
                await runtime.ResolveAsync("whisper-tiny", budget.Token);
                throw new InvalidOperationException("A missing model must require a download.");
            }
            catch (ModelDownloadRequiredException) { Console.WriteLine("Fresh offline model: download skipped."); }
        }
        if (Microsoft.AI.Foundry.Local.FoundryLocalManager.IsInitialized)
        {
            throw new InvalidOperationException("Offline first use must not initialize Foundry.");
        }

        string path = Path.Combine(root, "offline-ocr.png");
        using (var bitmap = new Bitmap(800, 200))
        using (var graphics = Graphics.FromImage(bitmap))
        using (var font = new Font("Arial", 48))
        {
            graphics.Clear(Color.White);
            graphics.DrawString("Offline test 42", font, Brushes.Black, 25, 40);
            bitmap.Save(path, ImageFormat.Png);
        }
        var media = new WindowsAnalysisMedia(services.GetRequiredService<IScratchArtifactStore>());
        var ocr = new WindowsImageAnalyzer("offline-ocr", WindowsImageModel.LegacyOcr,
            AnalysisMediaKind.Image, media, runtime, downloads: new(() => true));
        var available = await ocr.GetAvailabilityAsync(AnalysisMediaKind.Image, "en-US", budget.Token);
        if (available != AnalyzerAvailability.Ready)
        {
            throw new InvalidOperationException("Install the Windows English OCR language pack to run this check.");
        }
        var outcome = await ocr.AnalyzeAsync(new(CaptureId.New(), AnalysisMediaKind.Image,
            new SourceRevision(new string('a', 64)), path, "en-US"), null, budget.Token);
        if (outcome.Payload is not TextRecognitionMetadata text ||
            !string.Join(" ", text.Regions.Select(region => region.Text)).Contains("Offline test 42", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Local OCR did not recognize the synthetic fixture while offline.");
        }
        Console.WriteLine("Local OCR: recognized the fixture while offline, with no model download.");
        return 0;
    }
}
