using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Infrastructure.Analysis.Windows;
using CaptureTool.Infrastructure.Analysis.Windows.DependencyInjection;
using CaptureTool.Infrastructure.Analysis.Windows.Media;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Windows.Tests.Analysis;

[TestClass]
public sealed class QrCodeAnalyzerTests
{
    public TestContext TestContext { get; set; } = null!;
    private CancellationToken Ct => TestContext.CancellationToken;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CaptureToolQrTests", Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [TestMethod]
    public void DefaultCompositionIncludesQrAnalyzersWithoutDependingOnAnOcrModel()
    {
        using var services = new ServiceCollection().AddSingleton(Mock.Of<IStorageService>()).AddSingleton(Mock.Of<IScratchArtifactStore>())
            .AddSingleton<IMetadataProcessor, StructuredFactsProcessor>()
            .AddWindowsAnalysisProviders(MetadataEnrichmentConfiguration.SemanticModels).BuildServiceProvider();
        var analyzers = services.GetServices<IMediaAnalyzer>().ToArray();
        CaptureAnalysisConfiguration.CreateDefault().ValidateAnalyzers(analyzers.Select(analyzer => analyzer.Descriptor), services.GetServices<IMetadataProcessor>().Select(processor => processor.Descriptor));
        Assert.HasCount(2, analyzers.Where(analyzer => analyzer.Descriptor.Capability == AnalysisCapability.QrCodeDetection).ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ScansEncodedImagesWithNoPreparationAndPublishesEmptySuccess(bool empty)
    {
        string path = await CreateImageAsync(empty);
        var analyzer = Analyzer(AnalysisMediaKind.Image);
        Assert.AreEqual(AnalyzerAvailability.Ready, await analyzer.GetAvailabilityAsync(AnalysisMediaKind.Image, null, Ct));
        Assert.AreEqual(AnalyzerAvailability.Ready, await analyzer.PrepareAsync(null, Ct));
        var result = await analyzer.AnalyzeAsync(Input(path, AnalysisMediaKind.Image), null, Ct);
        Assert.AreEqual(AnalyzerOutcomeKind.Succeeded, result.Kind);
        var codes = ((QrCodeMetadata)result.Payload!).Codes;
        Assert.HasCount(empty ? 0 : 1, codes);
        if (!empty)
        {
            Assert.AreEqual("https://example.com/qr-fixture", codes.Single().Value);
            Assert.IsNull(codes.Single().Timestamp);
            Assert.IsGreaterThan(.5, codes.Single().Bounds.Width);
        }
        Assert.AreEqual("zxing", result.Producer!.ProviderId);
        Assert.IsFalse(string.IsNullOrEmpty(result.Producer.ModelVersion));
    }

    [TestMethod]
    public async Task ScansVideoFramesAndRetainsEachOccurrenceTimestamp()
    {
        string image = await CreateImageAsync(false);
        string path = Path.Combine(_root, "qr.mp4");
        var composition = new MediaComposition();
        composition.Clips.Add(await MediaClip.CreateFromImageFileAsync(await StorageFile.GetFileFromPathAsync(image), TimeSpan.FromSeconds(6)));
        await File.WriteAllBytesAsync(path, [], Ct);
        var transcode = await composition.RenderToFileAsync(await StorageFile.GetFileFromPathAsync(path),
            MediaTrimmingPreference.Precise, MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga));
        Assert.AreEqual(global::Windows.Media.Transcoding.TranscodeFailureReason.None, transcode);
        var result = await Analyzer(AnalysisMediaKind.Video).AnalyzeAsync(Input(path, AnalysisMediaKind.Video), null, Ct);
        Assert.AreEqual(AnalyzerOutcomeKind.Succeeded, result.Kind);
        var codes = ((QrCodeMetadata)result.Payload!).Codes;
        Assert.HasCount(2, codes);
        Assert.IsTrue(codes.All(code => code.Value == "https://example.com/qr-fixture"));
        Assert.AreEqual(TimeSpan.Zero, codes[0].Timestamp);
        Assert.IsTrue(codes[1].Timestamp > TimeSpan.Zero);
    }

    [TestMethod]
    public async Task RejectsUnsupportedMediaAndInvalidSourcesAndHonorsCancellation()
    {
        var analyzer = Analyzer(AnalysisMediaKind.Image);
        Assert.AreEqual(AnalyzerAvailability.Unsupported, await analyzer.GetAvailabilityAsync(AnalysisMediaKind.Audio, null, Ct));
        Assert.AreEqual(AnalyzerOutcomeKind.Unsupported, (await analyzer.AnalyzeAsync(Input("unused", AnalysisMediaKind.Audio), null, Ct)).Kind);
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "invalid.png");
        await File.WriteAllTextAsync(path, "not an image", Ct);
        Assert.AreEqual(AnalyzerOutcomeKind.InvalidSource, (await analyzer.AnalyzeAsync(Input(path, AnalysisMediaKind.Image), null, Ct)).Kind);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => analyzer.AnalyzeAsync(Input(path, AnalysisMediaKind.Image), null, canceled.Token));
    }

    private static QrCodeAnalyzer Analyzer(AnalysisMediaKind kind) => new("qr-test", kind, new WindowsAnalysisMedia(Mock.Of<IScratchArtifactStore>()));
    private static AnalysisInput Input(string path, AnalysisMediaKind kind) => new(CaptureId.New(), kind, new(new string('a', 64)), path);

    private async Task<string> CreateImageAsync(bool empty)
    {
        Directory.CreateDirectory(_root);
        var writer = new ZXing.BarcodeWriterPixelData
        {
            Format = ZXing.BarcodeFormat.QR_CODE,
            Options = new ZXing.Common.EncodingOptions { Width = 240, Height = 240, Margin = 4 }
        };
        byte[] pixels = empty ? Enumerable.Repeat((byte)255, 240 * 240 * 4).ToArray() : writer.Write("https://example.com/qr-fixture").Pixels;
        using var encoded = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, encoded);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, 240, 240, 96, 96, pixels);
        await encoder.FlushAsync();
        encoded.Seek(0);
        string path = Path.Combine(_root, "qr.png");
        using var source = encoded.AsStreamForRead();
        await using var file = File.Create(path);
        await source.CopyToAsync(file, Ct);
        return path;
    }
}
