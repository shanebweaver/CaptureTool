# PRD: Publish terminal audio stop failures to presentation

- Issue: [#415](https://github.com/shanebweaver/CaptureTool/issues/415)
- Finding: `ARCH-10`
- Severity: Medium
- Status: Implemented
- Affected features: `CAP-07`, `APP-07`

## Summary

Every audio stop attempt must publish exactly one terminal state change after the active session has been cleared, whether recorder finalization and post-processing succeed or fail. The terminal event will carry a structured failure payload when applicable, and the audio capture page will derive its timer, controls, waveform, and recoverable error UI from that event.

This keeps application state, presentation state, and navigation protection synchronized after platform or completion failures.

## Problem

`AudioCaptureWorkflow.StopCapture` clears its state-store session in `finally`, but raises `CaptureStateChanged(Stopped)` only after recorder stop succeeds. When the recorder throws, the use-case executor returns a failed response while the page receives no terminal transition. The workflow then reports no active recording, but the page can continue showing an active timer and enabled stop/pause controls.

The existing state-change event contains only `AudioCaptureState`, so it cannot distinguish a successful stop from a terminal stop caused by recorder or completion failure. The page also has no recoverable error surface for that outcome.

## Goals

1. Publish one terminal audio state change for every stop attempt that begins with an active session.
2. Clear the application session before publishing the terminal state.
3. Identify whether failure occurred while stopping the recorder or completing/post-processing the captured file.
4. Reset page recording state, timer, pause bookkeeping, and waveform for successful and failed terminal outcomes.
5. Surface a dismissible, localized failure message while allowing another recording attempt.
6. Ensure navigation protection observes the same inactive state after a failed stop.

## Non-goals

- Do not make a failed recorder stop produce or navigate to an audio edit file.
- Do not retry native recorder finalization automatically.
- Do not expose raw platform exception text directly to users.
- Do not change the existing behavior where auto-save and auto-copy failures are logged and treated as non-fatal output failures.
- Do not redesign the audio capture toolbar or waveform.
- Do not change video capture failure handling.

## State-change contract

Replace the bare state event value with a state-change value containing:

- the authoritative `AudioCaptureState`;
- an optional structured failure;
- a bounded failure stage (`RecorderStop` or `PostProcessing`); and
- the captured exception message for diagnostics/tests, not direct presentation.

Recording and pause/resume transitions carry no failure. A stop attempt publishes `Stopped` once from its terminal cleanup path. On failure, the same event carries the failure payload while the workflow rethrows so the use-case result and logging remain failed.

## Functional requirements

### Workflow finalization

For `StopCapture`:

1. Resolve the active session before invoking the recorder.
2. Treat an exception from `IAudioRecorder.StopCapture` as `RecorderStop` failure.
3. If the recorder returns an audio file, raise the captured-file event and run synchronous post-processing.
4. Treat an exception escaping captured-file completion or post-processing as `PostProcessing` failure.
5. Preserve existing success/failure telemetry and exception propagation.
6. In `finally`, clear the matching state-store session, then publish exactly one `Stopped` state change with the optional failure.
7. Never raise `NewAudioCaptured` or navigate to audio edit when the recorder did not return a file.

The terminal event must observe `IsRecording == false`, `IsPaused == false`, and `CaptureState == Stopped` when subscribers query the state service.

### Presentation recovery

The audio capture page must:

- consume the structured state-change value;
- refresh its Boolean state from the authoritative state service;
- stop and reset the timer for every `Stopped` event;
- clear paused-duration bookkeeping and waveform state;
- show a generic localized error when the terminal event contains a failure;
- avoid displaying raw exception text;
- clear the previous error when a new recording starts or the user dismisses it; and
- leave Start enabled after terminal failure.

The WinUI page must expose the error through a dismissible error `InfoBar` without replacing the capture page.

### Navigation recovery

- A failed stop returns a failed stop use-case response and does not navigate to audio edit.
- Because terminal cleanup clears the active session, a later unrelated navigation attempt must not prompt to stop the already-failed recording.
- Existing successful stop navigation to audio edit remains unchanged.

## Reliability requirements

- State-store cleanup occurs even if recorder stop, captured-file subscribers, post-processing, telemetry, or terminal subscribers throw.
- A terminal event is not duplicated by catch and finally paths.
- Presentation cleanup is safe when dispatched through `ITaskEnvironment`.
- Failure presentation remains recoverable: the user can dismiss the message or start another recording.
- Successful terminal events do not show a failure message.

## Test plan

### Workflow tests

- recorder stop failure clears the session and publishes one stopped event with `RecorderStop` failure;
- post-processing failure clears the session and publishes one stopped event with `PostProcessing` failure;
- successful stop publishes one stopped event without failure and preserves captured-file behavior;
- the state service is already stopped when terminal subscribers run;
- no captured-file event is raised when recorder stop fails.

### Presentation tests

- a failed terminal event resets recording/paused state, timer, and waveform;
- a failed terminal event enables Start and exposes the localized generic error;
- dismiss clears the error;
- a later Recording event clears the previous error and starts clean state;
- a successful Stopped event resets state without showing an error.

### Navigation tests

- stop failure does not dispatch audio-edit navigation;
- after a failed stop clears the workflow session, a later navigation guard check succeeds without prompting again.

### Build and regression validation

- run the Application and Presentation test projects;
- run all non-UI test projects;
- build the WinUI x64 Debug project.

## Acceptance criteria

- [x] Recorder stop failure cannot leave the audio page showing an active recording.
- [x] Post-processing failure produces the same terminal cleanup with the correct failure stage.
- [x] Exactly one terminal state event follows each stop attempt.
- [x] The page shows a dismissible generic error and can start another recording.
- [x] Navigation protection sees no active session after failure, and audio-edit navigation is not attempted without a completed file.
- [x] Existing non-UI tests pass and the WinUI x64 Debug project builds.
