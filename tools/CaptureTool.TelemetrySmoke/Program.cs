using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Infrastructure.Telemetry;

const int MissingExporterExitCode = 2;
const string SmokeRunIdAttribute = "telemetry.smoke_run_id";
const string SmokeMetricName = "telemetry.smoke.duration_ms";

string runId = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? args[0]
    : Guid.NewGuid().ToString("N");

using var runtime = new TelemetryRuntime();
if (!runtime.HasAzureMonitorExporter)
{
    Console.Error.WriteLine(
        "No Azure Monitor exporter is configured. Set CAPTURETOOL_APPLICATIONINSIGHTS_CONNECTION_STRING or APPLICATIONINSIGHTS_CONNECTION_STRING.");
    return MissingExporterExitCode;
}

Dictionary<string, object?> commonAttributes = new(StringComparer.Ordinal)
{
    [SmokeRunIdAttribute] = runId,
    [TelemetryAttributes.AppName] = "CaptureTool",
    [TelemetryAttributes.AppBuildChannel] = "telemetry-smoke",
    [TelemetryAttributes.CurrentRoute] = "TelemetrySmoke",
    [TelemetryAttributes.InstallIdHash] = "telemetry-smoke",
    [TelemetryAttributes.SchemaVersion] = "1",
    [TelemetryAttributes.SessionId] = $"telemetry-smoke-{runId}"
};

runtime.TrackEvent(
    TelemetryEvents.UiCommandInvoked,
    WithCommon(
        commonAttributes,
        (TelemetryAttributes.CommandId, "TelemetrySmoke"),
        (TelemetryAttributes.Outcome, "started"),
        (TelemetryAttributes.Surface, "telemetry_smoke")));

using (runtime.StartActivity(
    "telemetry.smoke.activity",
    WithCommon(
        commonAttributes,
        (TelemetryAttributes.Component, "telemetry_smoke"),
        (TelemetryAttributes.Outcome, "started"))))
{
    runtime.TrackMetric(
        SmokeMetricName,
        123.45,
        WithCommon(
            commonAttributes,
            (TelemetryAttributes.Component, "telemetry_smoke"),
            (TelemetryAttributes.Outcome, "recorded")));

    try
    {
        ThrowExpectedSmokeException();
    }
    catch (Exception exception)
    {
        runtime.TrackException(
            exception,
            WithCommon(
                commonAttributes,
                (TelemetryAttributes.Component, "telemetry_smoke"),
                (TelemetryAttributes.Fatal, false),
                (TelemetryAttributes.Outcome, "expected_exception")));
    }
}

runtime.TrackEvent(
    TelemetryEvents.WorkflowCompleted,
    WithCommon(
        commonAttributes,
        (TelemetryAttributes.Outcome, "completed"),
        (TelemetryAttributes.UseCaseId, "TelemetrySmoke")));

Console.WriteLine($"Telemetry smoke run emitted. Run ID: {runId}");
Console.WriteLine("Wait a few minutes for ingestion, then query Application Insights with:");
Console.WriteLine($"let smokeRunId = \"{runId}\";");
Console.WriteLine("union isfuzzy=true customEvents, customMetrics, dependencies, exceptions, requests, traces");
Console.WriteLine("| where timestamp > ago(30m)");
Console.WriteLine("| where tostring(customDimensions[\"telemetry.smoke_run_id\"]) == smokeRunId");
Console.WriteLine("| project timestamp, itemType, name, type, value, message, customDimensions");
Console.WriteLine("| order by timestamp asc");

return 0;

static Dictionary<string, object?> WithCommon(
    IReadOnlyDictionary<string, object?> commonAttributes,
    params (string Key, object? Value)[] attributes)
{
    Dictionary<string, object?> result = new(commonAttributes, StringComparer.Ordinal);
    foreach ((string key, object? value) in attributes)
    {
        result[key] = value;
    }

    return result;
}

static void ThrowExpectedSmokeException()
{
    throw new InvalidOperationException("Expected telemetry smoke exception. No user data.");
}
