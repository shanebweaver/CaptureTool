# Telemetry Store Privacy Disclosure Draft

Use this copy before enabling remote telemetry in any public Microsoft Store build. It is intentionally plain and conservative.

## Store Listing Short Disclosure

CaptureTool keeps captured screenshots, audio, video, clipboard content, annotation text, file paths, file names, folder paths, and window titles on your device. Optional diagnostic and usage telemetry is off by default. If you turn it on in Settings, CaptureTool sends privacy-preserving events, metrics, and exception details to Azure Monitor Application Insights, such as button names, navigation routes, feature outcomes, app version, anonymous session ID, and a hashed anonymous install ID. You can turn telemetry off at any time and reset the anonymous install ID.

## Linked Privacy Policy Section

### Optional Diagnostic And Usage Telemetry

CaptureTool does not collect, store, or transmit captured screenshots, captured audio, captured video, clipboard content, annotation text, file paths, file names, folder paths, window titles, audio device names, or other user-generated content.

CaptureTool includes optional diagnostic and usage telemetry to help understand reliability, feature usage, navigation flow, and error rates. This telemetry is disabled by default. If you enable it in Settings, CaptureTool may send privacy-preserving telemetry to Azure Monitor Application Insights.

Telemetry may include:

- Button or command identifiers.
- Navigation route names.
- Capture, edit, export, save, copy, share, settings, diagnostics, and store workflow outcomes.
- Duration and count metrics.
- App version, build channel, process architecture, and coarse OS version bucket.
- Anonymous session ID.
- Hashed anonymous install ID.
- Sanitized exception type and component context.

Telemetry does not include:

- Screenshot, image, audio, or video content.
- Clipboard content.
- File paths, file names, or folder paths.
- Window titles or captured application names.
- Audio device display names.
- User-entered annotation text.
- Raw protocol activation URIs.
- Raw exception messages.
- Store account or user identity information.

Telemetry delivery is best effort. You can disable telemetry at any time in Settings. You can also reset the anonymous install ID in Settings, which creates a new anonymous identifier for future telemetry from that installation.

## Store Submission Checklist

- Update the Microsoft Store listing privacy text or linked privacy policy before production telemetry is enabled.
- Confirm the Store privacy declaration matches the in-app privacy text in every supported locale.
- Confirm telemetry remains opt-in and disabled by default in the submitted build.
- Confirm screenshots, audio, video, clipboard content, local file information, window titles, annotation text, and raw exception messages are not present in Application Insights validation queries.
