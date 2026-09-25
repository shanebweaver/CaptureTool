using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Application.Tests.Analysis;

[TestClass]
public sealed class FileDetailsMetadataTests
{
    [TestMethod]
    public void FileDatesRemainDistinctAndUnknownMediaPropertiesRemainNull()
    {
        DateTimeOffset capturedAt = new(2020, 1, 2, 3, 4, 5, TimeSpan.FromHours(2));
        DateTimeOffset createdAt = capturedAt.AddDays(3);
        var metadata = new FileDetailsMetadata(AnalysisMediaKind.Image, "capture.png", 512, "image/png",
            createdAt, createdAt.AddHours(1), capturedAt, image: new(new(3840, 2160), 96, 96));
        Assert.AreEqual(16d / 9, metadata.Image!.Dimensions.AspectRatio);
        Assert.AreEqual(capturedAt.ToUniversalTime(), metadata.CapturedAt);
        Assert.AreEqual(TimeSpan.Zero, metadata.CapturedAt!.Value.Offset);
        Assert.AreEqual(createdAt.ToUniversalTime(), metadata.FileCreatedAt);
        Assert.AreNotEqual(metadata.FileCreatedAt, metadata.CapturedAt);
        Assert.IsNull(metadata.Duration);
        Assert.IsNull(metadata.Audio);
        Assert.IsNull(metadata.Video);
        Assert.IsTrue(metadata.Supports(AnalysisMediaKind.Image));
        Assert.IsFalse(metadata.Supports(AnalysisMediaKind.Audio));
        var unknown = new FileDetailsMetadata(AnalysisMediaKind.Video, "unknown.mp4", 0, null, createdAt, createdAt);
        Assert.IsNull(unknown.CapturedAt);
        Assert.IsNull(unknown.Video);
        Assert.IsNull(unknown.Duration);
    }

    [TestMethod]
    public void InvalidPropertiesAndMediaCombinationsCannotBeStored()
    {
        var date = DateTimeOffset.UtcNow;
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MediaDimensions(0, 10));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ImageFileDetails(new(10, 20), double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new VideoFileDetails(new(10, 20), double.PositiveInfinity));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AudioFileDetails(sampleRate: 0));
        Assert.ThrowsExactly<ArgumentException>(() => new AudioFileDetails(codec: ""));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new FileDetailsMetadata(AnalysisMediaKind.Audio, "a.wav", -1, null, date, date));
        Assert.ThrowsExactly<ArgumentException>(() => new FileDetailsMetadata(AnalysisMediaKind.Image, "folder/a.png", 1, null, date, date));
        Assert.ThrowsExactly<ArgumentException>(() => new FileDetailsMetadata(AnalysisMediaKind.Image, "a.png", 1, null, date, date, duration: TimeSpan.FromSeconds(1)));
        Assert.ThrowsExactly<ArgumentException>(() => new FileDetailsMetadata(AnalysisMediaKind.Audio, "a.wav", 1, null, date, date, image: new(new(1, 1))));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new FileDetailsMetadata(AnalysisMediaKind.Video, "a.mp4", 1, null, date, date, duration: TimeSpan.Zero));
    }
}
