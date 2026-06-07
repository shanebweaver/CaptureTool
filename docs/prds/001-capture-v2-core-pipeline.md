# PRD 001: CaptureInterop V2 Core Pipeline

## Status

Draft

## Related Documents

- [CaptureInterop V2 Architecture Plan](../architecture/capture-v2-architecture.md)

## Summary

Build the CaptureInterop V2 native core pipeline foundation. This PRD covers the platform-neutral C++ concepts and orchestration needed to compose media sources, processors, clocks, output planning, state transitions, ownership, and diagnostics. It does not implement Windows Graphics Capture, WASAPI, Media Foundation file output, or the managed C# API. Those are separate PRDs that plug into this core.

The first user-facing workflow will eventually be desktop video recording to MP4 with optional local audio, pause/resume, audio mute, and source volume control. This PRD creates the internal architecture that makes that workflow possible without hard-coding it into one recorder class.

## Problem

The current capture work can become difficult to extend if source capture, encoding, file writing, timing, pause/resume behavior, and managed interop are coupled together. Professional media capture requires explicit handling of:

- Media stream identity.
- Source and track capabilities.
- Recording-relative timestamps.
- Pause/resume clock behavior.
- Audio mute and gain semantics.
- Container and codec compatibility.
- Deterministic object ownership and teardown.
- Testable graph behavior without real Windows devices.

Without a core pipeline layer, adding MP3 output, microphone input, HDR processing, multiple sources, or additional containers risks repeating special-case logic across native and managed boundaries.

## Goals

- Define stable C++ value objects for source identity, stream identity, media kinds, media types, codec requests, container requests, timestamps, and durations.
- Define core interfaces for media sources, media processors, output sinks, clocks, output profile resolution, and pipeline sessions.
- Implement a reusable `CapturePipelineSession` that owns one recording graph and coordinates lifecycle.
- Implement a recording clock that excludes paused time from output timestamps.
- Implement validation for supported and unsupported source/output combinations.
- Implement output planning that can model MP4 video/audio and MP3 audio-only behavior without requiring Media Foundation.
- Implement audio control concepts in the core: armed, muted, and gain.
- Define deterministic ownership and teardown rules for session-owned graph objects.
- Provide fake sources, fake processors, and null sinks for unit tests.
- Keep Windows API details out of the core.

## Non-Goals

- Implement Windows Graphics Capture or DXGI Desktop Duplication.
- Implement WASAPI loopback capture.
- Implement Media Foundation sink writer output.
- Implement H.264, AAC, or MP3 encoding.
- Implement C ABI exports or C# facade types.
- Implement HDR tone mapping.
- Implement UI behavior.
- Replace the current recorder in production.

## Users

Primary users:

- Native capture developers implementing V2 sources, processors, and sinks.
- Managed infrastructure developers building the C# facade on top of the native API.

Secondary users:

- Application developers wiring the first workflow behind a feature flag.
- Test authors validating media behavior without real capture devices.

## Scope

### In Scope

- C++20 core value objects and interfaces.
- `CapturePipelineConfig` and source/output config validation.
- `OutputProfileResolver` and `OutputPlan`.
- `CapturePipelineSession` lifecycle orchestration.
- `RecordingClock` pause/resume behavior.
- Pipeline state machine.
- Audio gain and mute processor contracts.
- Source and stream id model.
- Ownership model for graph objects.
- Diagnostics and error objects.
- Unit tests for core behavior.
- Fake and null components for tests.

### Out of Scope

- Real device capture.
- Real file writing.
- Real encoder negotiation.
- Managed P/Invoke.
- App feature flag integration.
- Performance tuning for real-time capture.

## Requirements

### Core Value Objects

The core must define value objects for:

- `SourceId`.
- `StreamId`.
- `SourceKind`.
- `MediaKind`.
- `MediaTime`.
- `MediaDuration`.
- `Rational`.
- `VideoMediaType`.
- `AudioMediaType`.
- `VideoEncodingSettings`.
- `AudioEncodingSettings`.
- `OutputSettings`.
- `ToneMappingSettings`.
- `AudioGainSettings`.

Acceptance criteria:

- Value objects are copyable, cheap to pass by value where appropriate, and independent of Windows headers unless a transitional config field explicitly requires one.
- Media time is represented internally in 100 nanosecond ticks.
- Source and stream ids are explicit and can target runtime controls.

