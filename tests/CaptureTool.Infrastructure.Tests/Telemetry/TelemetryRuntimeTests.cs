using CaptureTool.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CaptureTool.Infrastructure.Tests.Telemetry;

[TestClass]
[DoNotParallelize]
public sealed class TelemetryRuntimeTests
{
    [TestMethod]
    public void Constructor_WhenNoConnectionString_CreatesLocalRuntimeWithoutAzureExporter()
    {
        string? customConnectionString = Environment.GetEnvironmentVariable("CAPTURETOOL_APPLICATIONINSIGHTS_CONNECTION_STRING");
        string? standardConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        string? samplingRatio = Environment.GetEnvironmentVariable(TelemetryRuntime.TraceSamplingRatioEnvironmentVariable);
        Environment.SetEnvironmentVariable("CAPTURETOOL_APPLICATIONINSIGHTS_CONNECTION_STRING", null);
        Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING", null);
        Environment.SetEnvironmentVariable(TelemetryRuntime.TraceSamplingRatioEnvironmentVariable, null);
        try
        {
            using var runtime = new TelemetryRuntime();

            Assert.IsNotNull(runtime.ActivitySource);
            Assert.IsNotNull(runtime.Meter);
            Assert.IsFalse(runtime.HasAzureMonitorExporter);
            Assert.AreEqual(1.0, runtime.TraceSamplingRatio);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CAPTURETOOL_APPLICATIONINSIGHTS_CONNECTION_STRING", customConnectionString);
            Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING", standardConnectionString);
            Environment.SetEnvironmentVariable(TelemetryRuntime.TraceSamplingRatioEnvironmentVariable, samplingRatio);
        }
    }

    [TestMethod]
    public void Constructor_WhenSamplingRatioIsConfigured_UsesConfiguredRatio()
    {
        string? samplingRatio = Environment.GetEnvironmentVariable(TelemetryRuntime.TraceSamplingRatioEnvironmentVariable);
        Environment.SetEnvironmentVariable(TelemetryRuntime.TraceSamplingRatioEnvironmentVariable, "0.25");
        try
        {
            using var runtime = new TelemetryRuntime();

            Assert.AreEqual(0.25, runtime.TraceSamplingRatio);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TelemetryRuntime.TraceSamplingRatioEnvironmentVariable, samplingRatio);
        }
    }

    [TestMethod]
    public void Constructor_WhenSamplingRatioIsInvalid_UsesDefaultRatio()
    {
        string? samplingRatio = Environment.GetEnvironmentVariable(TelemetryRuntime.TraceSamplingRatioEnvironmentVariable);
        Environment.SetEnvironmentVariable(TelemetryRuntime.TraceSamplingRatioEnvironmentVariable, "1.5");
        try
        {
            using var runtime = new TelemetryRuntime();

            Assert.AreEqual(1.0, runtime.TraceSamplingRatio);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TelemetryRuntime.TraceSamplingRatioEnvironmentVariable, samplingRatio);
        }
    }

    [TestMethod]
    public void CreateSanitizedExceptionForExport_DoesNotExposeOriginalMessageOrInnerException()
    {
        var original = new InvalidOperationException(
            @"Could not open C:\Users\alice\Pictures\private-capture.png",
            new IOException("inner path leak"));

        Exception sanitized = TelemetryRuntime.CreateSanitizedExceptionForExport(original);

        Assert.AreNotSame(original, sanitized);
        Assert.IsNull(sanitized.InnerException);
        Assert.AreEqual("Exception message omitted by CaptureTool telemetry privacy policy.", sanitized.Message);
        Assert.IsFalse(sanitized.ToString().Contains("private-capture.png", StringComparison.Ordinal));
        Assert.IsFalse(sanitized.ToString().Contains(@"C:\Users", StringComparison.Ordinal));
        Assert.IsFalse(sanitized.ToString().Contains("inner path leak", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Dispose_ForceFlushesProvidersBeforeDisposingThemOnce()
    {
        var tracerProvider = new RecordingProviderLifecycle();
        var meterProvider = new RecordingProviderLifecycle();
        ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        var runtime = new TelemetryRuntime(
            new ActivitySource("test"),
            new Meter("test"),
            tracerProvider,
            meterProvider,
            loggerFactory,
            loggerFactory.CreateLogger("test"),
            hasAzureMonitorExporter: true,
            traceSamplingRatio: 1.0);

        runtime.Dispose();
        runtime.Dispose();

        CollectionAssert.AreEqual(
            new[] { "ForceFlush:5000", "Dispose" },
            tracerProvider.Calls);
        CollectionAssert.AreEqual(
            new[] { "ForceFlush:5000", "Dispose" },
            meterProvider.Calls);
    }

    private sealed class RecordingProviderLifecycle : ITelemetryProviderLifecycle
    {
        public List<string> Calls { get; } = [];

        public bool ForceFlush(int timeoutMilliseconds)
        {
            Calls.Add($"ForceFlush:{timeoutMilliseconds}");
            return true;
        }

        public void Dispose()
        {
            Calls.Add("Dispose");
        }
    }
}
