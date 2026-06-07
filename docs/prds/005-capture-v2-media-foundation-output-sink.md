# PRD 005: CaptureInterop V2 Media Foundation Output Sink

## Status

Draft

## Related Documents

- [CaptureInterop V2 Architecture Plan](../architecture/capture-v2-architecture.md)
- [PRD 001: CaptureInterop V2 Core Pipeline](001-capture-v2-core-pipeline.md)
- [PRD 003: CaptureInterop V2 Desktop Video Source](003-capture-v2-desktop-video-source.md)

## Summary

Build the V2 Windows Media Foundation output sink that writes encoded media files from a core `OutputPlan`. The first production profile is MP4 with H.264 video and optional AAC audio. The design must also model MP3 as a future audio-only output profile so that container and stream compatibility are handled professionally from the start.

This PRD covers Media Foundation sink writer setup, stream negotiation, file/container profiles, D3D video sample handling, PCM audio sample handling, timestamp validation, sink write serialization, lifecycle/finalization rules, diagnostics, and expansion seams. It does not cover source capture, audio input, desktop video capture, managed API exports, or UI.

## Problem

Media Foundation file output is one of the highest-risk pieces of the capture pipeline because object lifetime, stream configuration, timestamps, and finalization rules are unforgiving. A sink writer must know every stream before writing starts, must receive monotonic timestamps, must not outlive Media Foundation shutdown, and must finalize exactly once to produce a valid file.

If the sink is treated as a simple "write frame" helper, future work such as MP3 audio-only output, video-only MP4, configurable bitrate, codec/container negotiation, or clean failure recovery will become brittle. V2 needs a professional sink abstraction that uses Media Foundation internally while honoring the core pipeline's generic output model.

## Goals

- Implement a V2 `MediaFoundationFileSink` that satisfies the core `IOutputSink` contract.
- Support MP4 output with H.264 video.
- Support MP4 output with H.264 video and AAC audio.
- Support MP4 video-only output.
- Model MP3 as an audio-only output profile for future implementation.
- Create only the streams accepted by the selected output profile.
- Negotiate input and output media types from the core `OutputPlan`.
- Serialize all writes into Media Foundation.
- Validate stream ids, media kinds, timestamps, and sample state before writing.
- Finalize exactly once.
- Use WIL and RAII for COM ownership, handles, and staged cleanup.
- Keep Media Foundation runtime lifetime separate from individual sink object destruction.
- Provide synthetic sample integration tests for MP4 output.

## Non-Goals

- Implement desktop video capture.
- Implement WASAPI audio capture.
- Implement managed C# API or P/Invoke.
- Implement UI output settings.
- Implement final HDR tone mapping.
- Implement camera, microphone, or multi-track mixing.
- Implement HEVC, AV1, WAV, or segmented output.
- Implement production MP3 writing in the first milestone.
- Support runtime stream addition after writing begins.

## Users

Primary users:

- Native developers implementing the first V2 MP4 recording workflow.
- Native developers building source and processor components that hand samples to an output sink.

Secondary users:

- Managed facade developers who need predictable native output errors.
- Test authors validating synthetic MP4 output and future MP3 profile behavior.

## Scope

### In Scope

- `MediaFoundationFileSink` implementation.
- Media Foundation runtime lease or lifecycle owner.
- MP4/H.264 video stream configuration.
- Optional MP4/AAC audio stream configuration.
- Video-only MP4 output.
- Output profile capability modeling.
- MP3 audio-only profile modeling and defensive sample rejection.
- Sink writer attribute configuration.
- Input and output media type construction.
- D3D texture sample conversion or wrapping needed for `IMFSinkWriter`.
- PCM audio sample conversion needed for AAC encoder input.
- Sink serialization queue or equivalent single-writer executor.
- Finalize/flush behavior.
- Structured diagnostics and HRESULT reporting.
- Synthetic integration tests.

### Out of Scope

- Real source implementation.
- Real audio device capture.
- Managed config DTOs.
- User-facing output picker.
- Encoder performance tuning beyond basic settings.
- Broad codec capability enumeration.
- Production MP3 file writing unless pulled into a later milestone.

## Output Profiles

Output profiles define which streams a container accepts and how the sink maps core media settings to Media Foundation.

### MP4 H.264/AAC Profile

The first profile is:

