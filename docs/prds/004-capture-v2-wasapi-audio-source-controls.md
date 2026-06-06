# PRD 004: CaptureInterop V2 WASAPI Audio Source and Audio Controls

## Status

Draft

## Related Documents

- [CaptureInterop V2 Architecture Plan](../architecture/capture-v2-architecture.md)
- [PRD 001: CaptureInterop V2 Core Pipeline](001-capture-v2-core-pipeline.md)
- [PRD 002: CaptureInterop V2 Native API and Managed Facade](002-capture-v2-native-api-managed-facade.md)
- [PRD 003: CaptureInterop V2 Desktop Video Source](003-capture-v2-desktop-video-source.md)

## Summary

Build the CaptureInterop V2 Windows system-audio source and audio control path. This PRD covers WASAPI loopback capture, armed vs muted semantics, pause behavior, source-specific recording gain, silence generation, audio buffer ownership, diagnostics, and the interface shape needed for future microphone and mixer support.

The first implementation captures local system audio through WASAPI loopback and feeds the V2 core pipeline as an audio source. It must support the first desktop MP4 workflow with optional AAC audio, runtime mute/unmute, runtime gain changes, pause/resume without timestamp drift, and deterministic teardown.

## Problem

Audio capture is easy to make audible and hard to make correct. WASAPI loopback packets have borrowed buffer lifetimes, device-dependent formats, threading constraints, silent-period behavior, endpoint changes, and timing details that can create subtle A/V drift or file-duration bugs.

The app also needs a clear distinction between two user-facing concepts that are often blurred:

- Whether an audio track exists in the recording.
- Whether the audio track is currently writing audible samples.

For CaptureInterop V2, that distinction is:

- `armed`: the audio source and output track exist for the session.
- `muted`: the source is present, but samples written to the output are silence.

Without this distinction, enabling audio midway through recording would require adding a media stream after the sink starts, which is not a valid shape for the MP4 workflow. Without explicit buffer ownership, the pipeline can accidentally publish pointers into WASAPI packet memory after `ReleaseBuffer`. Without well-defined silence and pause behavior, recordings can develop audio gaps, mismatched durations, or timestamp drift.

## Goals

- Implement a V2 `WasapiLoopbackAudioSource` for local system-audio capture.
- Capture from the default render endpoint for the first workflow.
- Represent audio source and stream identity with stable V2 ids.
- Report endpoint mix format through V2 audio media type descriptors.
- Copy or pool WASAPI packet data before publishing samples downstream.
- Support `armed`, `muted`, and `gain` as separate concepts.
- Generate silence for muted intervals while preserving sample duration and recording-relative timestamps.
- Preserve audio continuity during endpoint silent periods where practical.
- Ensure pause/resume excludes paused duration from the output and does not generate silence for paused time.
- Apply source-specific recording gain without changing Windows endpoint, application session, or hardware volume.
- Provide diagnostics for packet timing, gaps, silence, discontinuities, clipping, and device errors.
- Shape the audio graph so future microphone and mixer support can be added without redesigning first-version controls.

## Non-Goals

- Implement microphone capture.
- Implement multi-source audio mixing.
- Implement per-application audio capture.
- Implement endpoint hardware volume or Windows session-volume control.
- Implement echo cancellation, noise suppression, automatic gain control, or metering UI.
- Implement AAC encoding or Media Foundation sink writing.
- Implement managed UI behavior.
- Support non-Windows audio APIs.
- Support hot-swapping audio devices during an active session.

## Users

Primary users:

- Native developers implementing the first V2 desktop MP4 workflow.
- Native developers implementing audio processors and output sinks that consume V2 audio samples.

Secondary users:

- Managed infrastructure developers mapping `SystemAudioCaptureSourceOptions` into native config.
- Application developers wiring mute and source gain controls.
- Test authors validating audio timing, silence, and ownership behavior.

## Scope

### In Scope

- WASAPI loopback source for system audio.
- Default render endpoint selection.
- Source and stream descriptors for system audio.
- Endpoint mix-format discovery and V2 media type mapping.
- Audio capture loop thread.
- Packet-to-sample conversion.
- Audio buffer ownership and pooling.
- Muted-state silence generation.
- Silent endpoint or missing-packet continuity handling.
- Recording-only gain processor.
- Pause-aware audio sample routing.
- Source-specific runtime audio commands.
- Diagnostics and counters.
- Fake audio source and fake audio sink tests.
- Local Windows probe for endpoint format and packet timing.

