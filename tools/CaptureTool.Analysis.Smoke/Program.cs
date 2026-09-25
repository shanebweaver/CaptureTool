using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Analysis;
using CaptureTool.Application.DependencyInjection;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows.DependencyInjection;
using CaptureTool.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AI.Foundry.Local;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.ApplicationModel;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Storage;

// This opt-in harness uses synthetic fixtures only. It does not enable the app's background scanning.
if (args.Length == 0 || !Path.IsPathFullyQualified(args[0]))
{
    Console.Error.WriteLine("Usage: CaptureTool.Analysis.Smoke <absolute-output-directory> [--prepare-whisper | --prepare-vision | --prepare-all | --recovery-checks | --scale-checks]");
    return 2;
}
string output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
if (args.Contains("--recovery-checks", StringComparer.Ordinal)) return await RecoveryChecks.RunAsync(output);
if (args.Contains("--recovery-child", StringComparer.Ordinal)) return await RecoveryChecks.ChildAsync(output, args[^1], resume: false);
if (args.Contains("--recovery-resume", StringComparer.Ordinal)) return await RecoveryChecks.ChildAsync(output, args[^1], resume: true);
if (args.Contains("--scale-checks", StringComparer.Ordinal)) return await ScaleChecks.RunAsync(output);
var storage = new SmokeStorage(output);
var services = new ServiceCollection().AddGenericServices().AddApplicationServices()
    .AddSingleton<IStorageService>(storage).AddWindowsAnalysisProviders();
