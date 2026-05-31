using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.Domain.Capture.Windows.Metadata.Processing.Processors;
using FluentAssertions;

namespace CaptureTool.Domain.Capture.Tests.Windows.Metadata.Processing;

[TestClass]
public class OcrTextConsolidationProcessorTests
{
    [TestMethod]
    public async Task ProcessAsync_ShouldMergeNearbySimilarText()
    {
        var processor = new OcrTextConsolidationProcessor();
        var entries = new List<MetadataEntry>
        {
            new(10_000_000, "windows-ocr", "ocr-text", "Hello world screen"),
            new(15_000_000, "windows-ocr", "ocr-text", "Hello world again")
        };

        var insights = await processor.ProcessAsync(entries);

        insights.Should().ContainSingle();
        insights[0].Category.Should().Be("text-segment");
        insights[0].Duration.Should().Be(5_000_000);
    }

    [TestMethod]
    public async Task ProcessAsync_ShouldSplitDistantText()
    {
        var processor = new OcrTextConsolidationProcessor();
        var entries = new List<MetadataEntry>
        {
            new(10_000_000, "windows-ocr", "ocr-text", "Hello world"),
            new(25_000_000, "windows-ocr", "ocr-text", "Hello world")
        };

        var insights = await processor.ProcessAsync(entries);

        insights.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task ProcessAsync_ShouldIgnoreShortText()
    {
        var processor = new OcrTextConsolidationProcessor();

        var insights = await processor.ProcessAsync(
        [
            new MetadataEntry(10_000_000, "windows-ocr", "ocr-text", "Hi")
        ]);

        insights.Should().BeEmpty();
    }
}
