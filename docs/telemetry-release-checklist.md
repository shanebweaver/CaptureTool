# Telemetry Release Checklist

This checklist must be completed before CaptureTool enables remote telemetry in any public production build.

## Product Decisions

- Telemetry is opt-in. The default setting is disabled.
- Direct client-to-Azure Monitor Application Insights ingestion is acceptable for the MVP.
- A telemetry proxy or collector remains a follow-up option if ingestion abuse, privacy review, or cost controls require it.
- Crash/error telemetry follows the same opt-in setting as usage telemetry until a separate privacy decision is documented.

## Privacy Gates

- [x] Update the in-app privacy policy text in every localized resource file.
- [ ] Update any Store listing privacy text or linked privacy policy. Draft copy is in `docs\telemetry-store-privacy-disclosure.md`.
- [x] Verify telemetry setting copy clearly says optional diagnostic and usage data.
- [ ] Verify the app does not send file paths, file names, folder paths, screenshot/audio/video content, clipboard content, window titles, audio device names, annotation text, raw exception messages, or raw protocol URIs.
- [x] Verify users can disable telemetry after enabling it.
- [x] Verify users can reset the anonymous install identifier.

## Azure Gates

- [ ] Use separate Application Insights resources or connection strings for development, preview, and production.
- [ ] Configure ingestion caps and cost alerts.
- [ ] Configure retention policy.
- [ ] Verify sampling settings.
- [x] Verify offline storage location and retention, or explicitly disable offline storage.
- [ ] Verify dashboards/workbooks exist for usage, funnels, failures, and error rates.

## Engineering Gates

- [x] Run the full managed test suite.
- [x] Validate Native AOT publish.
- [ ] Verify Application Insights receives events, traces, metrics, and exceptions from a development build using `tools\telemetry-validation\Invoke-TelemetryAzureValidation.ps1`.
- [ ] Verify no prohibited attributes appear in Application Insights search/logs using `tools\telemetry-validation\Invoke-TelemetryAzureValidation.ps1` or the guardrail query in `docs/telemetry-azure-validation.md`.
- [x] Verify provider disposal flushes telemetry on normal shutdown as much as the platform allows.
