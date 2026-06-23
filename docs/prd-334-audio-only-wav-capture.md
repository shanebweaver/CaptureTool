# PRD: Audio-Only Capture Pipeline With WAV Output

## Summary

GitHub issue: https://github.com/shanebweaver/CaptureTool/issues/334

Implementation status: initial pipeline implementation started. The native audio-only session, WAV writer, managed recorder, stop navigation, and recent capture refresh plumbing are now in place. Remaining work is primarily device-picker UI, waveform visualization, audio-specific settings, and deeper audio format validation.

CaptureTool should support an audio-only capture mode that reuses the existing recording pipeline concepts while removing the requirement for a graphics capture target. Audio recordings must be written as `.wav` files, surfaced through the existing audio capture and audio edit experience, and compatible with recent captures, copy, save as, and future waveform display.

This PRD focuses on adapting the existing video capture pipeline for audio-only recording. The broader issue also includes home page and file menu entry points, device selection, waveform visualization, and settings. Those are included as integration requirements but should not force the native recording pipeline to depend on UI work.

## Problem

The app has established image and video capture flows, but the audio flow is only partially wired:

- `AudioCapturePage` and `AudioEditPage` exist.
- `AudioCaptureHandler` exists but `StopCapture` currently throws after stopping.
- `IAudioRecorder` exists but `WindowsAudioRecorder` is fully unimplemented.
- The native recording path is centered on `ScreenRecorder`, `WindowsGraphicsCaptureSession`, and `WindowsMFMP4SinkWriter`, which require a video source and MP4 output.
- Recent captures already recognize `.wav`, but no implemented capture path produces one.

Users need to start, pause, stop, and save an audio recording without selecting a screen region or opening the capture overlay.

## Goals

- Add a working audio-only recording pipeline that produces valid WAV files.
- Reuse existing audio capture primitives from the video pipeline where appropriate: audio device selection, enable/mute behavior, media clock timing, audio sample callbacks, and lifecycle semantics.
- Keep video recording behavior unchanged.
- Complete the existing `IAudioRecorder` and `AudioCaptureHandler` path so the current audio capture page can record and navigate to audio edit after stop.
- Make generated `.wav` files visible and openable from recent captures.
- Provide a foundation for waveform display through audio sample callbacks or an equivalent stream of sample metadata.

## Non-Goals

- Editing audio beyond playback, copy, and save as.
- Export formats other than WAV.
- Mixing multiple audio sources into one file unless the existing source abstraction already provides it.
- Hidden or dummy video capture to satisfy the current MP4 session shape.
- Rebuilding the video recording path.

## User Stories

- As a user, I can open audio capture from the main app and record audio without selecting a monitor, window, or rectangle.
- As a user, I can pause and resume an active audio recording.
- As a user, I can mute/unmute input while recording.
- As a user, I can choose whether local/system audio is captured when the audio capture mode supports it.
- As a user, stopping an audio recording takes me to the audio edit page with a playable recording.
- As a user, I can save or copy the resulting WAV file.
- As a user, recent captures includes newly recorded audio and reopens it in the audio edit page.

## Current Architecture

### Managed Layer

- `IAudioRecorder` defines `StartCapture`, `Pause`, `StopCapture`, `ToggleDesktopAudio`, and `ToggleMute`.
- `WindowsAudioRecorder` is registered for `IAudioRecorder` and now calls the native audio-only recorder exports.
- `AudioCaptureHandler` wraps `IAudioRecorder`, creates temporary `.wav` paths, returns the stopped `IAudioFile`, and emits `NewAudioCaptured`.
- `AudioCapturePageViewModel` already exposes start, stop, pause, mute, and local audio commands.
- `AudioEditPageViewModel` already supports load, save as, and copy for `IAudioFile`.
- `FileTypeDetector` already treats `.wav` as audio.
- `OpenRecentCaptureUseCase` already routes audio files to audio edit.

### Native Layer

