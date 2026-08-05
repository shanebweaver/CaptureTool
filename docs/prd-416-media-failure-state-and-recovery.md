# PRD: Media failure state and recovery

- Issue: [#416](https://github.com/shanebweaver/CaptureTool/issues/416)
- Finding: `ARCH-11`
- Severity: Medium
- Status: Implemented
- Affected features: `CAP-05`, `VID-01`, `AUD-01`

## Summary

Video and audio editors must expose an explicit media lifecycle independent of file availability: `Loading`, `Ready`, or `Failed` with a bounded failure category. Video finalization exceptions, synchronous source initialization failures, and asynchronous `MediaPlayer.MediaFailed` events will all enter a visible failed state, record technical detail in application logs, and offer recovery appropriate to the failure.

The existing file-ready properties remain responsible for capture finalization and source availability. Editor commands and playback UI will require the media pipeline to reach `Ready`.

## Problem

`VideoEditPageViewModel.WaitForVideoFinalizationAsync` catches every exception and only clears its progress flag. The editor remains blank and non-ready without a message or recovery route. Video and audio pages similarly swallow source initialization exceptions and never subscribe to `MediaFailed`, so missing files, corrupt content, unsupported codecs, and asynchronous decoder failures are invisible.

Users cannot distinguish slow loading from permanent failure and receive no path to retry or locate the recording. Technical failures also bypass the normal application log.

## Goals

1. Model media loading, readiness, and categorized failure explicitly for video and audio editors.
2. Surface finalization, unavailable-file, unsupported-media, and playback failures without exposing paths or media content in UI or telemetry.
3. Log technical exceptions and platform error details.
4. Allow retry for source and playback failures.
5. Keep finalization failure non-retryable while preserving the screen-recordings-folder recovery action.
6. Disable media-dependent editor actions until playback reaches `Ready`.

## Non-goals

- Do not redesign the capture finalization pipeline or retry a terminal `PendingVideoFile` task.
- Do not install codecs, repair corrupt files, or infer format support before the platform attempts playback.
- Do not add file paths, media names, error messages, or content to telemetry.
- Do not change save, copy, trimming, external-editor, or super-resolution implementations.

## State model

### States

- **Loading:** a finalized source is being handed to the platform media pipeline.
- **Ready:** the platform raised `MediaOpened`; playback-dependent actions are available.
- **Failed:** finalization or playback cannot continue and a failure category/message is available.

### Failure categories

- **Finalization:** the recording did not finish writing; retry is not offered.
- **File unavailable:** source lookup or initialization failed; retry is offered.
- **Unsupported:** the platform reports an unsupported source or codec; retry is offered in case platform support changes.
- **Playback:** another asynchronous media-pipeline failure occurred; retry is offered.

## Functional requirements

### View models

- Expose media state, failure category, localized message, loading/ready/failed projections, and retry availability.
- Reset to `Loading` whenever a new source path is selected.
- Transition to `Ready` only after the page reports `MediaOpened`.
- Clear failure detail when retry begins or a source opens successfully.
- Log video finalization exceptions and transition to non-retryable `Finalization` failure.

### WinUI pages

- Subscribe and unsubscribe every `MediaPlayer` to `MediaFailed` alongside existing opened/ended events.
- Report `MediaOpened` to the matching view model.
- Log source initialization exceptions and report `FileUnavailable`.
- Map `SourceNotSupported` to `Unsupported`; map other asynchronous failures to `Playback`.
- For a rendered trim preview failure, log and recover to the original player rather than failing the usable editor.
- Reinitialize the current source when the view model enters `Loading` through retry or source replacement.

### User experience

- Show bounded loading feedback while the platform opens video or audio.
- Replace a blank player with a localized error message on failure.
- Offer **Try again** for retryable failures.
- Keep **Open screen recordings folder** or **Open audio recordings folder** available from the failure panel.
- Keep detailed exceptions and platform error strings in logs only.

## Reliability requirements

- A finalization exception must clear the finalizing indicator, log once, and enter `Failed(Finalization)`.
- A `MediaFailed` event must not leave transport controls or media-dependent commands enabled.
- Retry must clear stale failure state before assigning the source again.
- Page unload must detach `MediaFailed` handlers.
- Trim-preview decoder failure must preserve the original playable source.

## Test plan

- Fail a pending video and verify finalization stops, technical detail is logged, and the state becomes non-retryable `Failed(Finalization)`.
- Report unsupported video and audio media and verify bounded user messages with no source path.
- Verify loading-to-ready transitions after `MediaOpened` reports.
- Verify retry clears failure detail and returns to `Loading`.
- Build WinUI to validate `MediaFailed` event integration and XAML bindings.
- Run every non-UI test project.

## Acceptance criteria

- [x] Pending-video finalization failure is visible, logged, and recoverable through the recordings folder.
- [x] Missing/corrupt/unsupported video and audio no longer leave a silent blank editor.
- [x] Video and audio pages handle asynchronous `MediaFailed` events.
- [x] Retryable failures expose a bounded retry action.
- [x] UI and telemetry do not expose media paths or platform error content.
- [x] Existing non-UI tests pass and the WinUI x64 Debug project builds.