```text
Container: MP4
Video: H.264, required when a video stream is present
Audio: AAC, optional when an audio stream is present
Input video: D3D11 texture-backed samples or converted processor output
Input audio: PCM or float samples normalized for AAC encoder input
```

Acceptance criteria:

- The profile accepts one video stream.
- The profile accepts zero or one audio stream for the first implementation.
- The profile rejects unsupported video codecs.
- The profile rejects unsupported audio codecs.
- The profile can produce video-only MP4.
- The profile can produce video-plus-audio MP4.
- The profile does not add streams after `BeginWriting`.

### MP3 Audio-Only Profile

MP3 is modeled now even if production writing is deferred.

Acceptance criteria:

- The profile advertises audio-only capability.
- The profile accepts one audio stream when implemented.
- The profile accepts zero video streams.
- The profile rejects open requests with no audio stream.
- The profile rejects video samples defensively even if graph construction should have pruned them.
- MP3 output plans contain only audio streams.
- If implementation is deferred, opening an MP3 profile returns a clear not-implemented result rather than pretending to support it.
- The MP3 design does not require changes to source capture components.

## Requirements

### Sink Capabilities

The sink must report capabilities to the core pipeline.

Acceptance criteria:

- Capabilities include supported containers.
- Capabilities include supported video codecs per container.
- Capabilities include supported audio codecs per container.
- Capabilities describe whether video is required, optional, or disallowed.
- Capabilities describe whether audio is required, optional, or disallowed.
- Capabilities are testable without creating an actual sink writer.

### Stream Negotiation

The sink must open from an `OutputPlan`, not from ad hoc parameters.

Acceptance criteria:

- `Open` validates all requested streams before creating final writing state.
- `Open` maps each accepted core `StreamId` to one Media Foundation sink stream index.
- `Open` stores the stream map for later sample validation.
- Unsupported stream kinds return a structured failure.
- Unsupported codec/container combinations return a structured failure.
- Duplicate stream ids return a structured failure.
- Missing required media type fields return a structured failure.

### Media Foundation Runtime Lifetime

The sink must not own global Media Foundation lifetime casually.

Acceptance criteria:

- A `MediaFoundationRuntime` or equivalent lifecycle object calls `MFStartup` and `MFShutdown`.
- The sink acquires a runtime lease before creating Media Foundation objects.
- The sink releases all Media Foundation COM objects before releasing its runtime lease.
- `MFShutdown` is not called from an individual sink writer destructor unless the lifecycle owner proves no MF objects remain.
- Multiple sinks or tests can acquire runtime leases without double-startup or premature shutdown.

### MP4 Sink Writer Setup

The sink must configure `IMFSinkWriter` correctly for MP4 output.

Acceptance criteria:

- `MFCreateSinkWriterFromURL` is used for file output.
- The output URL/path is validated before opening.
- Sink writer attributes are configured intentionally, including D3D-related attributes when applicable.
- Output media types are configured before input media types.
- `BeginWriting` is called only after all accepted streams are configured.
- Failure during setup releases partially created COM objects.
- Setup failures include component, operation, stream id where relevant, and HRESULT.

### H.264 Video Stream

The MP4 profile must support H.264 video.

Acceptance criteria:

- Video output media type uses H.264.
- Width and height come from the output plan.
- Frame rate comes from the output plan.
- Bitrate comes from video encoding settings.
- Pixel aspect ratio defaults to 1:1 unless configured otherwise.
- Input media type matches the processor output or D3D texture path chosen for the first implementation.
- Unsupported dimensions, frame rates, or bitrates fail before `BeginWriting`.
- HDR metadata is not fabricated. If HDR metadata propagation is unsupported, diagnostics state that clearly.

### AAC Audio Stream

The MP4 profile must support optional AAC audio.

Acceptance criteria:

- Audio output media type uses AAC.
- Sample rate comes from audio encoding settings or resolved source media type.
- Channel count comes from audio encoding settings or resolved source media type.
- Bitrate comes from audio encoding settings.
- Input media type accepts the normalized audio sample format produced by the pipeline.
- Audio stream setup is skipped when no audio stream exists in the output plan.
- Audio setup failure does not leave a partially writable sink.

### Sample Writing

The sink must write samples only after successful open.

Acceptance criteria:

- `WriteSample` rejects unknown stream ids.
- `WriteSample` rejects samples for stream kinds not accepted by the output profile.
- `WriteSample` rejects calls before `Open`.
- `WriteSample` rejects calls before Media Foundation `BeginWriting` has succeeded.
- `WriteSample` rejects calls after `Finalize` has begun.
- `WriteSample` rejects samples whose media type no longer matches the negotiated stream shape.
- Video sample timestamps are recording-relative and monotonic for the video stream.
- Audio sample timestamps are recording-relative and monotonic for the audio stream.
- Sample duration is set when available.
- Invalid or regressing timestamps return structured failures.
- The sink does not mutate source-owned samples unless the contract explicitly permits it.

### D3D Video Sample Handling

The sink must handle texture-backed video samples safely.

Acceptance criteria:

- The sink retains sample-owned texture references until Media Foundation write completion.
- The sink supports the D3D device strategy established by the Windows pipeline.
- Required texture copies or conversions are explicit and observable in diagnostics.
- Device removal or D3D failure returns a structured sink failure.
- Async write paths cannot outlive the sink or finalized state.

### Audio Sample Handling

The sink must handle audio buffers safely.

Acceptance criteria:

- The sink copies or retains audio sample buffers until Media Foundation write completion.
- Buffer length matches media type block alignment and sample duration.
- Muted/silent samples are treated as normal audio samples.
- Audio discontinuities are recorded in diagnostics when sample metadata indicates a gap.
- Audio samples are never written to video streams.

### Sink Serialization

Media Foundation writes must be serialized.

Acceptance criteria:

- The sink uses one serialization queue, executor, or lock-protected write path.
- `IMFSinkWriter::WriteSample` is not called concurrently from multiple source callback threads.
- Finalize waits for all accepted queued writes to complete or fail.
- Stop/finalize prevents new writes from entering the queue.
- Queue failures propagate to the session stop result.
- Queue depth and dropped/rejected write counters are available in diagnostics.

### Finalization

Finalization must be deterministic.

Acceptance criteria:

- `Finalize` is called at most once.
- `Finalize` returns the Media Foundation finalization HRESULT or mapped result.
- `Finalize` drains or rejects pending work according to a documented policy.
- No samples are written after finalization begins.
- Calling `Finalize` after a completed finalize returns a stable already-finalized result.
- Destruction of an unfinalized sink attempts safe cleanup without throwing.
- A failed `Finalize` still releases owned COM objects.

### Error Handling and Diagnostics

The sink must produce actionable diagnostics.

Acceptance criteria:

- Errors include component, operation, stream id if applicable, HRESULT, and message.
- Diagnostics include output path, selected profile, accepted streams, rejected streams, configured media types, and finalization status.
- Diagnostics include samples written per stream.
- Diagnostics include timestamp validation failures.
- Diagnostics include queue depth high-water mark.
- Diagnostics include Media Foundation setup failures by stage.

## User Stories

### Native Developer: Write Video-Only MP4

As a native developer, I want to create an MP4 sink with one H.264 video stream so that the first desktop-only workflow can produce a playable file.

Acceptance criteria:

- Synthetic video samples produce a valid MP4 file.
- The sink configures exactly one Media Foundation stream.
- Finalization closes the file successfully.

### Native Developer: Write MP4 with Audio

As a native developer, I want to add AAC audio to the MP4 sink so that local audio can be recorded with desktop video.

Acceptance criteria:

- Synthetic video and audio samples produce a valid MP4 file.
- Audio and video stream ids map to different sink stream indexes.
- Audio samples are never written to the video stream and video samples are never written to the audio stream.

### Pipeline Developer: Reject Unsupported Streams

As a pipeline developer, I want the sink to defensively reject unsupported samples so that graph construction bugs fail clearly.

Acceptance criteria:

- MP3 profile rejects video streams and video samples.
- MP3 profile rejects open requests with no audio stream.
- Unknown stream ids fail with a structured error.
- Regressing timestamps fail with a structured error.

### Test Author: Verify Finalization

As a test author, I want finalization behavior to be deterministic so that failed recordings do not leave locked or corrupt files without diagnostics.

Acceptance criteria:

- Finalize is called once under normal stop.
- Finalize is not called concurrently.
- Finalize failure is visible in stop diagnostics.
- Owned COM objects are released after finalization or setup failure.