### Out of Scope

- Real microphone source implementation.
- Real mixer implementation.
- AAC encoder behavior.
- MP4 muxing behavior.
- Managed facade implementation beyond DTO/control alignment.
- Device enumeration UI.
- Device change recovery beyond reporting a clear failure or unsupported transition.

## Recommended Initial Approach

Add a Windows audio source implementation behind the V2 source contract:

```cpp
class WasapiLoopbackAudioSource : public IAudioCaptureSource
{
};
```

Use a narrow provider boundary so WASAPI details do not leak into the core:

```text
WasapiLoopbackAudioSource
  -> IWasapiAudioClientFactory
  -> IAudioClient
  -> IAudioCaptureClient
```

For the first implementation:

- Use shared-mode WASAPI loopback.
- Capture from the default render endpoint.
- Use the endpoint mix format as the source media type.
- Normalize only when required by downstream processors or sink input requirements.
- Keep endpoint/device selection extensible in config, but do not implement full device enumeration in this PRD.

## Requirements

### Source Identity

The WASAPI source must expose stable V2 identity.

Acceptance criteria:

- The source descriptor includes `SourceId`, `SourceKind::SystemAudio`, and a human-readable name.
- The source exposes one audio stream descriptor for the life of the session.
- The stream descriptor includes a stable `StreamId`, `MediaKind::Audio`, and `AudioMediaType`.
- Runtime mute and gain commands target the configured source id.
- Missing or non-audio source ids return a stable unsupported-operation or not-found result.

### Device Selection

The first workflow must capture local system audio from the default render endpoint.

Acceptance criteria:

- The source uses the default render endpoint for first-version `AudioDeviceSelection::DefaultRenderEndpoint`.
- The source records endpoint id, friendly name, role, and mix format in diagnostics where available.
- If no default render endpoint is available, start fails with a structured device-not-found result.
- If the selected endpoint is not a render endpoint, validation fails before capture starts.
- Full endpoint enumeration and user selection can be deferred, but the config shape leaves room for a future endpoint id.

### WASAPI Initialization

The source must initialize WASAPI resources deterministically.

Acceptance criteria:

- COM and WASAPI objects are owned with RAII wrappers, preferably WIL where appropriate.
- The source uses `AUDCLNT_STREAMFLAGS_LOOPBACK` for system-audio capture.
- The source stores an immutable copy of the endpoint mix format after initialization.
- Initialization reports unsupported formats, unavailable devices, and activation failures through structured diagnostics.
- `Start` fails if initialization has not succeeded.
- `Stop` is safe to call after partial initialization failure.
- Teardown releases `IAudioCaptureClient`, `IAudioClient`, endpoint objects, events, and thread resources in deterministic order.

### Audio Media Type and Format Handling

The source must map WASAPI format information into V2 audio media type descriptors.

Acceptance criteria:

- PCM integer and IEEE float formats map to explicit V2 `AudioSampleFormat` values.
- Unknown or unsupported formats return a structured unsupported-format result.
- The media type includes sample rate, channel count, bits per sample, block align, and sample format.
- Channel mask and valid bits per sample are preserved in diagnostics or an extensible metadata field when available.
- The source does not assume stereo or 48 kHz.
- Format conversion is represented as a downstream processor when sink requirements differ from the endpoint mix format.

### Capture Loop

The source must capture audio on a dedicated native thread.

Acceptance criteria:

- The capture loop runs independently from the UI thread and session control thread.
- The loop uses event-driven capture when supported and a documented fallback otherwise.
- The loop drains all available packets before waiting again.
- The loop handles `AUDCLNT_BUFFERFLAGS_SILENT` by publishing an owned silent sample with the packet duration.
- The loop reports `AUDCLNT_BUFFERFLAGS_DATA_DISCONTINUITY` as a diagnostic event and counter.
- The loop does not call downstream handlers while holding WASAPI or source-state locks.
- The loop can be stopped promptly and joined during teardown.
- No sample callback is invoked after `Stop` completes.

### Timing

The source must provide enough timing data for the core recording clock to produce recording-relative timestamps.

Acceptance criteria:

- Each audio sample includes frame count, duration, source timestamp metadata, and stream id.
- Source timestamp metadata records whether timing came from WASAPI packet position, QPC, or generated continuity logic.
- The core recording clock remains authoritative for output timestamps.
- Muting audio does not advance or pause the recording clock independently.
- Gain changes do not affect timestamps.
- Audio discontinuities are reported without silently hiding them as normal packets.
- A probe can log packet durations, gaps, discontinuities, endpoint format, and observed timestamp source.

### Armed vs Muted

The implementation must keep armed and muted behavior separate.

Acceptance criteria:

- `armed = false` means no WASAPI source is created and no audio output track is planned.
- `armed = false` runtime mute and gain commands return a clear unsupported-operation result.
- `armed = true, initiallyMuted = true` creates the source and output track, but writes silence until unmuted.
- `armed = true, initiallyMuted = false` captures and writes audible audio immediately.
- Muting an armed source changes only the mute gate state.
- Unmuting an armed source does not rebuild the graph or reconfigure output streams.
- Muted state survives pause/resume.
- Muted state changes are applied to future samples and do not rewrite already queued samples.

### Pause Behavior

Pause and mute must behave differently.

Acceptance criteria:

- Pausing the session does not mean muting audio.
- While paused, no audio samples are written to the sink.
- Paused wall-clock duration is excluded from output timestamps.
- The capture loop may keep draining WASAPI packets while paused to avoid endpoint backlog.
- Samples captured while paused are dropped before gain, mute, encoding, or sink writing.
- Silence is not generated to fill paused duration.
- Resuming resets gap-continuity state so the pause interval is not interpreted as an audio dropout.
- Runtime mute and gain commands may be accepted while paused and apply to samples after resume.

### Gain Processor

Audio gain must be a recording-pipeline operation.

Acceptance criteria:

- Default gain is `0.0 dB`.
- Supported gain range is at least `-60.0 dB` to `+12.0 dB`, unless the core config chooses a narrower range.
- Gain outside the supported range returns a validation or range result.
- Gain is applied per source before mute.
- Mute overrides gain.
- Runtime gain changes affect only future samples.
- Gain is applied to captured sample data, not to Windows endpoint volume, application session volume, or microphone hardware volume.
- Positive gain that clips samples increments a clipping counter or emits a diagnostic event.
- Gain implementation supports PCM integer and float samples accepted by the source, or explicitly inserts a format normalizer before gain.
- Tests verify unity, attenuation, positive gain, clipping behavior, and mute override.

### Silence Generation

The audio path must generate silence intentionally, not by dropping packets.

Acceptance criteria:

- Muted samples preserve original frame count and duration.
- Muted samples contain valid zero-value audio for the sample format.
- Float silence is `0.0f`.
- PCM silence is zero-filled for signed PCM formats supported by the first implementation.
- Silence generation does not allocate a new buffer for every packet when a safe pool or reusable owned buffer is available.
- Reused silent buffers are not mutated while downstream components still hold them.
- Endpoint silent packets produce owned silent samples.
- If WASAPI provides no packets for a silent period, the source or a continuity component can synthesize bounded silence based on device period and recording clock policy.
- Synthesized silence is marked in sample metadata for diagnostics.
- Silence is not generated for paused intervals.

### Buffer Ownership

The source must never publish borrowed WASAPI packet memory downstream.

Acceptance criteria:

- WASAPI packet memory is treated as borrowed only until `IAudioCaptureClient::ReleaseBuffer`.
- The source copies packet data into a V2-owned audio sample buffer before invoking downstream handlers.
- The published audio sample owns or shares ownership of its buffer until all downstream consumers release it.
- Sample buffers may use pooling, but reuse cannot occur while a sink, processor, or callback still references the data.
- Audio sample metadata owns or references immutable format information with a lifetime longer than the sample.
- No downstream component receives a raw pointer that becomes invalid when the capture callback returns.
- Tests prove a sample can safely outlive the WASAPI packet callback.

### Threading and Command Safety

Audio capture and runtime controls must be thread-safe.

Acceptance criteria:

- Start, stop, pause-state updates, mute changes, and gain changes are safe across the session control thread and capture thread.
- Runtime command updates use atomics or a serialized control path with documented memory ordering.
- The capture thread reads current muted and gain state without blocking on long-running control operations.
- Stop prevents new callbacks and waits for in-flight callback dispatch to complete or routes through a session-owned drain mechanism.
- The source does not hold internal locks while invoking processors, sinks, or native-to-managed observer callbacks.

