using CaptureTool.Application.Abstractions.Telemetry;

namespace CaptureTool.Infrastructure.Tests.Telemetry;

[TestClass]
public sealed class TelemetryConsentSettingValuesTests
{
    [TestMethod]
    [DataRow(TelemetryConsentSettingValues.Unknown, TelemetryConsentState.Unknown)]
    [DataRow(TelemetryConsentSettingValues.Denied, TelemetryConsentState.Denied)]
    [DataRow(TelemetryConsentSettingValues.Granted, TelemetryConsentState.Granted)]
    [DataRow("unexpected", TelemetryConsentState.Unknown)]
    public void Parse_ShouldReturnExpectedState(string settingValue, TelemetryConsentState expected)
    {
        Assert.AreEqual(expected, TelemetryConsentSettingValues.Parse(settingValue));
    }

    [TestMethod]
    [DataRow(TelemetryConsentState.Unknown, TelemetryConsentSettingValues.Unknown)]
    [DataRow(TelemetryConsentState.Denied, TelemetryConsentSettingValues.Denied)]
    [DataRow(TelemetryConsentState.Granted, TelemetryConsentSettingValues.Granted)]
    public void Serialize_ShouldReturnExpectedValue(TelemetryConsentState state, string expected)
    {
        Assert.AreEqual(expected, TelemetryConsentSettingValues.Serialize(state));
    }
}