## Technical Constraints

- C++20.
- Windows implementation.
- No new third-party dependencies.
- Use WIL for COM pointer ownership, handle ownership, HRESULT helpers, and scoped cleanup.
- Use Media Foundation and D3D11; do not introduce external encoder libraries.
- Keep the public core sink contract generic.
- Do not expose `IMFSinkWriter`, `IMFMediaType`, or Media Foundation stream indexes outside the Windows sink implementation.
- Do not depend on managed C# objects.
- Do not add streams after writing begins.

## Proposed Deliverables

- `MediaFoundationRuntime` or equivalent runtime lease owner.
- `MediaFoundationFileSink`.
- MP4/H.264/AAC output profile implementation.
- MP3 audio-only profile model with not-implemented write path if production MP3 writing is deferred.
- Stream map from core `StreamId` to Media Foundation stream index.
- Media type builder helpers for H.264, AAC, and sink input types.
- Sink serialization queue or single-writer executor.
- Timestamp validator per stream.
- Sink diagnostics object.
- Synthetic video sample builder for tests.
- Synthetic audio sample builder for tests.
- MP4 integration tests.

## Testing Requirements

Unit tests must cover:

- Sink capabilities.
- MP4 profile stream acceptance.
- MP3 profile video rejection.
- Stream id to sink stream index mapping.
- Duplicate stream id rejection.
- Unknown stream id rejection.
- Writes before open are rejected.
- Writes after finalize begins are rejected.
- Timestamp monotonicity validation.
- Finalize idempotency.
- Setup failure cleanup with fake Media Foundation adapter where practical.
- Serialization behavior with concurrent write attempts.

Integration tests should cover:

- Synthetic video-only MP4 output.
- Synthetic video-plus-audio MP4 output.
- Finalize produces a readable file.
- Invalid output path failure.
- Unsupported bitrate/frame-rate combinations where deterministic.
- D3D texture input path if available in test environment.
- Audio-only MP4 only if the profile intentionally supports it.

## Targeted PR Chunks

Each chunk should be small enough for one focused pull request. Default execution rule: one chunk equals one PR. Combining adjacent chunks should require an explicit reason, such as a trivial mechanical follow-up with no additional behavior.

### PR 005-01: Output Profile Capabilities

Objective:

- Add profile and capability modeling for MP4 and MP3 without creating Media Foundation objects.

Deliverables:

- `MediaFoundationSinkCapabilities` or equivalent capability model.
- MP4/H.264/AAC profile definition.
- MP3 audio-only profile definition.
- Stream acceptance helpers for profile validation.
- Unit tests for profile capabilities and stream acceptance.

Acceptance criteria:

- MP4 advertises H.264 video support.
- MP4 advertises optional AAC audio support.
- MP4 accepts one video stream and zero or one audio stream.
- MP3 advertises audio-only support.
- MP3 rejects video streams.
- MP3 rejects open plans with no audio stream.
- Capabilities are queryable without `MFStartup`.
- No sink writer creation, media type builders, sample writing, or finalization behavior is included in this PR.

Suggested tests:

- MP4 video-only plan is accepted.
- MP4 video-plus-audio plan is accepted.
- MP4 unsupported codec fails.
- MP3 audio-only plan is accepted at the profile layer.
- MP3 video stream fails.
- MP3 no-audio plan fails.

### PR 005-02: Media Foundation Runtime Lease

Objective:

- Add explicit Media Foundation startup/shutdown ownership that can be shared safely by sink instances and tests.

Deliverables:

- `MediaFoundationRuntime` or equivalent lifecycle owner.
- Runtime lease/acquire-release API.
- Reference-counted or otherwise safe `MFStartup`/`MFShutdown` behavior.
- Tests with multiple runtime leases.

Acceptance criteria:

- First lease initializes Media Foundation.
- Repeated leases do not call startup unsafely.
- Shutdown occurs only after the final lease is released.
- Runtime lease objects are RAII-owned.
- Runtime failures return structured native results.
- No file sink, stream negotiation, media type building, or sample writing is included in this PR.

Suggested tests:

- Single lease acquires and releases successfully.
- Nested or parallel leases keep runtime alive until all are released.
- Failed startup is reported as `ExternalApiFailure` or equivalent.
- Lease release is safe during exception unwinding.

