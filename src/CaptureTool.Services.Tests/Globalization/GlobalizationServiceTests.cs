using System.Globalization;
using CaptureTool.Services.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CaptureTool.Services.Tests.Globalization;

[TestClass]
public class GlobalizationServiceTests
{
    private CultureInfo? _originalCulture;

    [TestInitialize]
    public void Setup()
    {
        _originalCulture = CultureInfo.CurrentCulture;
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_originalCulture is not null)
        {
            CultureInfo.CurrentCulture = _originalCulture;
        }
    }

    [TestMethod]
    public void IsRightToLeft_ReturnsFalse_ForLeftToRightCulture()
    {
        // Arrange
        CultureInfo.CurrentCulture = new CultureInfo("en-US"); // LTR
        var service = new GlobalizationService();

        // Act
        var result = service.IsRightToLeft;

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsRightToLeft_ReturnsTrue_ForRightToLeftCulture()
    {
        // Arrange
        CultureInfo.CurrentCulture = new CultureInfo("ar-SA"); // RTL
        var service = new GlobalizationService();

        // Act
        var result = service.IsRightToLeft;

        // Assert
        Assert.IsTrue(result);
    }
}
