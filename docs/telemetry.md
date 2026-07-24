# Product telemetry

## Goal

Product telemetry should answer which parts of Capture Tool are used, whether important workflows succeed, and where users abandon them. It is separate from `ILogService`: logging contains local diagnostic detail for troubleshooting, while telemetry contains structured, privacy-safe product signals suitable for aggregation.

"Who" means a resettable pseudonymous installation identifier and a short-lived session identifier. It does not mean a person's name, email address, Microsoft account, machine name, or other direct identity. Identity and common app/device context should eventually be added by the telemetry provider, not repeated by every call site.

## Recommended signals

1. **App and session lifecycle**
   - App started, activated, backgrounded, and exited.
   - Activation source, app version, release channel, OS version, architecture, locale, and session duration.
   - Derive active days, retention, and sessions per installation in the analytics system instead of emitting them as events.

2. **Capture funnel**
   - Capture requested, started, completed, canceled, and failed.
   - Media type (image, video, or audio), capture type (area, monitor, or all screens), capture mode, and privacy-safe audio configuration flags.
   - Duration and output-size buckets where useful; never capture screen coordinates or content.

3. **Editing feature adoption**
   - Editor opened and tool invoked/applied.
   - Crop, shapes, text, color picker, rotate, flip, trim, chroma key, undo/redo, super resolution, text extraction, image description, foreground extraction, object erase, and object extraction.
   - Record tool name, media type, outcome, and coarse duration; never record the edited content, prompts, recognized text, or generated descriptions.

4. **Output and sharing**
   - Save, auto-save, copy, auto-copy, print, share, and external-editor launch.
   - Media type, format, entry point, and outcome.
   - Never record file names, paths, clipboard contents, share payloads, or destination account details.

5. **Navigation and discovery**
   - Meaningful page views and feature entry points, such as home, settings, add-ons, diagnostics, and each editor.
   - Avoid tracking every hover, focus change, slider movement, or pointer event.

6. **Settings**
   - Allow-listed setting changes such as theme, language, auto-save, auto-copy, audio defaults, warning preferences, and review reminders.
   - Record only the setting key and a safe categorical value. Do not record folder paths.

7. **Store and conversion**
   - Add-ons viewed, purchase started, and purchase outcome.
   - Stable product code and normalized status; no payment or account information.

8. **Feedback and diagnostics**
   - Feedback Hub opened, logging toggled, logs cleared, and logs exported.
   - Record the action and outcome only, never the exported log contents.

9. **Reliability and performance**
   - Outcome and coarse duration for the critical workflows above.
   - Use stable error categories or reason codes. Detailed exceptions, stack traces, and free-form messages remain in local diagnostic logging unless a separate crash-reporting policy is approved.

## Common context for a future provider

Every event should be enriched centrally with a random installation ID, a rotating session ID, app version, release channel, OS version, architecture, and telemetry schema version. Installation identity must be resettable and must not be derived from hardware, account, or network identifiers.

## Data that must not be collected

- Screenshot, video, audio, or clipboard content.
- OCR text, image descriptions, text annotations, prompts, or file contents.
- File/folder names or paths, URLs, window titles, or screen coordinates.
- Audio device names, machine names, IP addresses, account IDs, names, or email addresses.
- Raw exception messages, stack traces, or arbitrary free-form properties in product events.
- High-cardinality values that have not been explicitly reviewed.

## Event contract and current implementation

`ITelemetryService.TrackEvent` accepts a stable event name plus structured, allow-listed properties. Instrumented events now include:

- `app.started`, `app.activated`, and `app.shutdown_requested`
- `navigation.completed`
- `capture.requested`, `capture.started`, `capture.completed`, `capture.canceled`, and `capture.failed`
- `editor.opened` and `edit.tool_invoked`
- `output.completed`
- `settings.changed`
- `store.opened`, `store.purchase_started`, and `store.purchase_completed`
- `feedback.opened` and `diagnostics.action`
- `ui.command_invoked`, `ui.command_completed`, `user.action`, and `use_case.completed`

Outcomes use `succeeded`, `canceled`, or `failed`. Capture events include only media/capture categories and audio-enabled flags. Editor and output events contain stable tool/operation names. Navigation records route and parameter type names, never parameter values. Settings events are protected by an explicit allow-list; folder settings are excluded and language values are reduced to `system_default` or `override`. Store product identifiers and activation sources are normalized to a small known vocabulary.

The use-case executor publishes `use_case.completed` for operational reliability, while command adapters publish user/UI action signals. Both keep detailed lifecycle/error information in `ILogService` and publish only structured outcomes through telemetry. Semantic events are emitted at workflow boundaries so a future analytics system can build funnels without depending on UI controls.

`NullTelemetryService` is registered today. It discards every event, creates no identity, persists nothing, and sends nothing off-device. Replacing it with a provider later should not require product code to depend on a vendor SDK.

## Current-state assessment

Before this cleanup, `TelemetryService` formatted timestamps, activity messages, caller information, and exceptions into `ILogService`. It was local diagnostic logging under a telemetry name:

- No events left the device.
- Logging was disabled by default and retained only a short in-memory window.
- There was no stable structured schema, provider, installation/session identity, consent, sampling, or retention policy.
- `ButtonInvoked` had no production call sites.
- Command instrumentation was optional and was not wired by production view models.
- Exception messages and call-site details were appropriate for local logs but unsafe as default product-analytics properties.

The logging and telemetry paths are now separate. Before registering any transmitting provider, choose the destination, document consent and retention, add a reset/opt-out control, and verify the event allow-list against the privacy disclosure.