### Diagnostics

The audio source and controls must expose useful diagnostics.

Acceptance criteria:

- Counters include captured packets, captured frames, silent packets, synthesized silence frames, dropped paused frames, discontinuities, clipping events, and late or gap-filled samples.
- Errors include component, operation, HRESULT, source id, stream id, and endpoint id where available.
- Stop result diagnostics can include audio discontinuity and clipping counters.
- Local probe output includes endpoint name, format, buffer duration, packet cadence, discontinuities, silence behavior, and timestamp source.
- Diagnostics do not require managed callbacks to be registered.

### Future Microphone and Mixer Shape

The first implementation must leave a clear path for microphone and mixed audio.

Target future graph shape:

```text
System audio -> format normalizer -> gain -> mute -
                                                 -> mixer -> output track
Microphone   -> format normalizer -> gain -> mute -
```

Acceptance criteria:

- Source config distinguishes `SystemAudio` from future `Microphone`.
- Gain and mute are source-specific before any future mixer stage.
- The future mixer input contract expects normalized sample rate, channel layout, and sample format.
- The first implementation writes one output audio track.
- The first implementation does not require a mixer when only system audio is armed.
- The source/control design does not treat UI volume zero as equivalent to unarmed.
- Future microphone hardware gain, endpoint mute, echo cancellation, and monitoring are explicitly separate from first-version recording gain.
- Future multi-track output is not blocked by first-version source and stream identity choices.

## User Stories

### User: Toggle System Audio During Recording

As a user recording the desktop, I want to turn system audio off and on during recording so that I can temporarily hide audio without breaking the output file.

Acceptance criteria:

- When audio is armed, muting writes silence instead of removing the track.
- Unmuting resumes audible captured audio without rebuilding the graph.
- Output audio duration remains aligned with video duration, excluding paused time.

### Native Developer: Own Audio Buffers Safely

As a native developer, I want WASAPI packet data copied or pooled into owned samples so that downstream processors and sinks never reference released packet memory.

Acceptance criteria:

- Samples remain valid after `ReleaseBuffer`.
- A fake sink can retain samples beyond the capture callback without reading invalid memory.
- Buffer pool reuse is covered by tests.

### Application Developer: Adjust Recording Gain

As an application developer, I want source-specific gain control so that system audio recording volume can change without changing the user's actual speaker volume.

Acceptance criteria:

- Gain commands target the system-audio source id.
- Endpoint volume is not changed.
- Future microphone gain can use the same command shape for recording gain.

### Test Author: Validate Pause Semantics

As a test author, I want pause to drop captured audio rather than synthesize silence so that paused time is excluded from the output.

Acceptance criteria:

- Audio generated during pause is not written to the sink.
- Resume does not fill the pause interval with silence.
- Muted state before pause is still active after resume.

## Technical Constraints

- C++20.
- Windows-first implementation.
- No new third-party dependencies.
- Use WIL for COM pointers, handles, HRESULT handling, and cleanup where useful.
- The V2 core must not depend on WASAPI headers.
- WASAPI types stay in the Windows infrastructure layer.
- Capture callbacks must not require UI thread affinity.
- Tests that require real audio hardware are probe or integration tests, not core unit tests.

## Proposed Deliverables

- `WasapiLoopbackAudioSource`.
- WASAPI audio client factory/provider abstraction.
- V2 audio sample buffer type or ownership wrapper.
- Endpoint mix-format to `AudioMediaType` mapper.
- Audio gain processor implementation.
- Audio mute gate implementation.
- Silence generation helper or processor.
- Pause-aware audio routing behavior.
- Audio diagnostics counters and events.
- Fake audio source and fake audio sink for tests.
- Local WASAPI loopback probe.
- Unit tests for armed/muted/gain/pause/buffer ownership behavior.
- Integration tests or probe documentation for default render endpoint capture.

## Testing Requirements

Unit tests must cover:

- Source and stream descriptor identity.
- Armed false rejects runtime mute and gain.
- Armed true with initially muted emits silence.
- Muted samples preserve frame count, duration, and timestamp metadata.
- Unmuted samples preserve captured data.
- Pause drops samples and does not generate silence.
- Resume does not fill paused duration.
- Gain defaults to unity.
- Gain attenuation and positive gain.
- Gain range rejection.
- Mute overrides gain.
- Clipping diagnostics for positive gain where applicable.
- Buffer ownership after simulated WASAPI packet release.
- No callbacks after stop.
- Discontinuity diagnostics.
- Silent packet handling.
- Synthesized silence metadata when continuity generation is used.