### Configuration

The core must support a `CapturePipelineConfig` that expresses capture intent:

- One or more source configs.
- Output container and encoding settings.
- Initial audio mute state.
- Initial audio gain.
- Tone mapping policy placeholder.
- Diagnostics options placeholder.

Acceptance criteria:

- Validation rejects duplicate source ids.
- Validation rejects missing output path.
- Validation rejects missing output streams.
- Validation rejects MP3 output when a required audio stream is absent.
- Validation rejects MP3 output plans that explicitly require video writing.
- Validation prunes incidental video streams from MP3 output plans when an audio stream is available and no preview sink is requested.
- Validation allows MP4 with video-only output.
- Validation allows MP4 with video and armed audio output.
- Validation rejects audio gain outside the supported range.

### Output Profile Resolution

The core must convert requested configuration into an `OutputPlan`.

Acceptance criteria:

- MP4 output can plan a video stream using H.264.
- MP4 output can plan an audio stream using AAC when audio is armed.
- MP3 output is represented as audio-only.
- MP3 output with audio and incidental video sources produces an audio-only output plan.
- MP3 output with an explicit video output requirement returns a structured validation failure.
- Unsupported source/container/codec combinations return a structured validation failure.
- Output planning does not instantiate Media Foundation objects.

### Pipeline Interfaces

The core must define interfaces for:

- `IMediaSource`.
- `IVideoCaptureSource`.
- `IAudioCaptureSource`.
- `IMediaProcessor`.
- `IOutputSink`.
- `IRecordingClock`.
- Factories for sources, processors, and sinks where useful.

Acceptance criteria:

- Interfaces use `HRESULT` or core result objects for failure, not exceptions as the contract.
- Interfaces do not expose Media Foundation, WASAPI, or Windows Graphics Capture types.
- Interfaces support fake implementations without Windows devices.
- Interfaces document ownership of callbacks, samples, and buffers.

### Pipeline Session Lifecycle

The core must implement `CapturePipelineSession` as the owner of a single recording graph.

Acceptance criteria:

- A session can be started once.
- A finalized or failed session cannot be restarted.
- A recorder or future owner can create a new session for a new recording.
- `Stop` is idempotent or returns a stable already-stopped result.
- Session teardown reports the first meaningful failure and the stage where it happened.
- Session teardown releases graph objects in a deterministic order.

### State Machine

The session must implement this state model:

```text
Created
  -> Prepared
  -> Recording
  -> Paused
  -> Recording
  -> Stopping
  -> Finalized

Any active state
  -> Failing
  -> Failed
```

Acceptance criteria:

- `Start` is valid only from `Created`.
- `Pause` is valid only from `Recording`.
- `Resume` is valid only from `Paused`.
- `SetAudioMuted` is valid from `Recording` and `Paused`.
- `SetAudioGain` is valid from `Recording` and `Paused`.
- `Stop` is valid from `Recording` and `Paused`.
- Invalid transitions return stable operation results.

### Recording Clock

The core must implement a clock that produces recording-relative timestamps.

Acceptance criteria:

- Recording time starts at zero.
- Paused wall-clock duration is excluded from output timestamps.
- The clock remains monotonic after pause/resume.
- Muting audio does not affect recording time.
- Changing audio gain does not affect recording time.
- Unit tests can inject deterministic time.

### Audio Controls

The core must model audio controls independently:

- `armed`: whether the audio source and track exist.
- `muted`: whether samples become silence.
- `gain`: source-specific recording volume.

Acceptance criteria:

- Muting an armed audio source produces silence while preserving sample duration and timestamps.
- Muting an unarmed or missing source returns a stable unsupported-operation result.
- Gain changes target a source id.
- Gain changes do not alter system endpoint volume.
- Gain changes apply to future samples.
- Gain defaults to `0.0 dB`.
- Gain outside `-60.0 dB` to `+12.0 dB` returns a range validation failure.
- The core exposes a processor or processor contract for gain and mute behavior.

### Ownership and Teardown

The core must encode single ownership for graph objects.

Acceptance criteria:

- The session owns sources, processors, sink, clock, and worker/control state.
- Session-owned graph components are held by `std::unique_ptr` or equivalent single-owner wrappers.
- Callback registrations use RAII tokens.
- Source callbacks are disabled or invalidated before source destruction.
- No graph component depends on managed object lifetime.
- Tests verify teardown ordering for normal stop and failure stop.

