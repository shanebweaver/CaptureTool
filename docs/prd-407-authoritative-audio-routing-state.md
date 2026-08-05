# PRD: Authoritative Audio Routing State

## Summary

GitHub issue: https://github.com/shanebweaver/CaptureTool/issues/407

Status: approved for implementation in this change.

CaptureTool must use one application-owned audio-routing snapshot as the source of truth for audio-only and video capture. A recorder must start from that explicit snapshot, and a live routing change must appear in application and presentation state only after the platform recorder accepts it.

## Problem

Audio-only capture currently prepares the persisted desktop-audio default in `AudioCaptureStateStore`, but `IAudioRecorder.StartCapture` receives only an output path. `WindowsAudioRecorder` independently initializes desktop audio to enabled and keeps its own mute and microphone fields. The state shown by the application can therefore say desktop audio is off while the native recording includes it.

Video capture passes an explicit snapshot at startup, but its live desktop-audio, microphone-mute, microphone-source, and volume methods update `VideoCaptureStateStore` before calling `IScreenRecorder`. If CaptureKit rejects an operation, the workflow and UI retain the requested value while the active recorder retains the previous value. The capture overlay also changes microphone state optimistically instead of waiting for the use-case result or workflow state event.

These mismatches are privacy-sensitive because a displayed disabled or muted source can remain active in the recorded media.

## Goals

- Make the application workflow the sole owner of desired and effective audio-routing state.
- Start audio-only recording from an explicit immutable settings snapshot.
- Keep active-session state equal to the settings last accepted by the platform recorder.
- Keep presentation mute, desktop-audio, and source selection synchronized with committed workflow state.
- Keep every audio route mutable for the lifetime of a capture, including enabling a route that was disabled when recording started.
- Preserve idle configuration so users can choose sources before capture starts.
- Add deterministic failure-path coverage for every live routing operation.

## Non-Goals

- Changing CaptureKit's device enumeration, codec behavior, or mixing behavior beyond keeping video-session audio controls live throughout capture.
- Adding audio mixing, per-source gain, or audio-only volume controls.
- Changing persisted default values or settings-page UX.
- Changing pause/resume behavior, which is not an audio-routing operation.
- Guaranteeing that a device remains available after the platform accepts it.

## User Stories

- As a user whose audio-only default is off, I can trust that desktop audio is not included when recording starts.
- As a user muting a microphone during recording, I see muted only after the recorder accepts the change.
- As a user changing an input source, a platform failure leaves the previous effective source selected instead of displaying an uncommitted choice.
- As a user configuring a capture before it starts, my desktop-audio and microphone choices are included in the recorder's startup options.
- As a user who starts a video with local audio muted, I can unmute local audio during that same recording.

## Product Requirements

### Explicit audio-only startup contract

1. `IAudioRecorder` must start from an immutable options value containing the output path, desktop-audio choice, active microphone source, and input volume.
2. `AudioCaptureWorkflow` must build those options from the newly started session's settings.
3. A muted microphone must be represented by a null active microphone source while retaining the selected source in application state for later unmute.
4. `WindowsAudioRecorder` must not maintain a second pre-start copy of desktop-audio, mute, or selected-source state.
5. With the persisted audio-only desktop-audio default disabled, the CaptureKit session must receive `CaptureAudio = false`.

### Transactional live routing

1. For an active audio-only or video session, each routing operation must compute a candidate settings snapshot without committing it.
2. The workflow must apply the candidate's effective value to the platform recorder first.
3. Only a successful platform call may commit the candidate to the state store and publish its state-change event.
4. If the platform call throws, the previous state and displayed value must remain in effect and the use case must return a failed response through the existing executor boundary.
5. Idle routing changes must update application state without calling a recorder that has no active session.
6. The transactional rule applies to desktop audio, microphone mute, microphone source, and microphone input volume where that operation exists.
7. A disabled startup value is an initial routing state, not a declaration that the route is unavailable. A live false-to-true transition must be sent to the active recorder and committed on success.
8. CaptureKit video sessions must always provision the local-audio pipeline. `CaptureAudio = false` is only the initial muted state: it must not record non-silent local-audio samples before the user enables it, and mute, unmute, source, and volume controls must remain available throughout capture.

