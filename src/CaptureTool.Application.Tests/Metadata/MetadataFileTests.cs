using CaptureTool.Domains.Capture.Interfaces.Metadata;
using FluentAssertions;

namespace CaptureTool.Application.Tests.Metadata;

[TestClass]
public class MetadataFileTests
{
    [TestMethod]
    public void Constructor_ShouldSetProperties()
    {
        // Arrange
        string sourceFilePath = "C:\\test\\video.mp4";
        var scanTimestamp = DateTime.UtcNow;
        var entries = new List<MetadataEntry>
        {
            new MetadataEntry(12345, "scanner1", "key1", "value1")
        };
        var scannerInfo = new Dictionary<string, string>
        {
            ["scanner1"] = "Scanner One"
        };

        // Act
        var file = new MetadataFile(sourceFilePath, scanTimestamp, entries, scannerInfo);

        // Assert
        file.SourceFilePath.Should().Be(sourceFilePath);
        file.ScanTimestamp.Should().Be(scanTimestamp);
        file.Entries.Should().HaveCount(1);
        file.ScannerInfo.Should().ContainKey("scanner1");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenSourceFilePathIsNull()
    {
        // Arrange
        var entries = new List<MetadataEntry>();
        var scannerInfo = new Dictionary<string, string>();

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            new MetadataFile(null!, DateTime.UtcNow, entries, scannerInfo));
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenEntriesIsNull()
    {
        // Arrange
        var scannerInfo = new Dictionary<string, string>();

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            new MetadataFile("path", DateTime.UtcNow, null!, scannerInfo));
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenScannerInfoIsNull()
    {
        // Arrange
        var entries = new List<MetadataEntry>();

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            new MetadataFile("path", DateTime.UtcNow, entries, null!));
    }
}