### Diagnostics

The core must provide structured diagnostics that future native API and managed layers can surface.

Acceptance criteria:

- Errors include a code, component, operation, HRESULT or equivalent native status, and human-readable message.
- Stop results include final state and failure stage if applicable.
- Validation results include errors and warnings.
- Pipeline counters can represent dropped video frames, audio discontinuities, late samples, and unsupported commands, even if not all counters are populated in this PRD.

## User Stories

### Native Developer: Compose a Pipeline

As a native developer, I want to compose fake sources, processors, and sinks through `CapturePipelineSession` so that I can test graph behavior before implementing Windows components.

Acceptance criteria:

- A test can configure one fake video source and one null sink.
- The session can start, process samples, pause, resume, and stop.
- The null sink receives recording-relative timestamps.

### Native Developer: Validate Output Compatibility

As a native developer, I want output planning to reject incompatible output requests so that sink implementations do not need to guess what the user meant.

Acceptance criteria:

- MP3 with audio and incidental video sources produces an audio-only output plan.
- MP3 with video-only sources is rejected.
- MP4 with video-only output is accepted.
- MP4 with video and armed audio is accepted.

### Managed Developer: Target Audio Controls by Source

As a managed developer, I want native source ids in the core model so that future C# commands can mute or adjust volume for a specific audio source.

Acceptance criteria:

- Source ids are stable for the life of a session.
- Runtime audio commands target a source id.
- Missing source ids return a clear result.

## Technical Constraints

- C++20.
- No new third-party dependencies.
- WIL should be preferred where the core touches handles or HRESULT helper patterns, although most core code should not require COM or Win32 ownership.
- The core should be buildable and testable without real capture devices.
- The core must not depend on C# types.
- The core must not depend on Media Foundation, WASAPI, or Windows Graphics Capture headers unless explicitly isolated behind an infrastructure boundary.

## Proposed Deliverables

- `CaptureInterop::V2` core namespace.
- Core media value object headers.
- Core configuration and validation classes.
- Output profile resolver.
- Recording clock with injectable time provider.
- Pipeline state machine.
- Capture pipeline session orchestration.
- Audio gain and mute processor contracts, with simple fake/test implementation.
- Fake media source and null output sink for tests.
- Core unit test suite.

## PR-Sized Work Chunks

These chunks are the recommended implementation split for PRD 001. Each chunk should be small enough for one targeted pull request and should compile with focused tests before the next chunk starts. Later PRDs can depend on these chunks, but each PR here should stay inside the V2 core boundary and avoid real Windows capture, Media Foundation output, C ABI exports, or managed facade work.

### PR 001-A: Core Namespace and Primitive Value Objects

Scope:

- Add the initial V2 core namespace and folder boundary.
- Add source, stream, media, and time primitives:
  - `SourceId`.
  - `StreamId`.
  - `SourceKind`.
  - `MediaKind`.
  - `MediaTime`.
  - `MediaDuration`.
  - `Rational`.
- Add simple equality, default construction, and validity helpers where useful.
- Add unit tests for defaults, equality, and basic validity.

Acceptance criteria:

- The V2 core namespace compiles without Windows capture, WASAPI, Media Foundation, or C# dependencies.
- Media time is represented in 100 nanosecond ticks.
- Source and stream ids are strongly typed or otherwise cannot be casually confused with raw unrelated integers in core code.
- Tests cover id equality, invalid/default ids, rational validity, and time/duration arithmetic needed by later chunks.

Out of scope for this PR:

- Pipeline config.
- Result or validation framework.
- Source, processor, sink, or session interfaces.

### PR 001-B: Core Result and Diagnostics Types

Scope:

- Add core operation result types.
- Add stable core result codes for success, validation failure, invalid state, unsupported operation, not found, native failure placeholder, and range errors.
- Add diagnostic error objects with code, component, operation, optional native status, and message.
- Add validation result objects that can carry errors and warnings.
- Add teardown/failure stage enums used by later session and stop results.
- Add unit tests for result construction and diagnostic aggregation.

Acceptance criteria:

- Core code can represent success, single failure, and validation failures without throwing as the public contract.
- Diagnostics can be inspected in tests without parsing strings.
- Result types are independent of Media Foundation, WASAPI, Windows Graphics Capture, and managed types.
- Tests cover success, failure, warnings, multiple validation errors, and stable result-code values.