Integration/probe tests should cover:

- Default render endpoint activation.
- Endpoint mix-format logging.
- Start and stop loopback capture.
- Packet cadence while audio is playing.
- Behavior while system audio is silent.
- Mute/unmute during capture.
- Gain changes during capture.
- Pause/resume during capture.
- Device removal or default-device change behavior where practical.

## Targeted PR Chunks

Each chunk should be small enough for one focused pull request. Default execution rule: one chunk equals one PR. Combining adjacent chunks should require an explicit reason, such as a trivial mechanical follow-up with no additional behavior.

### PR 004-01: System Audio Source Contract

Objective:

- Add the V2 system-audio source shell and identity mapping without touching real WASAPI devices.

Deliverables:

- `WasapiLoopbackAudioSource` skeleton.
- System-audio source config mapping.
- Source descriptor creation.
- Audio stream descriptor creation.
- Fake provider or fake packet source seam for tests.
- Source id and stream id tests.

Acceptance criteria:

- The source descriptor includes `SourceId`, `SourceKind::SystemAudio`, and a human-readable name.
- The source exposes one audio stream descriptor for the session.
- Runtime mute and gain commands can target the configured source id at the shell level.
- Missing or non-audio source ids return stable not-found or unsupported-operation results.
- No COM initialization, endpoint activation, capture thread, buffer pool, mute/gain processing, or real WASAPI calls are included in this PR.

Suggested tests:

- Source descriptor is stable.
- Stream descriptor is stable.
- Source id is retained from config.
- Non-matching source id command fails.
- Fake provider can be injected without real audio hardware.

### PR 004-02: Audio Format Mapping

Objective:

- Map Windows audio format descriptions into V2 `AudioMediaType` using fake or captured format data, without starting capture.

Deliverables:

- Endpoint mix-format to `AudioMediaType` mapper.
- PCM integer format mapping.
- IEEE float format mapping.
- Unsupported format result.
- Diagnostics fields for channel mask and valid bits per sample where available.

Acceptance criteria:

- PCM integer formats map to explicit V2 sample formats.
- IEEE float formats map to explicit V2 sample formats.
- Unknown or unsupported formats return a structured unsupported-format result.
- Media type includes sample rate, channel count, bits per sample, block align, and sample format.
- The mapper does not assume stereo or 48 kHz.
- No endpoint activation, capture loop, buffer ownership, mute/gain behavior, or pause behavior is included in this PR.

Suggested tests:

- PCM16 maps correctly.
- PCM24 or PCM32 maps according to supported policy.
- Float32 maps correctly.
- Unsupported format fails clearly.
- Channel mask and valid bits are preserved in diagnostics or metadata.

### PR 004-03: WASAPI Provider Boundary and Endpoint Activation

Objective:

- Add the WASAPI provider/factory boundary and activate the default render endpoint without running the capture loop.

Deliverables:

- `IWasapiAudioClientFactory` or equivalent provider abstraction.
- Default render endpoint lookup.
- Endpoint activation path.
- Mix-format retrieval through the provider.
- RAII ownership for COM/WASAPI objects.
- Local probe or integration test for endpoint format discovery.

Acceptance criteria:

- First-version default render endpoint selection is implemented.
- If no default render endpoint exists, activation returns a structured device-not-found result.
- Endpoint id, friendly name, role, and mix format are captured in diagnostics where available.
- COM and WASAPI objects are owned through RAII wrappers, preferably WIL.
- Partial activation failure releases owned objects.
- No capture thread, packet draining, sample publication, mute/gain processing, or pause behavior is included in this PR.

Suggested tests:

- Fake provider reports default endpoint info.
- No-endpoint fake returns device-not-found.
- Unsupported endpoint role fails validation.
- Local probe logs endpoint id/name/format when hardware is available.
- Partial activation cleanup is verified through fake provider counters.

### PR 004-04: Capture Thread Lifecycle

Objective:

- Add start/stop threading and lifecycle behavior for the audio source using a fake packet provider first.

Deliverables:

- Dedicated capture thread or equivalent worker.
- Start/stop state transitions.
- Prompt stop signal.
- Thread join during teardown.
- Callback disablement before teardown.
- Tests with fake provider.

