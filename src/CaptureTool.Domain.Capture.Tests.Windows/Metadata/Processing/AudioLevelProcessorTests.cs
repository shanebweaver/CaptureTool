using CaptureTool.Domain.Capture.Implementations.Windows.Metadata.Processing.Processors;
using CaptureTool.Domain.Capture.Interfaces.Metadata;
using FluentAssertions;

namespace CaptureTool.Domain.Capture.Tests.Windows.Metadata.Processing;

[TestClass]
public class AudioLevelProcessorTests
{
    private AudioLevelProcessor _processor = null!;

    [TestInitialize]
    public void Setup()
    {
        _processor = new AudioLevelProcessor();
    }

    [TestMethod]
    public void ProcessorId_ShouldBeCorrectValue()
    {
        _processor.ProcessorId.Should().Be("audio-level");
    }

    [TestMethod]
    public void SupportedKeys_ShouldContainAudioInfo()
    {
        _processor.SupportedKeys.Should().ContainSingle().Which.Should().Be("audio-info");
    }

    [TestMethod]
    public async Task ProcessAsync_ShouldReturnEmpty_WhenNoEntries()
    {
        var result = await _processor.ProcessAsync([]);
        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ProcessAsync_ShouldReturnEmpty_WhenNoEntriesAboveThreshold()
    {
        // numFrames = 100, below the 256 threshold
        var entries = new List<MetadataEntry>
        {
            new MetadataEntry(10_000_000L, "basic-audio-sample", "audio-info", "48000Hz 2ch",
                additionalData: new Dictionary<string, object?> { ["numFrames"] = 100 })
        };

        var result = await _processor.ProcessAsync(entries);
        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ProcessAsync_ShouldCreateSingleInsight_ForSingleActiveEntry()
    {
        var entries = new List<MetadataEntry>
        {
            new MetadataEntry(10_000_000L, "basic-audio-sample", "audio-info", "48000Hz 2ch",
                additionalData: new Dictionary<string, object?> { ["numFrames"] = 512 })
        };

        var result = await _processor.ProcessAsync(entries);

        result.Should().ContainSingle();
        result[0].Category.Should().Be("audio-activity");
        result[0].ProcessorId.Should().Be("audio-level");
        result[0].Timestamp.Should().Be(10_000_000L);
    }

    [TestMethod]
    public async Task ProcessAsync_ShouldMergeConsecutiveActiveEntries()
    {
        // Two entries within 0.5 s (5,000,000 ticks) → should merge
        var entries = new List<MetadataEntry>
        {
            new MetadataEntry(10_000_000L, "basic-audio-sample", "audio-info", "48000Hz 2ch",
                additionalData: new Dictionary<string, object?> { ["numFrames"] = 1024 }),
            new MetadataEntry(14_000_000L, "basic-audio-sample", "audio-info", "48000Hz 2ch",
                additionalData: new Dictionary<string, object?> { ["numFrames"] = 1024 })
        };

        var result = await _processor.ProcessAsync(entries);

        result.Should().ContainSingle();
        result[0].Duration.Should().Be(14_000_000L - 10_000_000L);
    }

    [TestMethod]
    public async Task ProcessAsync_ShouldSplitDistantActiveEntries()
    {
        // Gap > 0.5 s → two separate activity insights
        var entries = new List<MetadataEntry>
        {
            new MetadataEntry(10_000_000L, "basic-audio-sample", "audio-info", "48000Hz 2ch",
                additionalData: new Dictionary<string, object?> { ["numFrames"] = 1024 }),
            new MetadataEntry(20_000_000L, "basic-audio-sample", "audio-info", "48000Hz 2ch",
                additionalData: new Dictionary<string, object?> { ["numFrames"] = 1024 })
        };

        var result = await _processor.ProcessAsync(entries);

        result.Should().HaveCount(2);
    }
}
