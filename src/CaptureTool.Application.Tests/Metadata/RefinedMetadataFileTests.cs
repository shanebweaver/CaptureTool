using CaptureTool.Domain.Capture.Interfaces.Metadata;
using CaptureTool.Domain.Capture.Interfaces.Metadata.Processing;
using FluentAssertions;

namespace CaptureTool.Application.Tests.Metadata;

[TestClass]
public class RefinedMetadataFileTests
{
    [TestMethod]
    public void Constructor_ShouldSetProperties()
    {
        // Arrange
        string sourceFile = "C:\\test\\video.mp4";
        string sourceMetadata = "C:\\test\\video.metadata.json";
        var timestamp = DateTime.UtcNow;
        var insights = new List<InsightEntry>
        {
            new InsightEntry(1000L, "text-segment", "processor1", "Hello")
        };
        var processorInfo = new Dictionary<string, string> { ["processor1"] = "Test Processor" };

        // Act
        var file = new RefinedMetadataFile(sourceFile, sourceMetadata, timestamp, insights, processorInfo);

        // Assert
        file.SourceFilePath.Should().Be(sourceFile);
        file.SourceMetadataFilePath.Should().Be(sourceMetadata);
        file.ProcessingTimestamp.Should().Be(timestamp);
        file.Insights.Should().HaveCount(1);
        file.ProcessorInfo.Should().ContainKey("processor1");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenSourceFilePathIsNull()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new RefinedMetadataFile(null!, "meta.json", DateTime.UtcNow, [], new Dictionary<string, string>()));
    }

    [TestMethod]
    public void Constructor_ShouldAllowNullSourceMetadataFilePath()
    {
        // sourceMetadataFilePath is nullable (null when produced in-memory via ProcessAsync)
        var file = new RefinedMetadataFile("file.mp4", null, DateTime.UtcNow, [], new Dictionary<string, string>());
        file.SourceMetadataFilePath.Should().BeNull();
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenInsightsIsNull()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new RefinedMetadataFile("file.mp4", "meta.json", DateTime.UtcNow, null!, new Dictionary<string, string>()));
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenProcessorInfoIsNull()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new RefinedMetadataFile("file.mp4", "meta.json", DateTime.UtcNow, [], null!));
    }

    [TestMethod]
    public void FileExtension_ShouldBeCorrectValue()
    {
        RefinedMetadataFile.FileExtension.Should().Be(".insights.json");
    }
}