### Presentation synchronization

1. Audio and video workflows must publish successful microphone-source changes just as they publish successful mute and desktop-audio changes.
2. Capture view models must derive displayed mute, desktop-audio, and selected-source values from committed workflow state.
3. The video overlay microphone-mute command must await its use-case response and must not optimistically change the displayed value.
4. Source-selection UI must not retain a requested source when its use case fails.
5. Existing bounded error handling may report the failed action, but it must not invent a successful state transition.
6. After a successful desktop-audio command, the video overlay must synchronize directly from committed capture state; the state-change event remains the notification path for changes initiated elsewhere.

## Design Decision

Keep routing settings as immutable value records owned by the application state stores. Add non-mutating candidate creation and explicit commit operations to the stores. Workflows coordinate the transaction:

1. read current state;
2. derive a candidate;
3. apply the candidate's effective platform value when recording;
4. commit and publish only after success.

For audio-only startup, replace the path-only recorder call with `AudioCaptureRecordingOptions`. The Windows adapter becomes a thin active-session wrapper and no longer caches routing choices before startup.

This design keeps platform exceptions inside the existing use-case executor path and avoids compensating updates, which could also fail. It also preserves pre-capture configuration because idle candidates commit directly and become the next session's startup snapshot.

## Acceptance Criteria

- Audio-only capture with desktop audio disabled passes disabled desktop audio to CaptureKit at startup.
- Audio-only startup passes the selected unmuted microphone source and omits a muted source.
- The Windows audio adapter has no independent pre-start routing fields.
- Failed live desktop-audio, mute, source, or volume operations leave application state unchanged.
- Failed live operations do not publish success state-change events.
- Successful live operations update the recorder before committing and publishing state.
- A video recording started with desktop audio disabled can enable desktop audio without restarting the capture.
- Starting with every audio source disabled still creates a muted audio stream capable of accepting later live routing changes.
- Idle audio-routing changes do not call an inactive recorder.
- The video overlay does not change its displayed mute/source state after a failed use case.
- Existing audio and video capture behavior and non-UI tests remain green.

## Validation Plan

### Automated

- Assert the complete audio-only startup options passed by `AudioCaptureWorkflow`.
- Assert `WindowsAudioRecorder` maps immutable startup options to CaptureKit options.
- Inject failures for audio-only desktop-audio, mute, and source changes and verify state/event rollback semantics.
- Inject failures for video desktop-audio, mute, source, and volume changes and verify state/event rollback semantics.
- Start a video workflow with desktop audio disabled, enable it while recording, and assert the active recorder receives `true` before state and presentation commit the new value.
- Exercise CaptureKit with an initially muted video session and assert the audio source starts disabled, the AAC stream exists, a live false-to-true transition enables the source, and volume remains mutable.
- Add presentation tests for failed video mute and source selection.
- Run all application, presentation, Windows capture infrastructure, generic infrastructure, Windows edit infrastructure, and MCP server tests.

### Manual

If a packaged smoke test is performed, disable the default desktop-audio setting and start audio-only capture, then verify the resulting WAV contains no desktop playback. During video capture, toggle desktop audio and microphone mute while observing that the toolbar changes only after the recorder accepts the action.

## Risks and Mitigations

- **Public port change:** `IAudioRecorder.StartCapture` changes signature. The interface has one production implementation and test doubles; compile-time failures identify every caller.
- **Event ordering:** Recorder calls are synchronous. Publishing only after the call returns gives subscribers a committed snapshot and preserves current ordering expectations.
- **Device removal:** A removed microphone may make a source update fail. The previous effective state remains authoritative; device-list refresh can continue through the existing detection flow.
- **Repeated input:** Setting a value already in effect should be a no-op, avoiding redundant platform calls and duplicate events.

## Rollout

The change requires no migration or feature flag. It changes capture routing coordination immediately while retaining existing persisted settings and capture file formats.