- `ScreenRecorder` exposes start/pause/resume/stop methods for screen recording.
- `AudioRecorder` exposes start/pause/resume/stop methods for audio-only recording.
- `CaptureSessionConfig` requires a monitor, window, or rectangle target.
- `WindowsGraphicsCaptureSessionFactory` always creates both audio and video capture sources plus an MP4 sink writer.
- `WindowsGraphicsCaptureSession` initializes the video source before the sink writer and starts video capture after audio capture.
- `WindowsMFMP4SinkWriter` writes H.264 video and optional AAC audio to MP4.
- `WindowsWaveSinkWriter` writes audio-only RIFF/WAVE output.
- `WindowsLocalAudioCaptureSource`, `AudioCaptureHandler`, `SimpleMediaClock`, and `AudioSampleData` are reusable building blocks for audio-only capture.

## Requirements

### Functional Requirements

1. Audio capture mode must start recording without a graphics capture target.
2. Audio capture mode must write a temporary `.wav` file under the application temporary folder.
3. `IAudioRecorder.StopCapture` must return an `IAudioFile` whose path points to the completed WAV file.
4. Stop must finalize the WAV file before the audio edit page attempts playback.
5. Pause/resume must preserve elapsed media timing and avoid writing samples while paused.
6. Mute must result in silence or disabled sample writes according to the chosen audio source behavior, but the WAV file must remain valid.
7. Local/system audio toggle must use the same semantics exposed by the UI; if the implementation only supports one audio source initially, the UI must disable or clearly reflect unsupported sources.
8. Audio input device selection must be compatible with the existing `IAudioInputDetectionService` model and the native audio source device id support.
9. WAV output must be playable by `MediaPlayerElement` and common Windows media players.
10. Recently captured audio must appear in the recent captures list and reopen in audio edit.
11. Auto-save audio settings must copy completed WAV files to the configured audio folder, falling back to a system default music/audio folder when no folder is configured.
12. Audio sample data must be observable by presentation code or an application service for waveform visualization during recording.

### Technical Requirements

1. Implement `WindowsAudioRecorder` instead of routing audio capture through a fake screen recording target.
2. Introduce a native audio-only recorder/session path that can own:
   - `IMediaClock`
   - `IAudioCaptureSource`
   - a new WAV sink writer
   - optional audio sample callback registry
3. Introduce a WAV sink abstraction, for example `IWavSinkWriter`, separate from `IMP4SinkWriter`.
4. The WAV writer must support the format emitted by `IAudioCaptureSource` or perform a defined conversion to PCM.
5. The WAV writer must write a valid RIFF/WAVE header and patch final sizes during finalization, or use a Media Foundation sink writer configured for WAV.
6. The native API surface must expose audio-only start, pause, resume, stop, audio enable/mute, input source, volume, and audio sample callback operations.
7. Managed interop must map native failures to `CaptureRecorderResult` or an audio-specific result with equivalent status handling.
8. The video recording path must keep its current API and behavior unless shared abstractions are mechanically renamed.
9. Session state handling must prevent double-start, stop-before-start, and callback-after-stop races.
10. Finalization must be idempotent and safe from destructor cleanup, matching the existing native RAII style.

### UX Requirements

1. Audio capture lives in a navigable main window page, not the selection overlay.
2. The page includes start/stop, pause/resume, mute, local/system audio toggle, and device picker.
3. The page shows a waveform or level visualization while recording.
4. Stop navigates to `AudioEditPage` with the completed `AudioFile`.
5. Audio edit provides save as and copy actions.
6. Home and file menu entry points include audio capture.

## Proposed Implementation

### Phase 1: Native Audio-Only Session

- Add an audio-only native API alongside `ScreenRecorder`, for example `AudioRecorder`.
- Add `AudioRecordingOptions` with:
  - `outputPath`
  - `captureAudio`
  - `audioInputSourceId`
  - `audioInputVolumePercentage`
  - optional output format defaults
