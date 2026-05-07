using CaptureTool.Domain.Capture.Interfaces.Metadata;
using CaptureTool.Domain.Capture.Interfaces.Metadata.Processing;
using FluentAssertions;

namespace CaptureTool.Application.Tests.Metadata;

[TestClass]
public class InsightEntryTests
{
    [TestMethod]
    public void Constructor_ShouldSetRequiredProperties()
    {
        // Arrange
        long timestamp = 10_000_000L;
        string category = "text-segment";
        string processorId = "test-processor";
        string title = "Hello world";

        // Act
        var entry = new InsightEntry(timestamp, category, processorId, title);

        // Assert
        entry.Timestamp.Should().Be(timestamp);
        entry.Category.Should().Be(category);
        entry.ProcessorId.Should().Be(processorId);
        entry.Title.Should().Be(title);
        entry.Description.Should().BeNull();
        entry.Duration.Should().BeNull();
        entry.Tags.Should().BeEmpty();
        entry.Confidence.Should().Be(1.0);
        entry.SourceEntryIds.Should().BeEmpty();
        entry.AdditionalData.Should().BeNull();
    }

    [TestMethod]
    public void Constructor_ShouldSetOptionalProperties()
    {
        // Arrange
        var tags = new List<string> { "ocr", "text" };
        var sourceIds = new List<string> { "scanner@12345" };
        var additionalData = new Dictionary<string, object?> { ["wordCount"] = 5 };

        // Act
        var entry = new InsightEntry(
            timestamp: 5_000_000L,
            category: "text-segment",
            processorId: "processor",
            title: "Title",
            description: "A description",
            duration: 10_000_000L,
            tags: tags,
            confidence: 0.8,
            sourceEntryIds: sourceIds,
            additionalData: additionalData);

        // Assert
        entry.Description.Should().Be("A description");
        entry.Duration.Should().Be(10_000_000L);
        entry.Tags.Should().BeEquivalentTo(["ocr", "text"]);
        entry.Confidence.Should().Be(0.8);
        entry.SourceEntryIds.Should().ContainSingle().Which.Should().Be("scanner@12345");
        entry.AdditionalData.Should().ContainKey("wordCount");
    }

    [TestMethod]
    public void Constructor_ShouldClampConfidence_WhenBelowZero()
    {
        var entry = new InsightEntry(0, "cat", "processor", "title", confidence: -1.0);
        entry.Confidence.Should().Be(0.0);
    }

    [TestMethod]
    public void Constructor_ShouldClampConfidence_WhenAboveOne()
    {
        var entry = new InsightEntry(0, "cat", "processor", "title", confidence: 5.0);
        entry.Confidence.Should().Be(1.0);
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenCategoryIsNull()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new InsightEntry(0, null!, "processor", "title"));
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenProcessorIdIsNull()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new InsightEntry(0, "cat", null!, "title"));
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenTitleIsNull()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new InsightEntry(0, "cat", "processor", null!));
    }
}