Out of scope for this PR:

- Config validation logic.
- Session lifecycle.
- Exported C ABI result codes.

### PR 001-C: Media Types and Output Request Objects

Scope:

- Add media and output request value objects:
  - `VideoMediaType`.
  - `AudioMediaType`.
  - `VideoPixelFormat` placeholder enum.
  - `AudioSampleFormat`.
  - `VideoCodec`.
  - `AudioCodec`.
  - `ContainerFormat`.
  - `VideoEncodingSettings`.
  - `AudioEncodingSettings`.
  - `OutputSettings`.
  - `ToneMappingSettings`.
  - `AudioGainSettings`.
- Add clear defaults for gain and placeholder tone mapping.
- Add unit tests for defaults and basic validation helpers.

Acceptance criteria:

- MP4, MP3, and WAV can be represented as requested container formats.
- H.264, AAC, MP3, and PCM can be represented as requested codecs.
- Audio gain defaults to `0.0 dB` and exposes the supported range used by validation.
- Media type objects remain platform-neutral and do not include WASAPI, Media Foundation, D3D, DXGI, or WinRT types.

Out of scope for this PR:

- Output compatibility resolution.
- Encoder negotiation.
- Format conversion.

### PR 001-D: Capture Pipeline Config Model

Scope:

- Add platform-neutral `CapturePipelineConfig`.
- Add source config variants or tagged config objects for:
  - Desktop video source intent.
  - System audio source intent.
- Add control config for initial mute and initial gain.
- Add diagnostics config placeholder.
- Add config builder or helper constructors for tests if useful.
- Add unit tests for constructing common first-slice configs.

Acceptance criteria:

- Config can express video-only MP4 output.
- Config can express MP4 output with armed system audio.
- Config can express MP3 output with a system audio source and incidental video source.
- Config distinguishes `armed` from `initiallyMuted`.
- Config can target runtime audio controls by source id.
- Config remains independent of concrete Windows capture implementation types.

Out of scope for this PR:

- Validating all config combinations.
- Resolving output plans.
- Building or starting a pipeline session.

### PR 001-E: Config Validation

Scope:

- Add a core config validator.
- Validate duplicate source ids.
- Validate missing output path.
- Validate missing requested output streams.
- Validate invalid enum or placeholder values where represented in core.
- Validate audio gain range.
- Validate basic armed/muted consistency.
- Add unit tests for success and failure cases.

Acceptance criteria:

- Duplicate source ids are rejected.
- Missing output path is rejected.
- Missing output streams are rejected.
- Audio gain outside `-60.0 dB` to `+12.0 dB` is rejected.
- Muting an unarmed source is represented as unsupported or invalid according to the chosen config model.
- Validation returns structured errors with stable result codes and component/operation names.

Out of scope for this PR:

- MP4/MP3 stream compatibility planning.
- Real device, codec, file-system, or OS capability checks.

### PR 001-F: Output Profile Resolver

Scope:

- Add `OutputProfileResolver`.
- Add `OutputPlan` and output stream plan objects.
- Resolve MP4 video-only output.
- Resolve MP4 video plus armed AAC audio output.
- Resolve MP3 audio-only output.
- Prune incidental video streams from MP3 output when audio is available and video output is not explicitly required.
- Add unit tests for supported and unsupported combinations.

Acceptance criteria:

- MP4 output can plan H.264 video.
- MP4 output can plan AAC audio when audio is armed.
- MP3 output produces an audio-only plan.
- MP3 with video-only sources is rejected.
- MP3 with audio and incidental video produces an audio-only plan.
- Unsupported source/container/codec combinations return structured validation failures.
- No Media Foundation objects are instantiated.

Out of scope for this PR:

- Encoder capability negotiation.
- Sink writer construction.
- Session graph construction.

### PR 001-G: Recording Clock

Scope:

- Add `IRecordingClock`.
- Add injectable time provider or deterministic clock dependency.
- Implement recording-relative time starting at zero.
- Implement pause/resume duration exclusion.
- Add monotonic timestamp behavior.
- Add unit tests with deterministic time.

Acceptance criteria:

- Recording time starts at zero.
- Paused wall-clock time is excluded.
- Time remains monotonic after pause/resume.
- Muting and gain changes have no clock side effects.
- Tests cover start, elapsed time, pause, resume, multiple pauses, and edge cases around repeated pause/resume calls.

