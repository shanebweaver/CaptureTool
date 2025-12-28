using CaptureTool.Domains.Capture.Interfaces.Metadata;
using FluentAssertions;

namespace CaptureTool.Core.Tests.Metadata;

[TestClass]
public class MetadataEntryTests
{
    [TestMethod]
    public void Constructor_ShouldSetProperties()
    {
        // Arrange
        long timestamp = 12345678;
        string scannerId = "test-scanner";
        string key = "test-key";
        object value = "test-value";
        var additionalData = new Dictionary<string, object?> { ["extra"] = "data" };

        // Act
        var entry = new MetadataEntry(timestamp, scannerId, key, value, additionalData);

        // Assert
        entry.Timestamp.Should().Be(timestamp);
        entry.ScannerId.Should().Be(scannerId);
        entry.Key.Should().Be(key);
        entry.Value.Should().Be(value);
        entry.AdditionalData.Should().NotBeNull();
        entry.AdditionalData!["extra"].Should().Be("data");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenScannerIdIsNull()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            new MetadataEntry(12345, null!, "key", "value"));
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenKeyIsNull()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            new MetadataEntry(12345, "scanner", null!, "value"));
    }

    [TestMethod]
    public void Constructor_ShouldAllowNullValue()
    {
        // Act
        var entry = new MetadataEntry(12345, "scanner", "key", null);

        // Assert
        entry.Value.Should().BeNull();
    }

    [TestMethod]
    public void Constructor_ShouldAllowNullAdditionalData()
    {
        // Act
        var entry = new MetadataEntry(12345, "scanner", "key", "value", null);

        // Assert
        entry.AdditionalData.Should().BeNull();
    }
}