### PR 005-03: Sink Shell and Stream Negotiation

Objective:

- Add the `MediaFoundationFileSink` shell that opens from an `OutputPlan`, validates streams, and builds a stream map without writing files yet.

Deliverables:

- `MediaFoundationFileSink` class.
- `Open` validation using output profile capabilities.
- Core `StreamId` to internal sink stream mapping.
- Unknown/duplicate/unsupported stream rejection.
- Sink state model: created, opened, writing-ready placeholder, finalizing, finalized, failed.
- Unit tests using fake plans.

Acceptance criteria:

- `Open` validates all requested streams before creating writing state.
- Accepted streams are stored in a stream map.
- Duplicate stream ids fail.
- Unknown media kind fails.
- Unsupported codec/container combinations fail.
- Missing required media type fields fail.
- `WriteSample` before a real writer is attached returns a stable not-ready or not-implemented result.
- No `IMFSinkWriter`, media type builders, synthetic MP4 files, or serialization queue is included in this PR.

Suggested tests:

- MP4 video-only plan builds a stream map.
- MP4 video-plus-audio plan maps both streams.
- MP3 video stream fails defensively.
- Duplicate stream ids fail.
- Missing width/height fails.
- Unknown stream lookup fails.

### PR 005-04: MP4 Sink Writer Creation

Objective:

- Create and configure an `IMFSinkWriter` for MP4 output without writing samples.

Deliverables:

- Sink writer creation through `MFCreateSinkWriterFromURL`.
- Output path validation.
- Sink writer attribute builder.
- Runtime lease integration.
- Setup-stage diagnostics.
- Tests for valid and invalid setup where practical.

Acceptance criteria:

- `Open` for an MP4 plan creates an `IMFSinkWriter`.
- Output path failures return structured results.
- Sink writer attributes are configured intentionally.
- Partial setup failure releases created COM objects.
- The sink releases Media Foundation COM objects before releasing the runtime lease.
- No H.264 stream media type, `BeginWriting`, sample writing, or finalization is included in this PR unless required for safe cleanup testing.

Suggested tests:

- Valid temporary MP4 output path creates a sink writer.
- Invalid output path fails with component and operation diagnostics.
- Runtime lease is acquired before sink writer creation.
- Setup failure releases owned objects.

### PR 005-05: H.264 Video Stream Configuration

Objective:

- Configure an MP4 H.264 video stream from an output plan and call `BeginWriting` for video-only plans.

Deliverables:

- H.264 output media type builder.
- Video input media type builder for the first supported input path.
- Video stream index creation.
- `BeginWriting` after all configured streams are added.
- Video stream setup diagnostics.
- Tests for stream setup and unsupported settings.

Acceptance criteria:

- Video output media type uses H.264.
- Width, height, frame rate, and bitrate come from the output plan.
- Pixel aspect ratio defaults to 1:1.
- Output media type is configured before input media type.
- `BeginWriting` is called only after stream setup succeeds.
- Unsupported dimensions, frame rates, or bitrates fail before `BeginWriting`.
- No real sample writing, AAC audio, serialization queue, or MP3 implementation is included in this PR.

Suggested tests:

- Valid H.264 video-only plan reaches writing-ready state.
- Invalid dimensions fail.
- Invalid frame rate fails.
- Invalid bitrate fails.
- Stream map contains the video stream index.
- `BeginWriting` is not called when media type setup fails.

### PR 005-06: Synthetic Video Sample Writing

Objective:

- Write synthetic video samples to a video-only MP4 file.

Deliverables:

- Synthetic video sample builder for tests.
- Video sample conversion/wrapping path for the first supported input media type.
- `WriteSample` implementation for video stream.
- Per-stream timestamp validator for video.
- Video-only MP4 integration test.

Acceptance criteria:

- Synthetic video samples produce a playable MP4 file.
- `WriteSample` rejects unknown stream ids.
- `WriteSample` rejects writes before `Open` and before `BeginWriting`.
- `WriteSample` rejects writes after finalization begins.
- Video timestamps must be recording-relative and monotonic.
- Sample duration is set when available.
- The sink does not mutate source-owned samples unless explicitly allowed by the contract.
- No AAC audio, concurrent write serialization queue, or MP3 implementation is included in this PR.

Suggested tests:

- Write a short synthetic video-only MP4.
- Unknown stream id fails.
- Regressing timestamp fails.
- Write before open fails.
- Write after finalize begins fails.
- Output file exists and is non-empty after finalization helper path.

### PR 005-07: Finalization and Cleanup

Objective:

- Make finalization deterministic for the video-only sink path.

Deliverables:

- `Finalize` implementation.
- Finalize state guard.
- Already-finalized result behavior.
- Destructor safe-cleanup behavior.
- Finalization diagnostics.
- Tests for finalization and cleanup.

Acceptance criteria:

- `Finalize` is called at most once.
- Calling `Finalize` after completed finalization returns a stable already-finalized result.
- No samples are accepted after finalization begins.
- Failed `Finalize` still releases owned COM objects.
- Destroying an unfinalized sink attempts safe cleanup without throwing.
- Runtime lease outlives all Media Foundation COM objects.
- No AAC audio, serialization queue, or MP3 implementation is included in this PR.

Suggested tests:

- Finalize once succeeds for video-only synthetic output.
- Finalize twice returns stable result.
- Write after finalize begins fails.
- COM/runtime cleanup order is verified through test seams where practical.
- Destructor cleanup does not throw.

### PR 005-08: AAC Audio Stream Configuration

Objective:

- Add optional AAC audio stream setup to MP4 plans without writing audio samples yet.

Deliverables:

- AAC output media type builder.
- Audio input media type builder for normalized PCM/float samples.
- Audio stream index creation.
- Audio stream map integration.
- Tests for video-plus-audio setup.

Acceptance criteria:

- Audio output media type uses AAC.
- Sample rate, channel count, and bitrate come from the output plan or resolved source media type.
- Audio stream setup is skipped when no audio stream exists.
- Audio setup failure does not leave a partially writable sink.
- `BeginWriting` occurs only after video and audio stream setup succeeds.
- No audio sample writing, serialization queue, or MP3 implementation is included in this PR.

Suggested tests:

- Video-plus-audio MP4 plan reaches writing-ready state.
- Video-only plan still works.
- Invalid audio sample rate fails.
- Invalid channel count fails.
- Audio and video stream ids map to different sink indexes.

### PR 005-09: Synthetic Audio Sample Writing

Objective:

- Write synthetic audio samples into an MP4 file with video and AAC audio.

Deliverables:

- Synthetic audio sample builder for tests.
- Audio sample buffer conversion/copy path.
- `WriteSample` implementation for audio stream.
- Per-stream timestamp validator for audio.
- Video-plus-audio MP4 integration test.

Acceptance criteria:

- Synthetic video and audio samples produce a playable MP4 file.
- Audio samples are written only to the negotiated audio stream.
- Video samples are written only to the negotiated video stream.
- Muted/silent samples are treated as normal audio samples.
- Audio buffer length is validated against media type block alignment.
- Audio timestamps must be recording-relative and monotonic.
- No concurrent write serialization queue or MP3 implementation is included in this PR.

Suggested tests:

- Write a short synthetic video-plus-audio MP4.
- Audio sample sent to video stream fails.
- Video sample sent to audio stream fails.
- Regressing audio timestamp fails.
- Invalid audio buffer size fails.
- Silent audio sample writes successfully.

### PR 005-10: Sink Write Serialization

Objective:

- Ensure Media Foundation writes are serialized across audio/video source threads.

Deliverables:

- Single-writer executor, queue, or lock-protected write path.
- Queue close behavior during finalize.
- Pending-write drain behavior.
- Queue failure propagation.
- Queue diagnostics counters.
- Concurrent write tests.

Acceptance criteria:

- `IMFSinkWriter::WriteSample` is not called concurrently.
- Finalize waits for accepted queued writes to complete or fail.
- Finalize prevents new writes from entering the queue.
- Queue failures propagate to the session stop result or sink result.
- Queue depth high-water mark is recorded.
- The queue does not allow async write paths to outlive sink finalization.
- No new media type behavior or MP3 implementation is included in this PR.

Suggested tests:

- Concurrent video/audio write attempts are serialized.
- Finalize drains accepted writes.
- Write submitted after queue close fails.
- Simulated queue failure appears in diagnostics.
- Queue depth counter updates.

### PR 005-11: Sink Diagnostics and Error Detail

Objective:

- Make setup, write, queue, timestamp, and finalize failures observable.

