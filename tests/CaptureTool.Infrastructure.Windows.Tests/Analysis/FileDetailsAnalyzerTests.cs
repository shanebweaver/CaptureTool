using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows;
using CaptureTool.Infrastructure.Analysis.Windows.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Windows.Tests.Analysis;

[TestClass]
public sealed class FileDetailsAnalyzerTests
{
    public TestContext TestContext { get; set; } = null!;
    private CancellationToken Ct => TestContext.CancellationToken;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CaptureToolFileDetailsTests", Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset CapturedAt = new(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [TestCleanup]
    public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [TestMethod]
    public async Task DefaultPlansUseOneAlwaysReadyLocalAdapterForEveryMediaKind()
    {
        using var services = new ServiceCollection().AddSingleton(Mock.Of<IStorageService>()).AddWindowsAnalysisProviders().BuildServiceProvider();
        var analyzers = services.GetServices<IMediaAnalyzer>().ToArray();
        var configuration = CaptureAnalysisConfiguration.CreateDefault();
        configuration.ValidateAnalyzers(analyzers.Select(analyzer => analyzer.Descriptor));
        var analyzer = analyzers.Single(item => item.Descriptor.Capability == AnalysisCapability.FileDetails);
        foreach (var kind in Enum.GetValues<AnalysisMediaKind>())
            Assert.AreEqual(AnalyzerAvailability.Ready, await analyzer.GetAvailabilityAsync(kind, null, Ct));
        Assert.AreEqual(AnalyzerAvailability.Ready, await analyzer.PrepareAsync(null, Ct));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadsOriginalImageDimensionsAndOrientationWithoutLosingFileFacts(bool rotated)
    {
        string path = await CreateImageAsync(rotated);
        var details = await ScanAsync(path, AnalysisMediaKind.Image);
        Assert.AreEqual(new FileInfo(path).Length, details.SizeBytes);
        Assert.AreEqual(Path.GetFileName(path), details.FileName);
        Assert.AreEqual(new DateTimeOffset(File.GetCreationTimeUtc(path)), details.FileCreatedAt);
        Assert.AreEqual(new DateTimeOffset(File.GetLastWriteTimeUtc(path)), details.FileModifiedAt);
        Assert.AreEqual(CapturedAt, details.CapturedAt);
        Assert.AreNotEqual(details.FileCreatedAt, details.CapturedAt);
        Assert.AreEqual(rotated ? "image/jpeg" : "image/png", details.ContentType);
        Assert.IsNotNull(details.Image);
        Assert.AreEqual(rotated ? 1024u : 3072u, details.Image.Dimensions.Width);
        Assert.AreEqual(rotated ? 3072u : 1024u, details.Image.Dimensions.Height);
        Assert.AreEqual(rotated ? 1d / 3 : 3d, details.Image.Dimensions.AspectRatio);
        Assert.IsNull(details.Duration);
        Assert.IsNull(details.Video);
        Assert.IsNull(details.Audio);
    }

    [TestMethod]
    public async Task ReadsWavDurationAndAudioProperties()
    {
        var details = await ScanAsync(await CreateAudioAsync(), AnalysisMediaKind.Audio);
        Assert.AreEqual(TimeSpan.FromSeconds(2), details.Duration);
        Assert.IsNotNull(details.Audio);
        Assert.AreEqual(2u, details.Audio.Channels);
        Assert.AreEqual(48000u, details.Audio.SampleRate);
        Assert.AreEqual(1536000u, details.Audio.Bitrate);
        Assert.IsFalse(string.IsNullOrWhiteSpace(details.Audio.Codec));
        Assert.IsNull(details.Video);
        Assert.IsNull(details.Image);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadsVideoDurationDimensionsFrameRateAndOptionalAudio(bool withAudio)
    {
        Directory.CreateDirectory(_root);
        var composition = new MediaComposition();
        composition.Clips.Add(MediaClip.CreateFromColor(global::Windows.UI.Color.FromArgb(255, 100, 149, 237), TimeSpan.FromSeconds(2)));
        if (withAudio)
            composition.BackgroundAudioTracks.Add(await BackgroundAudioTrack.CreateFromFileAsync(await StorageFile.GetFileFromPathAsync(await CreateAudioAsync())));
        string path = Path.Combine(_root, "video.mp4");
        await File.WriteAllBytesAsync(path, [], Ct);
        var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga);
        if (!withAudio) profile.Audio = null;
        var transcode = await composition.RenderToFileAsync(await StorageFile.GetFileFromPathAsync(path), MediaTrimmingPreference.Precise, profile);
        Assert.AreEqual(global::Windows.Media.Transcoding.TranscodeFailureReason.None, transcode);
        var details = await ScanAsync(path, AnalysisMediaKind.Video);
        Assert.IsNotNull(details.Video);
        Assert.AreEqual(profile.Video.Width, details.Video.Dimensions.Width);
        Assert.AreEqual(profile.Video.Height, details.Video.Dimensions.Height);
        // Windows' short synthetic render can contain an extra endpoint frame.
        Assert.IsNotNull(details.Video.FrameRate);
        Assert.AreEqual(30d, details.Video.FrameRate.Value, 1d);
        Assert.IsLessThan(.1, Math.Abs(details.Duration!.Value.TotalSeconds - 2));
        Assert.AreEqual("H264", details.Video.Codec);
        Assert.IsNull(details.Image);
        if (withAudio)
        {
            Assert.IsNotNull(details.Audio);
            Assert.IsNotNull(details.Audio.SampleRate);
            Assert.IsNotNull(details.Audio.Channels);
            Assert.AreEqual("AAC", details.Audio.Codec);
        }
        else Assert.IsNull(details.Audio);
    }

    [TestMethod]
    [DataRow(AnalysisMediaKind.Image)]
    [DataRow(AnalysisMediaKind.Audio)]
    [DataRow(AnalysisMediaKind.Video)]
    public async Task UnreadableMediaHeadersKeepFileFactsAndUnknownCaptureDate(AnalysisMediaKind kind)
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "unknown.dat");
        await File.WriteAllTextAsync(path, "invalid media header", Ct);
        var outcome = await new FileDetailsAnalyzer().AnalyzeAsync(Input(path, kind) with { CapturedAt = null }, null, Ct);
        Assert.AreEqual(AnalyzerOutcomeKind.Succeeded, outcome.Kind);
        var details = (FileDetailsMetadata)outcome.Payload!;
        Assert.AreEqual(new FileInfo(path).Length, details.SizeBytes);
        Assert.IsNull(details.CapturedAt);
        Assert.IsNull(details.Image);
        Assert.IsNull(details.Audio);
        Assert.IsNull(details.Video);
        Assert.IsNull(details.Duration);
    }

    [TestMethod]
    public async Task MissingFileAndCancellationNeverPublishMetadata()
    {
        var analyzer = new FileDetailsAnalyzer();
        var input = Input(Path.Combine(_root, "missing.png"), AnalysisMediaKind.Image);
        Assert.AreEqual(AnalyzerOutcomeKind.InvalidSource, (await analyzer.AnalyzeAsync(input, null, Ct)).Kind);
        Assert.AreEqual(AnalyzerAvailability.Unsupported, await analyzer.GetAvailabilityAsync((AnalysisMediaKind)99, null, Ct));
        Assert.AreEqual(AnalyzerOutcomeKind.Unsupported, (await analyzer.AnalyzeAsync(input with { MediaKind = (AnalysisMediaKind)99 }, null, Ct)).Kind);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => analyzer.AnalyzeAsync(input, null, canceled.Token));
    }

    private async Task<FileDetailsMetadata> ScanAsync(string path, AnalysisMediaKind kind)
    {
        var outcome = await new FileDetailsAnalyzer().AnalyzeAsync(Input(path, kind), null, Ct);
        Assert.AreEqual(AnalyzerOutcomeKind.Succeeded, outcome.Kind);
        Assert.AreEqual("file-properties", outcome.Producer!.ModelId);
        return (FileDetailsMetadata)outcome.Payload!;
    }

    private static AnalysisInput Input(string path, AnalysisMediaKind kind) => new(CaptureId.New(), kind, new(new string('a', 64)), path, CapturedAt: CapturedAt);

    private async Task<string> CreateImageAsync(bool rotated)
    {
        Directory.CreateDirectory(_root);
        using var encoded = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(rotated ? BitmapEncoder.JpegEncoderId : BitmapEncoder.PngEncoderId, encoded);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, 3072, 1024, 96, 96, new byte[3072 * 1024 * 4]);
        if (rotated)
            await encoder.BitmapProperties.SetPropertiesAsync(new BitmapPropertySet { ["System.Photo.Orientation"] = new BitmapTypedValue((ushort)6, PropertyType.UInt16) });
        await encoder.FlushAsync();
        encoded.Seek(0);
        string path = Path.Combine(_root, rotated ? "image.jpg" : "image.png");
        using var source = encoded.AsStreamForRead();
        await using var file = File.Create(path);
        await source.CopyToAsync(file, Ct);
        return path;
    }

    private async Task<string> CreateAudioAsync()
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "audio.wav");
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
        {
            const int bytes = 48000 * 2 * 2 * 2;
            writer.Write("RIFF"u8); writer.Write(36 + bytes); writer.Write("WAVEfmt "u8);
            writer.Write(16); writer.Write((short)1); writer.Write((short)2); writer.Write(48000);
            writer.Write(192000); writer.Write((short)4); writer.Write((short)16);
            writer.Write("data"u8); writer.Write(bytes); writer.Write(new byte[bytes]);
        }
        await File.WriteAllBytesAsync(path, stream.ToArray(), Ct);
        return path;
    }
}
