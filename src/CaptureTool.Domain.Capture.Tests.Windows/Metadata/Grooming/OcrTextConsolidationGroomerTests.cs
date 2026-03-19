using CaptureTool.Domain.Capture.Implementations.Windows.Metadata.Grooming.Groomers;
using CaptureTool.Domain.Capture.Interfaces.Metadata;
using FluentAssertions;

namespace CaptureTool.Domain.Capture.Tests.Windows.Metadata.Grooming;

[TestClass]
public class OcrTextConsolidationGroomerTests
{
    private OcrTextConsolidationGroomer _groomer = null!;

    [TestInitialize]
    public void Setup()
    {
        _groomer = new OcrTextConsolidationGroomer();
    }

    [TestMethod]
    public void GroomerId_ShouldBeCorrectValue()
    {
        _groomer.GroomerId.Should().Be("ocr-text-consolidation");
    }

    [TestMethod]
    public void SupportedKeys_ShouldContainOcrText()
    {
        _groomer.SupportedKeys.Should().ContainSingle().Which.Should().Be("ocr-text");
    }

    [TestMethod]
    public async Task GroomAsync_ShouldReturnEmpty_WhenNoEntries()
    {
        var result = await _groomer.GroomAsync([]);
        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GroomAsync_ShouldCreateSingleInsight_ForIsolatedEntry()
    {
        var entries = new List<MetadataEntry>
        {
            new MetadataEntry(10_000_000L, "windows-ocr", "ocr-text", "Hello world")
        };

        var result = await _groomer.GroomAsync(entries);

        result.Should().ContainSingle();
        result[0].Category.Should().Be("text-segment");
        result[0].GroomerId.Should().Be("ocr-text-consolidation");
        result[0].Timestamp.Should().Be(10_000_000L);
        result[0].Description.Should().Be("Hello world");
    }

    [TestMethod]
    public async Task GroomAsync_ShouldMergeConsecutiveSimilarEntries()
    {
        // Two entries within 1 second, sharing >40% of words → should merge
        var entries = new List<MetadataEntry>
        {
            new MetadataEntry(10_000_000L, "windows-ocr", "ocr-text", "Hello world screen"),
            new MetadataEntry(15_000_000L, "windows-ocr", "ocr-text", "Hello world again")
        };

        var result = await _groomer.GroomAsync(entries);

        // Both share "Hello world" (2 of 3 words) → similarity > 40% → merged
        result.Should().ContainSingle();
        result[0].Duration.Should().Be(15_000_000L - 10_000_000L);
    }

    [TestMethod]
    public async Task GroomAsync_ShouldNotMergeDistantEntries()
    {
        // Two entries more than 1 second apart → separate insights
        var entries = new List<MetadataEntry>
        {
            new MetadataEntry(10_000_000L, "windows-ocr", "ocr-text", "Hello world"),
            new MetadataEntry(25_000_000L, "windows-ocr", "ocr-text", "Hello world")
        };

        var result = await _groomer.GroomAsync(entries);

        result.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task GroomAsync_ShouldNotMergeEntriesWithDifferentText()
    {
        // Two nearby entries with completely different text → separate insights
        var entries = new List<MetadataEntry>
        {
            new MetadataEntry(10_000_000L, "windows-ocr", "ocr-text", "Apple banana cherry"),
            new MetadataEntry(12_000_000L, "windows-ocr", "ocr-text", "Dog cat fish bird")
        };

        var result = await _groomer.GroomAsync(entries);

        result.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task GroomAsync_ShouldIgnoreShortText()
    {
        var entries = new List<MetadataEntry>
        {
            new MetadataEntry(10_000_000L, "windows-ocr", "ocr-text", "Hi")  // < 3 chars filtered
        };

        var result = await _groomer.GroomAsync(entries);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GroomAsync_ShouldOrderInsightsByTimestamp()
    {
        var entries = new List<MetadataEntry>
        {
            new MetadataEntry(30_000_000L, "windows-ocr", "ocr-text", "Second entry text"),
            new MetadataEntry(10_000_000L, "windows-ocr", "ocr-text", "First entry text here")
        };

        var result = await _groomer.GroomAsync(entries);

        result.Should().HaveCount(2);
        result[0].Timestamp.Should().BeLessThan(result[1].Timestamp);
    }
}