Deliverables:

- Sink diagnostics object.
- Structured error helper for component/operation/stream id/HRESULT/message.
- Samples-written counters per stream.
- Timestamp failure counters.
- Rejected-write counters.
- Setup/finalize stage reporting.
- Tests for diagnostics population.

Acceptance criteria:

- Diagnostics include output path and selected profile.
- Diagnostics include accepted and rejected streams.
- Diagnostics include configured media type summaries.
- Diagnostics include samples written per stream.
- Diagnostics include timestamp validation failures.
- Diagnostics include queue depth high-water mark.
- Setup failures identify stage and HRESULT.
- Finalize failures identify stage and HRESULT.
- No new sink behavior beyond instrumentation is included in this PR.

Suggested tests:

- Successful video-only output records samples-written counters.
- Unknown stream rejection increments rejected-write diagnostics.
- Timestamp failure increments timestamp diagnostics.
- Invalid setup records setup stage.
- Finalize failure test seam records finalize stage.

### PR 005-12: MP3 Expansion Readiness

Objective:

- Finish MP3 audio-only readiness without requiring production MP3 output unless explicitly chosen.

Deliverables:

- MP3 profile open behavior.
- Not-implemented result for production MP3 writing, or prototype `MediaFoundationMp3FileSink` if investigation proves it is small.
- Defensive video sample rejection in MP3 profile path.
- MP3 diagnostics and documentation note.
- Tests proving no video can reach MP3 output.

Acceptance criteria:

- MP3 output plan cannot write video.
- MP3 open with no audio stream fails.
- MP3 open with audio stream either succeeds in a prototype path or returns explicit not-implemented.
- MP3 `WriteSample` rejects video samples defensively.
- No desktop video or audio source contract changes are required.
- No MP4 behavior changes are introduced except shared profile infrastructure if needed.

Suggested tests:

- MP3 audio-only plan reaches not-implemented or prototype-open result.
- MP3 video stream fails.
- MP3 no-audio stream fails.
- MP3 video sample write fails defensively.
- MP4 video-only and video-plus-audio tests still pass.

### PR 005-13: Encoder Settings and Hardware Preference Follow-Up

Objective:

- Add first-pass encoder setting support beyond required basics only after MP4 output is stable.

Deliverables:

- Supported H.264 bitrate/frame-rate validation refinements.
- Optional GOP length setting if Media Foundation support is reliable.
- Hardware acceleration preference mapping if the chosen MF path supports it clearly.
- Diagnostics describing which settings were applied or ignored.
- Tests for accepted and rejected settings.

Acceptance criteria:

- Existing MP4 output tests continue to pass.
- Unsupported settings fail or are reported as ignored according to documented policy.
- Hardware preference does not silently claim success when not applied.
- This PR does not introduce new codecs such as HEVC or AV1.

Suggested tests:

- Valid bitrate/frame-rate settings apply.
- Unsupported bitrate/frame-rate combinations fail where deterministic.
- Optional GOP setting maps or reports unsupported.
- Hardware preference diagnostics are populated.

## Open Questions

- Should the MP4 sink support audio-only MP4 in the first implementation, or only video-only and video-plus-audio?
- Should the sink queue preserve strict global timestamp ordering across streams, or only serialize calls while preserving per-stream monotonicity?
- Which H.264 encoder attributes should be configurable in the first implementation beyond bitrate and frame rate?
- Should hardware encoder preference be controlled through sink writer attributes, transform attributes, or deferred capability enumeration?
- Should failed `Finalize` keep the output file for diagnostics or delete incomplete output through a higher-level workflow policy?
- Should production MP3 use a separate `MediaFoundationMp3FileSink` class or remain a profile of `MediaFoundationFileSink`?

## Definition of Done

- MP4/H.264 video-only output works with synthetic samples.
- MP4/H.264/AAC output works with synthetic video and audio samples.
- Sink capabilities and profile rules are testable without real capture sources.
- Stream negotiation happens before writing begins.
- All writes into Media Foundation are serialized.
- Timestamps are validated per stream.
- Finalize is called at most once and releases owned Media Foundation objects.
- Errors and diagnostics identify setup, write, queue, and finalize failures clearly.
- MP3 is represented as an audio-only profile with defensive video rejection, even if production MP3 writing is deferred.