- Create `WindowsAudioCaptureSession` or a similarly named class that reuses:
  - `WindowsLocalAudioCaptureSourceFactory`
  - `SimpleMediaClockFactory`
  - `CallbackRegistry<AudioSampleData>`
- Add `IWavSinkWriter` and `WindowsMFWavSinkWriter` or `WindowsWaveSinkWriter`.
- Add tests for start, pause, resume, stop, invalid state transitions, callback forwarding, and WAV finalization.

### Phase 2: Managed Recorder Integration

- Implement `WindowsAudioRecorder`.
- Generate temporary filenames with `.wav`.
- Complete `AudioCaptureHandler.StopCapture` so it returns the `IAudioFile` from the recorder and raises the stopped state without throwing.
- Add `NewAudioCaptured` event to `IAudioCaptureHandler` if recent captures or app menu refresh needs parity with image/video capture events.
- Wire stop flow so the stop use case navigates to audio edit with the returned `AudioFile`.
- Add application tests replacing the current expected `NotImplementedException`.

### Phase 3: UI and Settings Integration

- Add device picker binding to audio capture page.
- Add waveform display backed by audio sample callbacks or a throttled waveform service.
- Add audio auto-save settings:
  - `Settings_AudioCapture_AutoSave`
  - `Settings_AudioCapture_AutoSaveFolder`
  - optional `Settings_AudioCapture_DefaultLocalAudioEnabled`
- Add storage support for a default audio folder, likely Music.
- Ensure home page and file menu entry points route to audio capture.
- Refresh recent captures when an audio recording completes.

## Acceptance Criteria

- Starting audio capture from the audio page creates an active recording without showing the overlay.
- Stopping audio capture creates a non-empty `.wav` file in the app temporary folder.
- The generated file opens in `AudioEditPage` and plays through `MediaPlayerElement`.
- Save as writes a `.wav` copy selected by the user.
- Copy places the completed WAV file on the clipboard.
- Pause and resume do not corrupt the resulting file.
- Mute produces expected muted behavior without corrupting the file.
- Recent captures displays the new WAV file and opens it in audio edit.
- Existing video capture tests and manual video recording still pass.
- Native tests cover audio-only session lifecycle and WAV finalization.
- Application tests cover `AudioCaptureHandler`, stop use case navigation, recent capture detection, and settings defaults.

## Open Questions

- Should audio-only capture record microphone input, local/system audio, or both in the first implementation?
- Should mute write silence to preserve timeline duration, or disable sample writing and shorten the file?
- Should WAV output preserve the source format, such as IEEE float, or normalize to PCM 16-bit?
- What is the default auto-save location: Music, Documents, or the existing temporary folder until configured?
- Should waveform data be raw sample buffers, RMS/peak levels, or pre-windowed points for UI efficiency?

## Risks

- The current native session assumes a video source exists, so forcing audio-only through it would create fragile coupling and unnecessary graphics dependencies.
- WAV output may require audio format conversion if the WASAPI source emits a format not accepted by the chosen writer.
- Stop/finalize can block UI if managed code waits synchronously; video currently finalizes on a background task.
- Callback lifetimes need the same care as video capture to avoid callbacks after stop or during teardown.
- Recent captures currently takes the latest five files from the temporary folder, so audio can displace image/video items unless the list becomes type-aware later.

## Tracking Checklist

- [x] Native audio-only recorder API added.
- [x] WAV sink writer added and build-tested.
- [x] `WindowsAudioRecorder` implemented.
- [x] `AudioCaptureHandler.StopCapture` returns `IAudioFile`.
- [x] Stop use case navigates to audio edit.
- [ ] Audio sample stream exposed for waveform.
- [ ] Audio settings and default folder added.
- [x] Recent captures refreshes for audio completion.
- [ ] Manual recording test completed for microphone/local audio.
- [ ] Regression test completed for video recording with and without audio.
