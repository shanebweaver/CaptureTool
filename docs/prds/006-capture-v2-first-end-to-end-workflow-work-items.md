# PRD 006 Work Items: First End-to-End V2 Workflow

## Summary

Break PRD 006 into small, targeted PRs that turn the existing V2 components into a Capture Tool-facing workflow. Each work item should be independently reviewable and should leave the repo in a buildable/testable state.

## Work Items

### 1. Native Recorder Owns A Real V2 Session

**Goal:** Replace the native V2 lifecycle harness with real session ownership behind `CtCaptureV2_Start`.

**Key changes:**

- Add recorder-owned state for an active `CapturePipelineSession`.
- Map `CtCaptureV2_Config` into `CapturePipelineConfig`.
- Start the session from `CtCaptureV2_Start`.
- Route `Pause`, `Resume`, `SetAudioMuted`, `SetAudioGain`, and `Stop` to the active session.
- Ensure `DestroyRecorder` stops/releases an active session before deleting the handle.

**Acceptance tests:**

- Start creates a session, not just copied config state.
- Pause/resume/audio commands call the session.
- Stop tears down and clears active session.
- Destroy while recording performs stop/release.

### 2. Production Graph Factory Assembly

**Goal:** Provide production factories that assemble the first real graph.

**Key changes:**

- Add production `IMediaSourceFactory`, `IMediaProcessorFactory`, and `IOutputSinkFactory` implementations.
- Desktop source uses `WindowsDesktopVideoSource`, WGC provider, monitor resolver, and D3D dependency.
- Audio source uses `WasapiLoopbackAudioSource` only when audio is armed.
- Output sink uses `MediaFoundationFileSink`.
- Keep factory seams injectable for tests.

**Acceptance tests:**

- Video-only config creates desktop source and MP4 sink.
- Video+audio config creates desktop source, WASAPI source, audio controls, and MP4 sink.
- Unsupported source/output combinations fail with structured diagnostics.

### 3. Full-Monitor Video Planning

**Goal:** Make the existing `IScreenRecorder.StartRecording(hMonitor, ...)` path capture the selected full monitor.

**Key changes:**

- Remove the managed adapter's hardcoded `1x1` capture area behavior.
- Define full-monitor semantics for the V2 config path.
- Resolve monitor bounds before output profile planning.
- Ensure H.264 media type has width/height before `MediaFoundationFileSink::Open`.

**Acceptance tests:**

- Adapter no longer emits `1x1` as the default.
- Full-monitor config resolves to selected monitor dimensions.
- Output plan includes valid video media type dimensions.
- Missing/invalid monitor handle fails clearly.

### 4. Terminal Failure Teardown Fix

**Goal:** Make lifecycle cleanup reliable when runtime failures occur after start.

**Key changes:**

- Distinguish "terminal state" from "graph already torn down."
- Ensure `Stop()` still runs teardown if sources/sink/processors are live after failure.
- Preserve idempotent stop after finalization.
- Record failure stage and diagnostics without skipping sink finalization.

**Acceptance tests:**

- Sink write failure marks failure and later stop tears down.
- Processor failure marks failure and later stop tears down.
- Stop after successful finalized session remains idempotent.
- Stop after teardown failure reports first meaningful failure stage.

### 5. Managed Adapter End-To-End Integration

**Goal:** Make `CaptureV2ScreenRecorderAdapter` a reliable bridge from Capture Tool to V2.

**Key changes:**

- Build V2 options from `hMonitor`, output path, and `captureAudio`.
- Treat `captureAudio: false` as video-only MP4.
- Treat `captureAudio: true` as MP4 with armed AAC audio.
- Implement audio toggle as mute/unmute only when audio was armed at start.
- Return `false` on start failure after disposing partial recorder state.

**Acceptance tests:**

- Start with audio disabled builds video-only options.
- Start with audio enabled builds video+audio options.
- Toggle audio is no-op when audio was not armed.
- Toggle audio calls mute/unmute when audio was armed.
- Start failure disposes recorder and returns `false`.

### 6. Feature Flag Default To V2

**Goal:** Make Capture Tool use V2 by default while preserving V1 fallback.

**Key changes:**

- Default `WindowsCaptureInfrastructureOptions.UseCaptureV2ScreenRecorder` to `true`.
- Keep explicit `false` opt-out for V1.
- Keep `CaptureV2ScreenRecorderFactory` override behavior.
- Update dependency injection tests.

**Acceptance tests:**

- Default DI resolves `CaptureV2ScreenRecorderAdapter`.
- Explicit opt-out resolves `WindowsScreenRecorder`.
- Custom V2 factory still wins.
- Application layer remains dependent only on `IScreenRecorder`.

### 7. Native/Managed Error Reporting Hardening

**Goal:** Make failures actionable during the first app run.

**Key changes:**

- Ensure native start/stop failures populate last-error details.
- Include component, operation, result code, native status, and failure stage where possible.
- Preserve managed `CaptureNativeException` translation.
- Ensure adapter cleanup does not erase the useful start failure before it can be observed in tests/logs.

**Acceptance tests:**

- Monitor failure surfaces as monitor/source failure.
- WGC/D3D failure surfaces as desktop activation failure.
- WASAPI failure surfaces as audio activation failure.
- Media Foundation setup/finalize failure surfaces as sink failure.
- Managed exception includes meaningful component and operation.

### 8. First App-Facing Acceptance Harness

**Goal:** Add a controlled acceptance path proving Capture Tool can exercise V2.

**Key changes:**

- Add an integration-style test or manual probe entrypoint that uses the same `IScreenRecorder` path as Capture Tool.
- Record a short selected-monitor MP4 with audio disabled.
- Record a short selected-monitor MP4 with audio enabled when available.
- Include pause/resume and audio toggle coverage.
- Keep machine-dependent tests skippable or clearly categorized if they require desktop/session access.

**Acceptance tests:**

- Output MP4 exists and is non-empty.
- Video-only recording finalizes.
- Video+audio recording finalizes when audio endpoint is available.
- Pause/resume does not crash and produces finalized output.
- Audio toggle does not require adding/removing streams after start.

## Suggested PR Order

1. Terminal failure teardown fix.
2. Full-monitor video planning.
3. Production graph factory assembly.
4. Native recorder owns real session.
5. Managed adapter integration.
6. Feature flag default to V2.
7. Error reporting hardening.
8. App-facing acceptance harness.

## Assumptions

- `IScreenRecorder` remains unchanged.
- V2 is the default once this work lands.
- V1 remains available through explicit opt-out.
- Runtime audio toggle means mute/unmute, not stream creation.
- Full-monitor capture is the expected behavior for the current Capture Tool workflow.