using ServiceProvider provider = services.BuildServiceProvider();
IMediaAnalyzer[] analyzers = provider.GetServices<IMediaAnalyzer>().ToArray();
CaptureAnalysisConfiguration.CreateDefault().ValidateAnalyzers(analyzers.Select(analyzer => analyzer.Descriptor));
string imagePath = Path.Combine(output, "synthetic.png");
using (var bitmap = new Bitmap(1000, 350))
using (Graphics graphics = Graphics.FromImage(bitmap))
using (var font = new Font("Arial", 48))
{
    graphics.Clear(Color.White);
    graphics.DrawString("Capture analysis\nSmoke test 42", font, Brushes.Black, 35, 35);
    bitmap.Save(imagePath, ImageFormat.Png);
}
string visionPath = Path.Combine(output, "synthetic-shapes.png");
string visionVideoPath = Path.Combine(output, "synthetic-shapes.mp4");
using (var bitmap = new Bitmap(384, 256))
using (Graphics graphics = Graphics.FromImage(bitmap))
{
    graphics.Clear(Color.White);
    graphics.FillRectangle(Brushes.Red, 32, 64, 112, 112);
    graphics.FillEllipse(Brushes.Blue, 224, 64, 112, 112);
    bitmap.Save(visionPath, ImageFormat.Png);
}
if (!File.Exists(visionVideoPath))
{
    var composition = new MediaComposition();
    composition.Clips.Add(await MediaClip.CreateFromImageFileAsync(await StorageFile.GetFileFromPathAsync(visionPath), TimeSpan.FromSeconds(2)));
    await File.WriteAllBytesAsync(visionVideoPath, []);
    var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga);
    profile.Audio = null;
    var transcode = await composition.RenderToFileAsync(await StorageFile.GetFileFromPathAsync(visionVideoPath),
        MediaTrimmingPreference.Precise, profile);
    if (transcode != Windows.Media.Transcoding.TranscodeFailureReason.None) throw new InvalidOperationException("Synthetic vision video creation failed.");
}
string audioPath = Path.Combine(output, "synthetic.wav");
string videoPath = Path.Combine(output, "synthetic.mp4");
if (File.Exists(audioPath) && !File.Exists(videoPath))
{
    var composition = new MediaComposition();
    StorageFile audioFile = await StorageFile.GetFileFromPathAsync(audioPath);
    BackgroundAudioTrack audio = await BackgroundAudioTrack.CreateFromFileAsync(audioFile);
    composition.Clips.Add(await MediaClip.CreateFromImageFileAsync(await StorageFile.GetFileFromPathAsync(imagePath), audio.OriginalDuration));
    composition.BackgroundAudioTracks.Add(audio);
    await File.WriteAllBytesAsync(videoPath, []);
    var transcode = await composition.RenderToFileAsync(await StorageFile.GetFileFromPathAsync(videoPath),
        MediaTrimmingPreference.Precise, MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga));
    if (transcode != Windows.Media.Transcoding.TranscodeFailureReason.None) throw new InvalidOperationException("Synthetic video creation failed.");
}
List<SmokeResult> results = [];
bool packaged;
try { _ = Package.Current.Id.FullName; packaged = true; } catch (InvalidOperationException) { packaged = false; }
foreach (IMediaAnalyzer analyzer in analyzers)
{
    foreach (AnalysisMediaKind kind in analyzer.Descriptor.SupportedMedia)
    {
        string path = kind switch { AnalysisMediaKind.Image => imagePath, AnalysisMediaKind.Audio => Path.Combine(output, "synthetic.wav"), _ => Path.Combine(output, "synthetic.mp4") };
        bool vision = analyzer.Descriptor.Id == "foundry-local-image-description";
        if (vision) path = kind == AnalysisMediaKind.Image ? visionPath : visionVideoPath;
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        string status;
        int? items = null;
        string? modelId = null;
        string? diagnostic = null;
        string? descriptionText = null;
        try
        {
            AnalyzerAvailability readiness = await analyzer.GetAvailabilityAsync(kind, "en", budget.Token);
            bool prepare = args.Contains("--prepare-all", StringComparer.Ordinal) ||
                args.Contains("--prepare-vision", StringComparer.Ordinal) && vision ||
                args.Contains("--prepare-whisper", StringComparer.Ordinal) && analyzer.Descriptor.Id == "foundry-local-speech-transcript";
            if (readiness == AnalyzerAvailability.PreparationRequired && prepare)
                readiness = await analyzer.PrepareAsync(null, budget.Token);
            status = readiness.ToString();
            if (readiness == AnalyzerAvailability.Ready && File.Exists(path))
            {
                await using var source = File.OpenRead(path);
                var revision = new SourceRevision(Convert.ToHexStringLower(await SHA256.HashDataAsync(source, budget.Token)));
                AnalyzerOutcome outcome = await analyzer.AnalyzeAsync(new(CaptureId.New(), kind, revision, path, "en"), null, budget.Token);
                status = outcome.Kind + (outcome.FailureCode == null ? "" : ":" + outcome.FailureCode);
                modelId = outcome.Producer?.ModelId;
                items = outcome.Payload switch
                {
                    FileDetailsMetadata => 1,
                    TextRecognitionMetadata text => text.Regions.Count,
                    QrCodeMetadata qr => qr.Codes.Count,
                    DescriptionMetadata description => description.Descriptions.Count,
                    TranscriptMetadata transcript => transcript.Segments.Count,
                    _ => null,
                };
                if (outcome.Payload is TextRecognitionMetadata recognized && !recognized.Regions.Any(region => region.Text.Contains("Capture", StringComparison.OrdinalIgnoreCase)))
                    status = "FixtureMismatch";
                if (vision && outcome.Payload is DescriptionMetadata described)
                {
                    descriptionText = string.Join(" ", described.Descriptions.Select(item => item.Text));
                    if (described.Descriptions.Count != 1 ||
                        !descriptionText.Contains("red", StringComparison.OrdinalIgnoreCase) ||
                        !descriptionText.Contains("blue", StringComparison.OrdinalIgnoreCase) ||
                        kind == AnalysisMediaKind.Video && described.Descriptions[0].Timestamp != TimeSpan.Zero)
                        status = "FixtureMismatch";
                }
            }
            else if (readiness == AnalyzerAvailability.Ready) status = "MissingFixture";
        }
        catch (Exception exception) { status = "Error:" + exception.GetType().Name + ":" + exception.HResult.ToString("X8"); diagnostic = exception.ToString(); }
        results.Add(new(analyzer.Descriptor.Id, kind.ToString(), status, items, modelId, diagnostic, descriptionText));
        Console.WriteLine($"{analyzer.Descriptor.Id} {kind}: {status}");
        await File.WriteAllTextAsync(Path.Combine(output, "results.json"), JsonSerializer.Serialize(Report(), SmokeJsonContext.Default.SmokeReport));
    }
}
results.AddRange(await AudioChecks.RunAsync(analyzers, output, storage.GetApplicationScratchFolderPath()));
// Exercise a real native command failure as well as successful transcription under AOT.
if (FoundryLocalManager.IsInitialized)
{
    ICatalog catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
    IModel? model = await catalog.GetModelAsync("whisper-tiny");
    IModel? cpu = model?.Variants.FirstOrDefault(variant => variant.Info.Runtime?.DeviceType == DeviceType.CPU);
    if (model != null && cpu != null)
    {
        model.SelectVariant(cpu);
        if (await model.IsCachedAsync())
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            await model.LoadAsync(budget.Token);
            try
            {
                OpenAIAudioClient client = await model.GetAudioClientAsync(budget.Token);
                try
                {
                    await client.TranscribeAudioAsync(Path.Combine(output, "missing-fixture.wav"), budget.Token);
                    results.Add(new("foundry-native-error", "Audio", "FixtureMismatch", null, model.Id, null));
                }
                catch (FoundryLocalException) { results.Add(new("foundry-native-error", "Audio", "Succeeded", null, model.Id, null)); }
            }
            finally { await model.UnloadAsync(); }
        }
    }
}
IMediaAnalyzer legacy = analyzers.Single(analyzer => analyzer.Descriptor.Id == "windows-ocr-document");
if (await legacy.GetAvailabilityAsync(AnalysisMediaKind.Image, "en", default) == AnalyzerAvailability.Ready)
{
    string corrupt = Path.Combine(output, "invalid.png");
    await File.WriteAllTextAsync(corrupt, "Synthetic malformed image fixture");
    var outcome = await legacy.AnalyzeAsync(new(CaptureId.New(), AnalysisMediaKind.Image, new SourceRevision(new string('a', 64)), corrupt, "en"), null, default);
    results.Add(new("invalid-image", "Image", outcome.Kind == AnalyzerOutcomeKind.InvalidSource ? "Succeeded" : "FixtureMismatch", null, null, null));
}
await File.WriteAllTextAsync(Path.Combine(output, "results.json"), JsonSerializer.Serialize(Report(), SmokeJsonContext.Default.SmokeReport));
int exitCode = results.Any(result => result.Status.StartsWith("Error:", StringComparison.Ordinal) ||
    result.Status is "FixtureMismatch" or "MissingFixture" || result.Status.StartsWith("Failed", StringComparison.Ordinal)) ? 1 : 0;
