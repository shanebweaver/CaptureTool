using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.Domain.Capture.Windows.Metadata.Processing.Processors;
using FluentAssertions;

namespace CaptureTool.Domain.Capture.Tests.Windows.Metadata.Processing;

[TestClass]
public class AudioLevelProcessorTests
{
    [TestMethod]
    public async Task ProcessAsync_ShouldIgnoreEntriesBelowFrameThreshold()
    {
        var processor = new AudioLevelProcessor();
        var entries = new List<MetadataEntry>
        {
            CreateAudioEntry(10_000_000, 100)
        };

        var insights = await processor.ProcessAsync(entries);

        insights.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ProcessAsync_ShouldMergeNearbyAudioActivity()
    {
        var processor = new AudioLevelProcessor();
        var entries = new List<MetadataEntry>
        {
            CreateAudioEntry(10_000_000, 512),
            CreateAudioEntry(14_000_000, 512)
        };

        var insights = await processor.ProcessAsync(entries);

        insights.Should().ContainSingle();
        insights[0].Category.Should().Be("audio-activity");
        insights[0].Duration.Should().Be(4_000_000);
    }

    [TestMethod]
    public async Task ProcessAsync_ShouldSplitDistantAudioActivity()
    {
        var processor = new AudioLevelProcessor();
        var entries = new List<MetadataEntry>
        {
            CreateAudioEntry(10_000_000, 512),
            CreateAudioEntry(20_000_000, 512)
        };

        var insights = await processor.ProcessAsync(entries);

        insights.Should().HaveCount(2);
    }

    private static MetadataEntry CreateAudioEntry(long timestamp, int numFrames)
    {
        return new MetadataEntry(
            timestamp,
            "basic-audio-sample",
            "audio-info",
            "48000Hz 2ch",
            new Dictionary<string, object?> { ["numFrames"] = numFrames });
    }
}
