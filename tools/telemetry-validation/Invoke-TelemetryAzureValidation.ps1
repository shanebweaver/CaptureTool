#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$ConnectionString = $env:CAPTURETOOL_APPLICATIONINSIGHTS_CONNECTION_STRING,

    [string]$AppId = $env:APPLICATIONINSIGHTS_APP_ID,

    [string]$ApiKey = $env:APPLICATIONINSIGHTS_API_KEY,

    [ValidatePattern('^[A-Za-z0-9_.-]+$')]
    [string]$RunId = "smoke-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())",

    [int]$TimeoutSeconds = 300,

    [int]$PollIntervalSeconds = 15,

    [string]$QueryEndpoint = "https://api.applicationinsights.io/v1/apps"
)

$ErrorActionPreference = "Stop"

$exitMissingConnectionString = 2
$exitMissingQueryCredentials = 3
$exitSmokeFailed = 4
$exitIngestionTimeout = 5
$exitGuardrailFailed = 6

function Invoke-AppInsightsScalarQuery {
    param(
        [Parameter(Mandatory)]
        [string]$Query
    )

    $uri = "$QueryEndpoint/$AppId/query"
    $headers = @{
        "x-api-key" = $ApiKey
    }
    $body = @{
        query = $Query
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -ContentType "application/json" -Body $body
    if ($null -eq $response.tables -or $response.tables.Count -eq 0 -or $response.tables[0].rows.Count -eq 0) {
        return 0
    }

    return [int]$response.tables[0].rows[0][0]
}

function New-SmokeQuery {
    param(
        [Parameter(Mandatory)]
        [string]$TableExpression,

        [string]$WhereClause = ""
    )

    $query = @"
$TableExpression
| where timestamp > ago(60m)
| where tostring(customDimensions["telemetry.smoke_run_id"]) == "$RunId"
"@

    if (-not [string]::IsNullOrWhiteSpace($WhereClause)) {
        $query += "`n$WhereClause"
    }

    return "$query`n| count"
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    [Console]::Error.WriteLine("Set CAPTURETOOL_APPLICATIONINSIGHTS_CONNECTION_STRING or pass -ConnectionString.")
    exit $exitMissingConnectionString
}

if ([string]::IsNullOrWhiteSpace($AppId) -or [string]::IsNullOrWhiteSpace($ApiKey)) {
    [Console]::Error.WriteLine("Set APPLICATIONINSIGHTS_APP_ID and APPLICATIONINSIGHTS_API_KEY, or pass -AppId and -ApiKey.")
    exit $exitMissingQueryCredentials
}

$oldConnectionString = $env:CAPTURETOOL_APPLICATIONINSIGHTS_CONNECTION_STRING
try {
    $env:CAPTURETOOL_APPLICATIONINSIGHTS_CONNECTION_STRING = $ConnectionString
    dotnet run --project tools\CaptureTool.TelemetrySmoke\CaptureTool.TelemetrySmoke.csproj --configuration Release -- $RunId
    if ($LASTEXITCODE -ne 0) {
        exit $exitSmokeFailed
    }
}
finally {
    if ($null -eq $oldConnectionString) {
        Remove-Item Env:CAPTURETOOL_APPLICATIONINSIGHTS_CONNECTION_STRING -ErrorAction SilentlyContinue
    }
    else {
        $env:CAPTURETOOL_APPLICATIONINSIGHTS_CONNECTION_STRING = $oldConnectionString
    }
}

$queries = @{
    customEvents = New-SmokeQuery `
        -TableExpression "customEvents" `
        -WhereClause '| where name in ("ui.command.invoked", "workflow.completed")'
    customMetrics = New-SmokeQuery `
        -TableExpression "customMetrics" `
        -WhereClause '| where name == "telemetry.smoke.duration_ms"'
    spans = New-SmokeQuery `
        -TableExpression "union isfuzzy=true dependencies, requests, traces"
    exceptions = New-SmokeQuery `
        -TableExpression "exceptions" `
        -WhereClause '| where tostring(customDimensions["exception.type"]) == "InvalidOperationException"'
}

$deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
$counts = @{}
do {
    foreach ($name in $queries.Keys) {
        $counts[$name] = Invoke-AppInsightsScalarQuery -Query $queries[$name]
    }

    Write-Host "Smoke run $RunId counts: customEvents=$($counts.customEvents), customMetrics=$($counts.customMetrics), spans=$($counts.spans), exceptions=$($counts.exceptions)"

    if ($counts.customEvents -ge 2 -and $counts.customMetrics -ge 1 -and $counts.spans -ge 1 -and $counts.exceptions -ge 1) {
        break
    }

    if ([DateTimeOffset]::UtcNow -lt $deadline) {
        Start-Sleep -Seconds $PollIntervalSeconds
    }
}
while ([DateTimeOffset]::UtcNow -lt $deadline)

if (-not ($counts.customEvents -ge 2 -and $counts.customMetrics -ge 1 -and $counts.spans -ge 1 -and $counts.exceptions -ge 1)) {
    [Console]::Error.WriteLine("Timed out waiting for all telemetry smoke signals in Application Insights.")
    exit $exitIngestionTimeout
}

$guardrailQuery = @"
union isfuzzy=true customEvents, customMetrics, dependencies, exceptions, requests, traces
| where timestamp > ago(60m)
| where tostring(customDimensions["telemetry.smoke_run_id"]) == "$RunId"
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
) or tostring(message) has_any (
    "C:\\",
    "\\Users\\",
    "/Users/",
    "/home/",
    "Expected telemetry smoke exception. No user data."
)
| count
"@

$guardrailCount = Invoke-AppInsightsScalarQuery -Query $guardrailQuery
if ($guardrailCount -ne 0) {
    [Console]::Error.WriteLine("Telemetry privacy guardrail failed with $guardrailCount prohibited row(s).")
    exit $exitGuardrailFailed
}

Write-Host "Telemetry Azure validation passed for run ID $RunId."
exit 0