Out of scope for this PR:

- Pipeline state machine.
- Source timestamp normalization.
- Sink writes.

### PR 001-H: Pipeline State Machine

Scope:

- Add session state enum and transition helper.
- Model:

```text
Created -> Prepared -> Recording -> Paused -> Recording -> Stopping -> Finalized
Any active state -> Failing -> Failed
```

- Add operation names for start, pause, resume, stop, set muted, and set gain.
- Add unit tests for valid and invalid transitions.

Acceptance criteria:

- `Start` is valid only from `Created`.
- `Pause` is valid only from `Recording`.
- `Resume` is valid only from `Paused`.
- `SetAudioMuted` and `SetAudioGain` are valid from `Recording` and `Paused`.
- `Stop` is valid from `Recording` and `Paused`.
- Invalid transitions return stable operation results.

Out of scope for this PR:

- Actual graph ownership.
- Clock integration.
- Runtime audio processing.

### PR 001-I: Pipeline Interfaces and Sample Contracts

Scope:

- Add core interfaces:
  - `IMediaSource`.
  - `IVideoCaptureSource`.
  - `IAudioCaptureSource`.
  - `IMediaProcessor`.
  - `IOutputSink`.
  - Factory interfaces only where needed by the session.
- Add platform-neutral video and audio sample contracts.
- Add callback or sample handler ownership documentation in code comments.
- Add callback token or registration abstraction if the interfaces require callbacks.
- Add compile-focused tests with simple fake implementations.

Acceptance criteria:

- Interfaces do not expose Media Foundation, WASAPI, Windows Graphics Capture, D3D, DXGI, WinRT, or managed types.
- Interfaces use core result objects or `HRESULT` consistently as the contract selected for V2 core.
- Sample contracts document whether buffers are owned, borrowed, or shared.
- Fake implementations can compile and satisfy each interface.

Out of scope for this PR:

- Session orchestration.
- Real sources or sinks.
- Buffer pooling implementation.

### PR 001-J: Fake Components and Null Sink

Scope:

- Add fake video source.
- Add fake audio source.
- Add fake processor or pass-through processor.
- Add null output sink.
- Add deterministic sample builders for tests.
- Add tests proving fakes can emit and receive samples without real devices.

Acceptance criteria:

- Fake sources can emit controlled timestamps and sample payloads.
- Null sink records received samples for assertions.
- Fake components can simulate start, stop, and failure.
- Test components are reusable by later session, pause, teardown, and audio-control tests.

Out of scope for this PR:

- `CapturePipelineSession`.
- Real device capture.
- Real file writing.

### PR 001-K: Session Ownership and Start/Stop Skeleton

Scope:

- Add `CapturePipelineSession`.
- Wire session construction from validated config and injected fake/test factories.
- Own sources, processors, sink, clock, and session state through single-owner wrappers.
- Implement start and stop lifecycle without full sample routing.
- Add deterministic teardown order and teardown-stage reporting.
- Add tests for start once, stop, stop idempotency or stable already-stopped result, and teardown order.

Acceptance criteria:

- A session can be started once.
- A finalized or failed session cannot be restarted.
- Stop transitions through stopping to finalized or failed.
- Session-owned graph objects are released in deterministic order.
- Teardown reports first meaningful failure and the stage where it occurred.
- Source callbacks are disabled or invalidated before source destruction.

Out of scope for this PR:

- Full sample routing through processors to sink.
- Pause/resume sample behavior.
- Audio gain or mute processing.

### PR 001-L: Session Sample Routing and Pause/Resume

Scope:

- Route fake source samples through processors to the sink.
- Apply recording clock timestamps or timestamp normalization policy.
- Implement pause/resume behavior in the session.
- Drop or ignore source samples while paused according to the architecture rules.
- Add tests for fake video and audio routing, pause, resume, and recording-relative timestamps.

Acceptance criteria:

- A fake graph can start, route samples, pause, resume, and stop.
- Samples written to the null sink use recording-relative timestamps.
- Samples produced during pause are not written to the sink.
- The first sample after resume uses the next recording-relative timestamp.
- Muting and gain state changes do not affect clock behavior.

Out of scope for this PR:

- Real capture sources.
- Real output sinks.
- Audio silence generation.