await File.WriteAllTextAsync(Path.Combine(output, "exit-code.txt"), exitCode.ToString());
return exitCode;

SmokeReport Report() => new(packaged, results.ToArray(), RuntimeInformation.OSDescription,
    RuntimeInformation.OSArchitecture.ToString(), RuntimeInformation.ProcessArchitecture.ToString(), DateTimeOffset.UtcNow);

internal sealed record SmokeResult(string Analyzer, string Media, string Status, int? Items, string? ActualModel, string? Diagnostic,
    string? SyntheticDescription = null, string? SyntheticTranscript = null);
internal sealed record SmokeReport(bool Packaged, SmokeResult[] Results, string OS, string OSArchitecture,
    string ProcessArchitecture, DateTimeOffset RecordedAt);
[JsonSerializable(typeof(SmokeReport))]
internal partial class SmokeJsonContext : JsonSerializerContext;

internal sealed class SmokeStorage(string root) : IStorageService
{
    public string GetApplicationDataFolderPath() => Path.Combine(root, "data");
    public string GetApplicationScratchFolderPath() => Path.Combine(root, "scratch");
    public string GetApplicationRetainedCaptureFolderPath() => Path.Combine(root, "captures");
    public string GetSystemDefaultScreenshotsFolderPath() => root;
    public string GetSystemDefaultMusicFolderPath() => root;
    public string GetSystemDefaultVideosFolderPath() => root;
    public string GetTemporaryFileName() => Guid.NewGuid().ToString("N");
}