Acceptance criteria:

- The capture loop runs independently from UI and session control threads.
- `Start` begins loop execution after successful initialization.
- `Stop` signals the loop, joins it, and prevents further callbacks.
- `Stop` is safe after partial initialization failure.
- No sample callback is invoked after `Stop` completes.
- The loop does not call downstream handlers while holding source-state locks.
- No real WASAPI packet draining, buffer pool, mute/gain, or pause routing is included in this PR unless needed by the fake loop test.

Suggested tests:

- Start transitions to running.
- Stop transitions to stopped.
- Stop joins worker thread.
- Stop after partial initialization failure is safe.
- No callbacks after stop.
- Downstream callback is invoked outside internal locks.

### PR 004-05: WASAPI Loopback Packet Draining

Objective:

- Implement the real shared-mode WASAPI loopback packet-draining path.

Deliverables:

- Loopback audio client setup with `AUDCLNT_STREAMFLAGS_LOOPBACK`.
- Event-driven capture path when supported.
- Documented polling fallback or explicit unsupported fallback result.
- Packet drain loop.
- Handling for `AUDCLNT_BUFFERFLAGS_SILENT`.
- Handling for `AUDCLNT_BUFFERFLAGS_DATA_DISCONTINUITY`.
- Local loopback probe.

Acceptance criteria:

- Local probe can start and stop default render endpoint loopback capture.
- The loop drains all available packets before waiting again.
- Silent WASAPI packets are detected.
- Data discontinuity flags increment diagnostics.
- Stop releases `IAudioCaptureClient`, `IAudioClient`, endpoint objects, events, and thread resources in deterministic order.
- No downstream owned-buffer publication beyond a minimal test seam, no mute/gain, and no pause-aware routing is included in this PR.

Suggested tests:

- Fake capture client drains multiple packets in one loop.
- Fake silent packet is recognized.
- Fake discontinuity increments counter.
- Local probe captures packets while system audio is playing.
- Stop releases resources in expected order through test seams.

### PR 004-06: Owned Audio Samples and Buffer Pool

Objective:

- Convert borrowed WASAPI packet memory into owned V2 audio samples.

Deliverables:

- V2 audio sample buffer ownership wrapper.
- Packet copy path.
- Optional buffer pool with safe reuse.
- Sample metadata for frame count, duration, stream id, and media type.
- Tests proving sample lifetime after packet release.

Acceptance criteria:

- WASAPI packet memory is treated as borrowed only until `ReleaseBuffer`.
- Published samples own or share ownership of their buffers.
- No downstream component receives a pointer invalidated by packet release.
- Buffer pool reuse does not occur while downstream still holds a sample.
- Audio sample metadata has a lifetime longer than the sample.
- No mute/gain processing, synthesized silence, pause behavior, or sink writing is included in this PR.

Suggested tests:

- Sample remains valid after fake packet release.
- Retained sample prevents pool buffer reuse.
- Frame count and duration are preserved.
- Media type metadata is immutable for sample lifetime.
- Empty packet handling follows documented policy.

### PR 004-07: Audio Timing and Discontinuity Metadata

Objective:

- Attach source timing metadata and discontinuity diagnostics to audio samples.

Deliverables:

- Source timestamp metadata model.
- Timestamp-source enum or equivalent: WASAPI packet position, QPC, generated continuity, arrival time.
- Packet duration calculation.
- Gap/discontinuity counters.
- Probe logging for packet cadence and timestamp source.

Acceptance criteria:

- Each sample includes frame count, duration, source timestamp metadata, and stream id.
- Timing metadata records where the timestamp came from.
- The core recording clock remains authoritative for output timestamps.
- Muting and gain do not affect timestamps.
- Discontinuities are reported rather than hidden as normal packets.
- No mute/gain processing, pause-aware routing, or synthesized missing-packet silence is included in this PR.

Suggested tests:

- Fake packet position timestamp maps to metadata.
- Fake QPC timestamp maps to metadata.
- Duration is computed from frame count and sample rate.
- Discontinuity flag increments counter.
- Probe logs packet duration and timestamp source.

### PR 004-08: Armed and Muted Semantics

Objective:

- Implement armed-vs-muted behavior and mute gate semantics for an already-producing audio source.

Deliverables:

- Armed-state validation.
- Initial muted-state handling.
- Runtime mute/unmute command path.
- Audio mute gate.
- Silence generation for muted samples using owned buffers.
- Tests for armed/muted combinations.

