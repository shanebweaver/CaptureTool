# Product telemetry

## Goal

Product telemetry should answer which parts of Capture Tool are used, whether important workflows succeed, and where users abandon them. It is separate from `ILogService`: logging contains local diagnostic detail for troubleshooting, while telemetry contains structured, privacy-safe product signals suitable for aggregation.

The telemetry is designed for anonymous aggregate counts, not individual usage histories. Events must not contain a user, account, device, installation, or session identifier, including a pseudonymous one.

## Recommended signals

1. **App and session lifecycle**
   - App started, activated, backgrounded, and exited.
   - Activation source, app version, release channel, OS version, architecture, locale, and session duration.
   - Aggregate launches and activations without attempting to calculate per-user retention or sessions per installation.

2. **Capture funnel**
   - Capture requested, started, completed, canceled, and failed.
   - Media type (image, video, or audio), capture type (rectangle, monitor, window, all screens, or audio only), capture mode, and privacy-safe audio configuration flags when applicable.
   - For startup failures, a stable failure stage and allow-listed reason category; never the exception message or raw HRESULT.
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

## Provider context

Capture Tool does not attach user, account, device, installation, session, hardware, or network identifiers. The Microsoft Store Services SDK receives only a short custom-event name. Partner Center supplies aggregate breakdowns by market, device type, and package version; Capture Tool does not populate those dimensions itself.

## Data Capture Tool must not include in events

- Screenshot, video, audio, or clipboard content.
- OCR text, image descriptions, text annotations, prompts, or file contents.
- File/folder names or paths, URLs, window titles, or screen coordinates.
- Audio device names, machine names, network identifiers, account IDs, names, or email addresses.
- User, device, installation, advertising, or session identifiers, including generated pseudonymous identifiers.
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

Outcomes use `succeeded`, `canceled`, or `failed`. Capture funnel events include media type, capture type, and, when applicable, desktop-audio/audio-input enabled flags. A video failure raised while starting the recorder includes a bounded failure stage and reason. Synchronous initialization failures use `failure_stage=recorder_start`; a session that does not produce its first frame before the startup deadline uses `failure_stage=first_frame` and `failure_reason=start_timeout`. Other bounded reasons are `access_denied`, `component_unavailable`, `configuration_unsupported`, `graphics_unsupported`, `initialization_failed`, `invalid_configuration`, `output_unavailable`, `platform_unsupported`, `resource_exhausted`, and `target_unavailable`. The classifier uses exception types and a small mapping of known HRESULTs, but never records an HRESULT, exception message, path, or device identifier. Editor and output events contain stable tool/operation names. Navigation records route and parameter type names, never parameter values. Settings events are protected by an explicit allow-list; folder settings are excluded and language values are reduced to `system_default` or `override`. Store product identifiers and activation sources are normalized to a small known vocabulary.

The use-case executor publishes `use_case.completed` for operational reliability, while command adapters publish user/UI action signals. Both keep detailed lifecycle/error information in `ILogService` and publish only structured outcomes through telemetry. Semantic events are emitted at workflow boundaries so aggregate reports can describe funnels without depending on UI controls.

`ConsentAwareTelemetryService` is the application-facing service. It discards every event unless the user has explicitly opted in. The first-run consent dialog and the Privacy setting persist only `unknown`, `granted`, or `denied`; declining or restoring defaults closes the gate immediately.

`NullTelemetryService` remains the default event sink for platform-neutral hosts. The Windows app replaces it with `StoreServicesTelemetryEventSink`, which uses `StoreServicesCustomEventLogger` from Microsoft Store Services. This replacement is still downstream of `ConsentAwareTelemetryService`, so the Store SDK is not called until the user opts in.

The Store API accepts one string and no property bag. `PartnerCenterTelemetryEventNameFormatter` converts each event and a small, event-specific set of allow-listed dimensions into a brief `ct1_...` name. Examples include:

- `ct1_app_started`
- `ct1_capture_completed_video_rectangle_true_false_succeeded`
- `ct1_capture_failed_video_rectangle_true_false_failed_recorder_start_target_unavailable`
- `ct1_capture_failed_video_window_false_false_failed_first_frame_start_timeout`
- `ct1_ui_command_invoked_image_editor_open_selection_overlay`

Unexpected properties are ignored. Failure stages and reasons are protected by explicit value allow-lists, so an unreviewed value cannot be encoded in the Store event name. Names are normalized to lowercase ASCII and capped at 96 characters with a deterministic suffix when necessary. No identifier or user content is encoded in the event name.

## Verifying events in Partner Center

Microsoft requires the app to be published in the Store before its custom events appear. Install and exercise the published package, opt into optional usage data in Capture Tool, then open the app's **Usage** report in Partner Center and inspect **Custom events**. The Windows Usage report includes only customers who have not opted out of Windows telemetry, and its custom-event breakdown is aggregate by market, device type, and package version.

## Current-state assessment

Before this cleanup, `TelemetryService` formatted timestamps, activity messages, caller information, and exceptions into `ILogService`. It was local diagnostic logging under a telemetry name:

- No events left the device.
- Logging was disabled by default and retained only a short in-memory window.
- There was no stable structured schema, provider, consent, sampling, or retention policy.
- `ButtonInvoked` had no production call sites.
- Command instrumentation was optional and was not wired by production view models.
- Exception messages and call-site details were appropriate for local logs but unsafe as default product-analytics properties.

The logging and telemetry paths are separate, and the opt-in dialog plus Settings control protect the Store sink. Before considering the trial production-ready, verify the events in Partner Center, review Microsoft's retention and transport behavior, and confirm the event-name allow-list against the privacy disclosure.
