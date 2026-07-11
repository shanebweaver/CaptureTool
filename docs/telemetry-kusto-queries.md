# CaptureTool Telemetry Kusto Queries

Starter Application Insights queries for the OpenTelemetry event schema in `docs/prd-opentelemetry-azure-monitor.md`.

Set `timeRange` at the top of each query while investigating.

## Top Commands By Surface

```kusto
let timeRange = 7d;
customEvents
| where timestamp > ago(timeRange)
| where name == "ui.command.invoked"
| extend surface = tostring(customDimensions["surface"])
| extend command_id = tostring(customDimensions["command.id"])
| summarize count() by surface, command_id
| order by count_ desc
```

## Navigation Flow

```kusto
let timeRange = 7d;
customEvents
| where timestamp > ago(timeRange)
| where name == "navigation.completed"
| extend from_route = tostring(customDimensions["from_route"])
| extend to_route = tostring(customDimensions["to_route"])
| summarize transitions = count() by from_route, to_route
| order by transitions desc
```

## Capture Outcomes

```kusto
let timeRange = 7d;
customEvents
| where timestamp > ago(timeRange)
| where name in ("capture.started", "capture.completed", "capture.failed", "capture.cancelled")
| extend media_type = tostring(customDimensions["media.type"])
| extend capture_mode = tostring(customDimensions["capture.mode"])
| extend capture_type = tostring(customDimensions["capture.type"])
| summarize count() by name, media_type, capture_mode, capture_type
| order by media_type asc, count_ desc
```

## Edit Export Share Adoption

```kusto
let timeRange = 7d;
customEvents
| where timestamp > ago(timeRange)
| where name in ("edit.command.invoked", "file.saved", "share.invoked")
| extend surface = tostring(customDimensions["surface"])
| extend media_type = tostring(customDimensions["media.type"])
| extend command_id = tostring(customDimensions["command.id"])
| summarize count() by name, surface, media_type, command_id
| order by count_ desc
```

## Workflow Duration

```kusto
let timeRange = 7d;
customMetrics
| where timestamp > ago(timeRange)
| where name == "use_case.duration_ms"
| extend use_case_id = tostring(customDimensions["use_case.id"])
| summarize
    p50_ms = percentile(value, 50),
    p95_ms = percentile(value, 95),
    count()
    by use_case_id
| order by p95_ms desc
```

## Exceptions By Version And Component

```kusto
let timeRange = 7d;
exceptions
| where timestamp > ago(timeRange)
| extend app_version = tostring(customDimensions["app.version"])
| extend component = tostring(customDimensions["component"])
| extend use_case_id = tostring(customDimensions["use_case.id"])
| summarize count() by app_version, component, use_case_id, type
| order by count_ desc
```

## PII Guardrail Spot Check

```kusto
let timeRange = 7d;
union isfuzzy=true customEvents, customMetrics, dependencies, exceptions, requests, traces
| where timestamp > ago(timeRange)
| extend dimensions = tostring(customDimensions)
| where dimensions has_any ("C:\\", "\\Users\\", "/Users/", "/home/", "file.path", "folder.path", "window.title", "clipboard", "protocol.uri", "http://", "https://")
| project timestamp, itemType, name, type, dimensions
| order by timestamp desc
```

## Smoke Run Verification

Run `tools\CaptureTool.TelemetrySmoke` first and use the printed run ID.

```kusto
let smokeRunId = "replace-with-smoke-run-id";
union isfuzzy=true customEvents, customMetrics, dependencies, exceptions, requests, traces
| where timestamp > ago(30m)
| where tostring(customDimensions["telemetry.smoke_run_id"]) == smokeRunId
| project timestamp, itemType, name, type, value, message, customDimensions
| order by timestamp asc
```