Acceptance criteria:

- `armed = false` means no WASAPI source is created and no audio output track is planned.
- `armed = false` runtime mute and gain commands return unsupported-operation.
- `armed = true, initiallyMuted = true` creates the source and produces silence until unmuted.
- Muting changes only the mute gate state.
- Unmuting does not rebuild the graph or reconfigure output streams.
- Muted samples preserve original frame count and duration.
- Muted state changes apply only to future samples.
- No pause behavior, gain processing, endpoint missing-packet continuity, or mixer support is included in this PR.

Suggested tests:

- Armed false rejects mute.
- Armed true initially muted emits silence.
- Unmuted samples preserve captured data.
- Muted samples preserve frame count and duration.
- Muted state survives source processing across multiple packets.
- Unmute resumes audible samples without source restart.

### PR 004-09: Pause-Aware Audio Routing

Objective:

- Make pause behavior distinct from mute by dropping paused audio samples and excluding paused time.

Deliverables:

- Pause-state update path.
- Pause-aware routing or drop stage.
- Resume gap-continuity reset.
- Dropped-paused-frames counter.
- Tests for pause/resume behavior.

Acceptance criteria:

- While paused, no audio samples are written downstream.
- Samples captured while paused are dropped before gain, mute, encoding, or sink writing.
- Silence is not generated to fill paused duration.
- Resuming resets gap-continuity state so the pause interval is not reported as an audio dropout.
- Runtime mute and gain commands may be accepted while paused and apply after resume.
- No source gain implementation or endpoint missing-packet continuity is included in this PR.

Suggested tests:

- Pause drops incoming samples.
- Resume allows samples again.
- Pause does not synthesize silence.
- Muted state before pause is active after resume.
- Gain command during pause is stored for later processing once gain exists.
- Dropped paused frames counter increments.

### PR 004-10: Source-Specific Gain Processor

Objective:

- Add source-specific recording gain without changing Windows endpoint or session volume.

Deliverables:

- Gain processor implementation.
- Runtime gain command handling.
- Gain range validation.
- PCM integer gain path for supported formats.
- Float32 gain path for supported formats.
- Clipping diagnostics.
- Processor ordering: format normalizer if needed, gain, mute.

Acceptance criteria:

- Default gain is `0.0 dB`.
- Gain outside supported range returns a validation or range result.
- Gain is applied per source before mute.
- Mute overrides gain.
- Runtime gain changes affect only future samples.
- Windows endpoint volume, app session volume, and hardware volume are not changed.
- Positive gain clipping increments a diagnostic counter or emits a diagnostic event.
- No microphone source, mixer, metering UI, or endpoint hardware gain behavior is included in this PR.

Suggested tests:

- Unity gain leaves samples unchanged.
- Negative gain attenuates samples.
- Positive gain amplifies samples.
- Clipping behavior is deterministic and diagnosed.
- Mute overrides positive gain.
- Endpoint volume provider is not called.

### PR 004-11: Silent-Period Continuity

Objective:

- Handle endpoint silent periods and missing-packet continuity deliberately.

Deliverables:

- Owned silent sample helper for WASAPI silent packets.
- Optional bounded synthesized-silence component for missing packets.
- Metadata marking synthesized silence.
- Silent and synthesized silence counters.
- Tests for silent packet and missing-packet behavior.

Acceptance criteria:

- `AUDCLNT_BUFFERFLAGS_SILENT` produces owned silent samples with packet duration.
- Float silence is `0.0f`.
- Supported PCM silence is zero-filled.
- If missing-packet synthesized silence is implemented, it is bounded by documented policy.
- Synthesized silence is marked in sample metadata.
- Silence is not generated for paused intervals.
- No new gain behavior, mixer behavior, or sink writing behavior is included in this PR.

Suggested tests:

- Silent packet produces zeroed sample.
- Silent sample preserves frame count and duration.
- Reused silent buffers are not mutated while retained downstream.
- Synthesized silence metadata is set when continuity generation is used.
- Paused interval does not generate silence.

### PR 004-12: Thread-Safe Runtime Commands

Objective:

- Harden concurrent start/stop/pause/mute/gain interactions across the capture thread and control thread.

Deliverables:

- Documented runtime state ownership model.
- Atomic or serialized command state for muted, gain, and paused.
- In-flight callback drain or dispatcher integration.
- Stress-style unit tests with fake capture loop.

