# Telemetry Azure Validation

Use this checklist to complete the final PRD validation gate against a development Application Insights resource.

## Smoke Emitter

Set a development-only connection string, then run:

```powershell
$env:CAPTURETOOL_APPLICATIONINSIGHTS_CONNECTION_STRING = '<development Application Insights connection string>'
dotnet run --project tools\CaptureTool.TelemetrySmoke\CaptureTool.TelemetrySmoke.csproj --configuration Release -- smoke-001
```

The tool emits:

- `ui.command.invoked` custom event.
- `workflow.completed` custom event.
- `telemetry.smoke.activity` span.
- `telemetry.smoke.duration_ms` custom metric.
- One expected sanitized exception with `exception.type=InvalidOperationException`.

If no exporter is configured, the tool exits with code `2` and does not emit telemetry.

## Automated Validation

If you have an Application Insights application ID and API key with query/read access, run the full smoke-and-query validation script:

```powershell
$env:CAPTURETOOL_APPLICATIONINSIGHTS_CONNECTION_STRING = '<development Application Insights connection string>'
$env:APPLICATIONINSIGHTS_APP_ID = '<Application Insights app ID>'
$env:APPLICATIONINSIGHTS_API_KEY = '<Application Insights query API key>'
.\tools\telemetry-validation\Invoke-TelemetryAzureValidation.ps1 -RunId smoke-001
```

The script runs the smoke emitter, polls Application Insights until all signal types arrive, and fails if the privacy guardrail query finds prohibited payloads. A passing script run is sufficient evidence to check the PRD item `Verify Application Insights events, traces, metrics, and exceptions`.

## Trace Sampling

Trace sampling defaults to `1.0`, meaning all spans are sampled. Override it only for development or release validation by setting:

```powershell
$env:CAPTURETOOL_TELEMETRY_TRACE_SAMPLING_RATIO = '0.25'
```

Accepted values are inclusive decimal ratios from `0.0` through `1.0`. Invalid values fall back to `1.0`. Custom events and metrics are not sampled by this runtime knob.

## Ingestion Query

Use the run ID printed by the tool, or the `-RunId` passed to the validation script:

```kusto
let smokeRunId = "smoke-001";
union isfuzzy=true customEvents, customMetrics, dependencies, exceptions, requests, traces
| where timestamp > ago(30m)
| where tostring(customDimensions["telemetry.smoke_run_id"]) == smokeRunId
| project timestamp, itemType, name, type, value, message, customDimensions
| order by timestamp asc
```

Pass criteria:

- At least two rows appear in `customEvents`.
- At least one row appears in `customMetrics`.
- At least one span row appears in `dependencies`, `requests`, or `traces`.
- At least one exception row appears with `customDimensions["exception.type"] == "InvalidOperationException"`.

## Privacy Guardrail Query

Run this against the same smoke run and against a short interactive app session:

```kusto
let smokeRunId = "smoke-001";
union isfuzzy=true customEvents, customMetrics, dependencies, exceptions, requests, traces
| where timestamp > ago(30m)
| where tostring(customDimensions["telemetry.smoke_run_id"]) == smokeRunId
| extend dimensions = tostring(customDimensions)
| where dimensions has_any (
    "C:\\",
    "\\Users\\",
    "/Users/",
    "/home/",
    "file.path",
    "folder.path",
    "window.title",
    "clipboard",
    "protocol.uri",
    "http://",
    "https://",
    "Expected telemetry smoke exception. No user data."
)
| project timestamp, itemType, name, type, message, dimensions
```

Pass criteria: zero rows.

## PRD Checkbox Rule

Only check `Verify Application Insights events, traces, metrics, and exceptions` in the PRD after the ingestion query proves all signal types arrived in Application Insights and the guardrail query returns zero rows.
