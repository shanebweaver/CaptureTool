using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Infrastructure.Telemetry;

namespace CaptureTool.Infrastructure.Tests.Telemetry;

[TestClass]
public sealed class TelemetrySanitizerTests
{
    [TestMethod]
    public void SanitizeEventName_WhenKnown_ReturnsEventName()
    {
        var sanitizer = new TelemetrySanitizer();

        string eventName = sanitizer.SanitizeEventName(TelemetryEvents.UiCommandInvoked);

        Assert.AreEqual(TelemetryEvents.UiCommandInvoked, eventName);
    }

    [TestMethod]
    public void SanitizeEventName_WhenUnknown_ReturnsUnknownEvent()
    {
        var sanitizer = new TelemetrySanitizer();

        string eventName = sanitizer.SanitizeEventName("dynamic.event");

        Assert.AreEqual(TelemetryEvents.Unknown, eventName);
    }

    [TestMethod]
    public void SanitizeAttributes_DropsProhibitedKeysAndRedactsPathValues()
    {
        var sanitizer = new TelemetrySanitizer();
        Dictionary<string, object?> attributes = new()
        {
            ["command.id"] = "SaveImageFile",
            ["file.path"] = @"C:\Users\someone\Pictures\capture.png",
            ["destination"] = @"C:\Users\someone\Pictures\capture.png",
            ["window.title"] = "Private document",
            ["count"] = 3
        };

        IReadOnlyDictionary<string, object?> sanitized = sanitizer.SanitizeAttributes(attributes);

        Assert.IsTrue(sanitized.ContainsKey("command.id"));
        Assert.IsFalse(sanitized.ContainsKey("file.path"));
        Assert.IsFalse(sanitized.ContainsKey("window.title"));
        Assert.AreEqual("[redacted]", sanitized["destination"]);
        Assert.AreEqual(3, sanitized["count"]);
    }

    [TestMethod]
    public void SanitizeAttributes_TruncatesLongStrings()
    {
        var sanitizer = new TelemetrySanitizer();
        string value = new('a', 300);

        IReadOnlyDictionary<string, object?> sanitized = sanitizer.SanitizeAttributes(
            new Dictionary<string, object?> { ["message"] = value });

        Assert.AreEqual(256, ((string)sanitized["message"]!).Length);
    }

    [TestMethod]
    public void SanitizeAttributes_RedactsAbsoluteUrls()
    {
        var sanitizer = new TelemetrySanitizer();

        IReadOnlyDictionary<string, object?> sanitized = sanitizer.SanitizeAttributes(
            new Dictionary<string, object?>
            {
                ["source"] = "https://example.test/capture?file=secret.png"
            });

        Assert.AreEqual("[redacted]", sanitized["source"]);
    }
}