Acceptance criteria:

- Start, stop, pause-state updates, mute changes, and gain changes are safe across source and control threads.
- Capture thread reads muted and gain state without blocking on long-running control operations.
- Stop prevents new callbacks and waits for in-flight callback dispatch to complete or uses a documented drain mechanism.
- The source does not hold internal locks while invoking processors, sinks, or observer callbacks.
- No new WASAPI behavior or media processing behavior is introduced unless required to close race conditions.

Suggested tests:

- Concurrent mute/gain updates while fake packets arrive are safe.
- Stop during callback drains cleanly.
- Pause/resume during fake packet flow is safe.
- No callback occurs while internal lock is held.
- Race-oriented test repeats command sequences many times without deadlock.

### PR 004-13: Audio Diagnostics and Probe

Objective:

- Make audio source behavior observable through diagnostics and a local Windows probe.

Deliverables:

- Audio diagnostics object.
- Counters for packets, frames, silent packets, synthesized silence, paused drops, discontinuities, clipping, gaps, and failures.
- Local WASAPI loopback probe command or test harness.
- Probe output for endpoint format, buffer duration, packet cadence, discontinuities, silence behavior, and timestamp source.

Acceptance criteria:

- Diagnostics do not require managed callbacks.
- Stop result diagnostics can include discontinuity and clipping counters.
- Errors include component, operation, HRESULT, source id, stream id, and endpoint id where available.
- Probe demonstrates start/stop loopback capture on a local Windows machine.
- Probe can exercise mute, gain, pause/resume, and silent-period behavior where practical.
- No new source behavior beyond instrumentation and probe wiring is included in this PR.

Suggested tests:

- Diagnostics counters increment for fake packets.
- Silent packet counter increments.
- Discontinuity counter increments.
- Clipping counter increments.
- Probe logs endpoint name and format.
- Probe logs packet cadence and timestamp source.

### PR 004-14: Future Microphone and Mixer Shape

Objective:

- Document and lightly codify the extension seam for microphone and mixed audio without implementing either.

Deliverables:

- Interface or documentation updates for future microphone source shape.
- Source-specific control routing notes.
- Mixer input contract notes: normalized sample rate, channel layout, and sample format.
- Tests or compile-time checks proving current system-audio controls are source-specific.

Acceptance criteria:

- Source config distinguishes `SystemAudio` from future `Microphone`.
- Gain and mute remain source-specific before any future mixer stage.
- The first implementation writes one output audio track and does not require a mixer.
- UI volume zero is not treated as equivalent to unarmed.
- Future microphone hardware gain, endpoint mute, echo cancellation, and monitoring remain separate from first-version recording gain.
- No real microphone capture, mixer implementation, or multi-track output is included in this PR.

Suggested tests:

- Source-specific mute command targets only the matching source id.
- Source-specific gain command targets only the matching source id.
- Missing microphone implementation returns unsupported where applicable.
- Current system-audio tests remain unchanged.

## Open Questions

- Should first-version loopback use WASAPI event-driven capture only, or include a polling fallback immediately?
- Should endpoint silent periods with no packets be filled by the source, a dedicated continuity processor, or the sink-facing audio scheduler?
- Should source timestamps prefer WASAPI device position, QPC, or capture-loop arrival time for the first implementation?
- Should synthesized silence be bounded by video clock progress, audio device period, or sink scheduling needs?
- Should gain be exposed to managed callers as dB, scalar, or UI percent mapped by the application layer?
- Should clipping clamp samples, soft-limit samples, or only report diagnostics in the first implementation?
- Should default device changes fail the session, continue on the old endpoint, or trigger a future graph rebuild event?

## Definition of Done

- The V2 WASAPI loopback source can capture system audio from the default render endpoint.
- Audio source and stream identity are stable.
- Endpoint mix format is mapped to V2 audio media type descriptors.
- WASAPI packet data is copied or pooled before downstream publication.
- Muted armed audio emits silence with correct frame count and duration.
- Paused time is excluded from output and not filled with silence.
- Recording gain is source-specific and does not affect Windows endpoint volume.
- Diagnostics report packet timing, silence, discontinuities, clipping, and device failures.
- Unit tests pass without requiring real audio hardware.
- A local Windows probe demonstrates start, stop, mute, gain, pause/resume, and silent-period behavior.