### PR 001-M: Audio Control Processor Contracts

Scope:

- Add core contracts for audio gain and mute processing.
- Add simple test implementations:
  - Gain processor.
  - Mute gate.
  - Silence sample generation helper where needed.
- Add runtime command routing by source id.
- Add tests for armed, muted, gain, missing source, and range behavior.

Acceptance criteria:

- Muting an armed audio source produces silence while preserving duration and timestamps.
- Muting an unarmed or missing source returns stable unsupported-operation or not-found result.
- Gain changes target a source id.
- Gain changes apply to future samples only.
- Gain does not alter any endpoint or system volume concept.
- Gain outside the supported range returns a range validation failure.

Out of scope for this PR:

- WASAPI loopback capture.
- Microphone capture.
- Mixer implementation.
- Managed runtime command facade.

### PR 001-N: Session Diagnostics and Counters

Scope:

- Integrate diagnostics into `CapturePipelineSession`.
- Add stop result object with final state and failure stage.
- Add counters for dropped video frames, audio discontinuities, late samples, unsupported commands, and validation warnings where available.
- Add tests for diagnostics on normal stop, invalid command, validation failure, and teardown failure.

Acceptance criteria:

- Stop results include final state and failure stage when applicable.
- Invalid runtime commands produce structured diagnostics.
- Session counters can be queried without relying on callbacks.
- Diagnostics are stable enough for PRD 002 native API mapping.

Out of scope for this PR:

- Populating counters from real Windows sources or Media Foundation sinks.
- Managed exception mapping.
- Telemetry upload or UI logging.

### Recommended Sequence

Implement these chunks in order:

```text
001-A -> 001-B -> 001-C -> 001-D -> 001-E -> 001-F
      -> 001-G -> 001-H -> 001-I -> 001-J -> 001-K
      -> 001-L -> 001-M -> 001-N
```

The order keeps each PR reviewable: primitives first, then config/planning, then time/state, then interfaces/fakes, then session behavior, then audio controls and diagnostics. A later PR may combine adjacent chunks only if both chunks are already very small after implementation, but the default expectation is one chunk per PR.

## Testing Requirements

Core tests must cover:

- Value object defaults and equality where applicable.
- Config validation success and failure paths.
- Duplicate source id detection.
- MP4 output planning for video-only and video-plus-audio.
- MP3 audio-only output planning.
- State transition validity.
- Stop idempotency or stable already-stopped result.
- Pause/resume timestamp behavior.
- Audio mute silence generation.
- Audio gain range rejection.
- Source-specific audio command routing.
- Teardown order on success.
- Teardown order when a source, processor, or sink fails.

## Milestones

### Milestone 1: Core Model

- Add V2 core namespace/folder.
- Add value objects.
- Add config objects.
- Add validation result and error result types.

Exit criteria:

- Core model compiles.
- Basic validation tests pass.

### Milestone 2: Planning and State

- Add output profile resolver.
- Add state machine.
- Add recording clock.

Exit criteria:

- Output planning tests pass.
- Pause/resume clock tests pass.
- Invalid transition tests pass.

### Milestone 3: Session and Test Graph

- Add pipeline session.
- Add fake sources.
- Add fake processors.
- Add null sink.
- Add teardown-stage reporting.

Exit criteria:

- A fake end-to-end graph can start, pause, resume, route samples, and stop.
- Teardown tests pass.

### Milestone 4: Audio Control Contracts

- Add audio gain and mute processor contracts.
- Add test implementation for gain/mute processing.
- Add runtime source-targeted command handling.

Exit criteria:

- Muted audio preserves duration and timestamp.
- Gain commands target the correct source.
- Missing source commands return stable errors.

## Open Questions

- Should `CapturePipelineSession` own a serialized executor in the core PRD, or should threading be deferred until source/sink PRDs?
- Should the core result type wrap HRESULT everywhere, or use an app-level result code with optional HRESULT?
- Should tone mapping policy validation be a placeholder only, or should it reject unsupported HDR requests in the first core implementation?
- Should preview be modeled as a sink in the first core implementation, or deferred until the workflow PRD?

## Definition of Done

- The core PRD scope is implemented in C++.
- Core tests pass without real Windows capture devices.
- The first workflow PRD can depend on this core without needing to redesign config, state, ownership, or timing.
- The architecture document is updated if any accepted implementation decision changes the planned architecture.
