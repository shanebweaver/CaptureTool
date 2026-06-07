# PRD 003: CaptureInterop V2 Desktop Video Source

## Status

Draft

## Related Documents

- [CaptureInterop V2 Architecture Plan](../architecture/capture-v2-architecture.md)
- [PRD 001: CaptureInterop V2 Core Pipeline](001-capture-v2-core-pipeline.md)

## Summary

Build the V2 Windows desktop video source for local monitor and region capture. This PRD covers the source-side implementation that produces D3D-backed video samples for the V2 core pipeline. It includes monitor targeting, physical-pixel region capture, source identity, D3D texture ownership, frame metadata, frame timing, basic video processing contracts, and HDR placeholder behavior.

This PRD does not cover audio capture, Media Foundation output, encoding, MP4 writing, the C ABI, or the C# managed facade. It produces video samples that downstream processors and sinks can consume.

## Problem

Desktop video capture is deceptively easy to start and hard to make professional. A robust source must do more than emit textures. It must establish stable source identity, report media type and frame metadata, preserve texture lifetime until downstream work is finished, handle monitor and region configuration, expose timing consistently, and leave room for HDR handling without baking color assumptions into the source.

If the desktop source is implemented as a one-off recorder detail, future work such as region capture, HDR tone mapping, preview rendering, multiple capture sources, or alternative Windows capture APIs will become unnecessarily expensive.

## Goals

- Implement a V2 `WindowsDesktopVideoSource` that satisfies the core `IVideoCaptureSource` contract.
- Capture a selected local desktop monitor.
- Support optional monitor-relative physical-pixel region capture.
- Produce D3D11 texture-backed video samples with explicit ownership.
- Report stable `SourceId` and `StreamId` values for the life of the session.
- Report `VideoMediaType` and frame metadata before samples reach processors or sinks.
- Produce source timestamps or frame-arrival metadata that the core recording clock can normalize.
- Support deterministic start, stop, and teardown behavior.
- Provide basic video processing hooks for crop/scale/format conversion.
- Detect or record source color information where available.
- Resolve HDR policy to a safe placeholder behavior for the first implementation.
- Provide fake/probe tests and integration coverage for source start/stop and sample emission.

## Non-Goals

- Implement audio capture.
- Implement Media Foundation encoding or sink writing.
- Implement final HDR tone mapping.
- Implement camera or window-specific capture.
- Implement managed C# configuration or P/Invoke.
- Implement UI monitor selection.
- Implement full capability enumeration for every display configuration.
- Implement production multi-monitor simultaneous capture.

## Users

Primary users:

- Native developers implementing the first V2 desktop recording workflow.
- Native developers implementing video processors and output sinks that consume D3D textures.

Secondary users:

- Managed infrastructure developers that need stable source and stream identity.
- Test authors building probe tools for monitor, frame timing, and color behavior.

## Scope

### In Scope

- Windows desktop/monitor video source implementation.
- Monitor handle or monitor identity binding.
- Optional physical-pixel capture region.
- D3D11 device/resource ownership rules.
- Video sample object shape for texture-backed frames.
- Frame timing metadata.
- Source and stream descriptors.
- Cursor capture policy placeholder.
- Dirty region metadata placeholder if supported by the chosen API.
- HDR/color metadata placeholder.
- Basic crop/scale/format processor contracts needed by this source.
- Source diagnostics and counters.
- Unit tests with fake capture provider.
- Integration/probe tests that can run on a local Windows machine.

### Out of Scope

- Audio pipeline behavior.
- File/container output behavior.
- Encoder input media type negotiation.
- Managed API DTOs.
- UI/UX for selecting monitors or regions.
- Shipping HDR tone mapping.
- Advanced frame pacing or real-time encoder backpressure policy beyond source metadata and counters.

## Recommended Initial Approach

Use a single V2 source abstraction:

```cpp
class WindowsDesktopVideoSource : public IVideoCaptureSource
{
};
```

The source implementation should hide the concrete Windows capture API behind a provider boundary:

```text
WindowsDesktopVideoSource
  -> IDesktopCaptureProvider
       -> WindowsGraphicsCaptureProvider
       -> DxgiDesktopDuplicationProvider, future/probe option
```

For the first implementation, prefer Windows Graphics Capture if it can satisfy the first workflow's monitor and region requirements. Keep DXGI Desktop Duplication as an investigation/probe path because it may behave differently for HDR displays, cursor capture, frame timing, and protected content.

## Requirements

### Source Identity

The desktop source must expose stable identity.

Acceptance criteria:

- The source descriptor includes `SourceId`, `SourceKind::Desktop`, and a human-readable name.
- The source creates exactly one video stream descriptor for the first implementation.
- The stream descriptor includes `StreamId`, parent `SourceId`, `MediaKind::Video`, and a stream name.
- Source and stream ids remain stable from `Start` through `Stop`.
- Runtime diagnostics include source id and stream id.

### Monitor Targeting

The source must capture a selected local monitor.

Acceptance criteria:

- The source accepts a monitor identity from the V2 config or factory input.
- The source validates that the monitor target exists before recording starts.
- The source reports the monitor's physical pixel bounds.
- If the monitor disappears before start, `Start` returns a structured failure.
- If the monitor disappears during capture, the source reports a source-ended or source-failed event and stops producing frames.
- The source does not silently switch to another monitor.

### Region Capture

The source must support optional region capture in monitor-relative physical pixels.

Acceptance criteria:

- Empty region means capture the full monitor.
- Region coordinates are interpreted as physical pixels, not effective DPI units.
- Region validation rejects negative coordinates.
- Region validation rejects zero or negative width/height.
- Region validation rejects regions outside the selected monitor bounds.
- Region output dimensions are reflected in `VideoMediaType`.
- If the chosen Windows capture API captures the full monitor first, region cropping is performed by a video processor stage rather than hidden in downstream output code.

### D3D Device Ownership

The source must integrate with the session's D3D ownership model.

Acceptance criteria:

- The session or Windows infrastructure factory owns the D3D11 device strategy for the graph.
- The source receives a D3D device/context dependency from Windows infrastructure rather than creating unrelated device state without coordination.
- Any source-created API-specific objects are released before the graph's D3D device is released.
- COM objects are held with `wil::com_ptr<T>`.
- Win32 handles are held with WIL handle wrappers where applicable.
- Device removal or reset is reported as a structured source failure.

### Texture Sample Ownership

The source must publish texture-backed video samples with safe lifetime.

Acceptance criteria:

- Each emitted video sample owns or retains a reference to its `ID3D11Texture2D` until downstream consumers release the sample.
- The sample does not expose a borrowed texture pointer whose lifetime ends when the frame callback returns.
- The sample includes source id, stream id, media type, source timestamp, frame sequence number, and frame dimensions.
- Async downstream queues can retain the sample without use-after-release risk.
- Tests verify that sample lifetime is independent of callback return.

### Frame Timing

The source must provide timing metadata without owning recording-time policy.

Acceptance criteria:

- Each frame includes a source timestamp or frame arrival timestamp.
- Each frame includes a monotonically increasing sequence number.
- The source does not subtract pause duration; the core recording clock owns recording-relative time.
- The source may continue producing frames while the session is paused; the pipeline decides whether to drop or process paused frames.
- Duplicate, late, or skipped frames increment diagnostics counters when detectable.
- Frame timing behavior is tested with a fake capture provider and deterministic timestamps.

### Media Type Reporting

The source must report a video media type before samples are consumed.

Acceptance criteria:

- `VideoMediaType` includes width, height, frame rate request or observed frame rate, pixel format, color primaries, transfer function, and range when known.
- Unknown color information is represented explicitly as `Unknown`, not guessed.
- Region dimensions are reflected in width and height.
- Pixel format is mapped into the V2 `VideoPixelFormat` model.
- Media type changes during capture are either rejected as source failure or reported through a future media-type-changed event; the first implementation should fail clearly rather than silently change stream shape.

### Basic Video Processing

The desktop source must integrate with basic video processing stages but should not own all processing policy.

Acceptance criteria:

- Region crop can be represented as a video processor stage when the capture provider cannot capture the region directly.
- Format conversion can be represented as a video processor stage when the source texture format is not compatible with downstream requirements.
- Scaling is available as a future processor contract but not required for the first implementation unless needed by the source/provider.
- Processor stages preserve source id, stream id, timestamps, and sequence number.
- The source PRD does not require encoder-specific conversion such as final H.264 input format negotiation.

### HDR and Color Placeholder Behavior

The source must not pretend HDR is solved.

Acceptance criteria:

- The source records known display or texture color metadata when the chosen Windows API exposes it.
- Unknown HDR/color metadata is represented as unknown.
- The first implementation may resolve `HdrPolicy::Auto` to SDR-safe processing for downstream H.264 MP4 work.
- The source does not implement final tone mapping.
- The source emits diagnostics when HDR or wide-color input is detected but not fully handled.
- The source keeps enough metadata for a future tone mapping processor to make informed decisions.

### Cursor Capture Policy

Cursor handling must be explicit even if the first implementation uses a default.

Acceptance criteria:

- The source config or provider supports a documented cursor capture policy placeholder.
- The first implementation documents whether cursor capture is included or excluded.
- Cursor behavior is included in probe output.
- Cursor capture does not change frame timing or media type.

### Lifecycle

The source must support deterministic lifecycle behavior.

Acceptance criteria:

- `Start` initializes provider resources and begins frame delivery.
- `Stop` stops frame delivery before releasing provider resources.
- `Stop` is idempotent or returns a stable already-stopped result.
- No frame callbacks are invoked after `Stop` returns.
- Source teardown occurs before the graph releases D3D resources.
- Failures during stop are reported with component and operation details.

### Diagnostics

The source must expose enough diagnostics to support future performance work.

Acceptance criteria:

- Counters include frames produced, frames dropped by source, duplicate frames detected, late frames detected, source restarts if any, and provider failures.
- Diagnostics include selected monitor identity, requested region, effective output size, pixel format, and color metadata.
- Diagnostics include selected provider name, for example `WindowsGraphicsCaptureProvider`.
- Diagnostics are queryable by the pipeline or included in stop results.

## User Stories

### Native Developer: Capture a Monitor

As a native developer, I want to configure a desktop video source for one monitor so that the V2 pipeline can receive local desktop frames.

Acceptance criteria:

- Given a valid monitor target, `Start` begins producing video samples.
- Samples include source id, stream id, texture reference, sequence number, and timestamp.
- `Stop` releases capture resources and prevents further callbacks.

### Native Developer: Capture a Region

As a native developer, I want to configure a monitor-relative capture region so that the first workflow can record part of the desktop.

Acceptance criteria:

- Valid regions produce samples with region-sized media type dimensions.
- Invalid regions fail validation before capture starts.
- Region crop is performed in a source-owned or processor-owned stage, not inside file output.

### Video Processor Developer: Preserve Texture Lifetime

As a video processor developer, I want video samples to retain their D3D texture references so that async processing cannot read released resources.

Acceptance criteria:

- A downstream test can retain a sample after source callback return.
- The texture remains valid until the sample is destroyed.

### Future HDR Developer: Inspect Color Metadata

As a future HDR developer, I want the source to preserve known color metadata so that tone mapping can be implemented later without redesigning the source contract.

Acceptance criteria:

- Captured metadata maps into `VideoMediaType`.
- Unknown metadata is explicit.
- HDR detection emits diagnostics without requiring tone mapping in this PRD.

## Technical Constraints

- C++20.
- Windows implementation.
- No new third-party dependencies.
- Use WIL for COM and handle ownership.
- Prefer D3D11 for the first implementation to align with Media Foundation and existing Windows capture work.
- Do not expose Windows capture API types through the core `IVideoCaptureSource` contract.
- Do not couple the source to MP4, H.264, or Media Foundation sink writer implementation details.
- Do not depend on managed C# objects.

## Proposed Deliverables

- `WindowsDesktopVideoSource`.
- `IDesktopCaptureProvider` abstraction.
- `WindowsGraphicsCaptureProvider` first implementation or probe-backed implementation.
- Optional `DxgiDesktopDuplicationProvider` probe or spike notes if needed.
- `DesktopVideoSourceConfig` mapping from V2 source config.
- D3D device dependency object or factory contract.
- Windows texture-backed video sample payload that can be carried by the V2 media sample model.
- Region crop processor or source-integrated region handling.
- Source diagnostics object.
- Unit tests using fake capture provider.
- Local Windows integration/probe tests.

## PR-Sized Work Chunks

These chunks are the recommended implementation split for PRD 003. Each chunk should fit in one targeted pull request with focused tests and a narrow review surface. The default assumption is that PRD 001 core primitives, interfaces, fake components, and sample contracts exist before this work starts. If a missing core type is discovered, add only the smallest adapter or placeholder needed in the current chunk and capture broader core changes in PRD 001 follow-up work.

### PR 003-A: Desktop Source Config and Identity Mapping

Scope:

- Add `DesktopVideoSourceConfig` or the V2 Windows mapping from core source config to desktop-source construction input.
- Define monitor target fields used by the source for the first implementation.
- Define optional region fields in monitor-relative physical pixels.
- Map source id and stream id into source and stream descriptors.
- Add tests for descriptor construction and config mapping.

Acceptance criteria:

- A desktop source config can express full-monitor capture.
- A desktop source config can express monitor-relative region capture.
- Source descriptor includes `SourceId`, `SourceKind::Desktop`, and a human-readable name.
- Stream descriptor includes `StreamId`, parent `SourceId`, `MediaKind::Video`, and a stream name.
- Source and stream ids are stable across repeated descriptor reads.

Out of scope for this PR:

- Monitor existence validation.
- Real Windows capture provider.
- D3D device ownership.
- Frame emission.

### PR 003-B: Provider Boundary and Fake Provider Contract

Scope:

- Add `IDesktopCaptureProvider`.
- Define provider start, stop, descriptor, media type, frame callback, and diagnostic hooks.
- Add fake provider implementation for unit tests.
- Add provider event or callback token ownership rules.
- Add compile-focused tests proving fake provider can emit frames through the provider contract.

Acceptance criteria:

- `WindowsDesktopVideoSource` can depend on a provider interface instead of a concrete Windows API.
- The provider contract does not expose Windows Graphics Capture or DXGI types through core-facing interfaces.
- Fake provider can emit controlled frame metadata and sample payload placeholders.
- Provider callbacks are not invoked while holding provider-state locks.
- Callback ownership rules are documented in code comments.

Out of scope for this PR:

- Real Windows Graphics Capture provider.
- D3D texture lifetime.
- Region crop behavior.
- Source lifecycle beyond provider-contract tests.

### PR 003-C: WindowsDesktopVideoSource Skeleton

Scope:

- Add `WindowsDesktopVideoSource`.
- Inject `DesktopVideoSourceConfig`, provider, and source diagnostics dependencies.
- Implement descriptor and media type read paths using provider or config data.
- Wire fake provider callbacks into the source's video sample handler with placeholder sample payloads if needed.
- Add unit tests for construction, descriptor reads, and fake frame forwarding.

Acceptance criteria:

- The source satisfies the V2 `IVideoCaptureSource` contract.
- The source can be constructed with a fake provider.
- Descriptor and stream identity remain stable from construction through stop.
- Fake provider frames can be observed through the source callback path.
- The source still compiles without real Windows capture code.

Out of scope for this PR:

- Real D3D texture samples.
- Real provider start/stop implementation.
- Monitor and region validation.
- HDR/color metadata.

### PR 003-D: Monitor Identity and Bounds Validation

Scope:

- Add monitor identity abstraction used by the desktop source.
- Add monitor bounds resolver interface for Windows infrastructure.
- Add fake monitor resolver for tests.
- Validate monitor target existence before start.
- Report physical pixel bounds in diagnostics or source metadata.
- Add tests for valid monitor, missing monitor, and stable bounds.

Acceptance criteria:

- A valid monitor target can be resolved before capture starts.
- Missing monitor target fails with a structured source validation or start failure.
- Physical pixel bounds are available for full-monitor and region validation.
- The source does not silently switch to a different monitor when the requested monitor is missing.

Out of scope for this PR:

- Real monitor enumeration UI.
- Real Windows Graphics Capture activation.
- Monitor disappearance during active capture.

### PR 003-E: Region Validation and Effective Media Type

Scope:

- Add region validation in physical pixels.
- Treat empty region as full monitor.
- Compute effective output dimensions for full-monitor and region capture.
- Reflect effective dimensions in `VideoMediaType`.
- Add tests for negative coordinates, zero dimensions, out-of-bounds regions, full-monitor dimensions, and valid region dimensions.

Acceptance criteria:

- Empty region means full monitor.
- Negative coordinates are rejected.
- Zero or negative width/height is rejected.
- Regions outside monitor bounds are rejected.
- Valid region dimensions are reflected in `VideoMediaType`.
- Region units are documented as physical pixels, not effective DPI units.

Out of scope for this PR:

- Actual texture cropping.
- Scaling.
- Real provider capture.

### PR 003-F: D3D Device Dependency Model

Scope:

- Add Windows infrastructure-facing D3D dependency object or factory contract for the source.
- Define how the session-owned or infrastructure-owned D3D device is passed to the desktop source/provider.
- Add fake or lightweight test doubles for D3D dependency ownership where possible.
- Document teardown ordering relative to provider and sample resources.

Acceptance criteria:

- The source/provider receives a D3D dependency rather than creating unrelated device state without coordination.
- D3D dependency ownership is explicit and compatible with session graph ownership.
- Provider resources are released before the graph D3D device dependency is released.
- Device removal/reset can be represented as a structured source failure.

Out of scope for this PR:

- Actual WGC frame capture.
- Texture-backed sample implementation.
- Device creation policy for the whole application.

### PR 003-G: Texture-Backed Video Sample Ownership

Scope:

- Add Windows texture-backed video sample payload for V2 video samples.
- Ensure each sample owns or retains an `ID3D11Texture2D` reference until downstream consumers release it.
- Add frame metadata fields: source id, stream id, media type, source timestamp, sequence number, and frame dimensions.
- Add tests using fake reference-counted texture objects or test doubles.

Acceptance criteria:

- A sample can outlive the frame callback that produced it.
- Async downstream queues can retain samples without use-after-release risk.
- Sample metadata includes source id, stream id, dimensions, sequence number, and timestamp metadata.
- The source does not expose borrowed texture pointers as the lifetime contract.

Out of scope for this PR:

- Real Windows Graphics Capture frames.
- Region crop.
- Encoder-compatible format conversion.

### PR 003-H: Source Lifecycle and Callback Drain

Scope:

- Implement source start/stop orchestration around the provider.
- Ensure provider frame delivery starts only after successful source start.
- Ensure stop disables frame delivery before provider resource release.
- Add idempotent stop or stable already-stopped behavior.
- Add tests for start once, stop, stop before start, provider start failure, provider stop failure, and no callbacks after stop.

Acceptance criteria:

- `Start` initializes provider resources and begins frame delivery.
- `Stop` prevents further frame callbacks before provider resources are released.
- `Stop` is idempotent or returns a stable already-stopped result.
- No frame callbacks occur after `Stop` returns.
- Stop failures include component and operation details.

Out of scope for this PR:

- Real provider implementation.
- Monitor disappearance during capture.
- Probe executable.

### PR 003-I: Windows Graphics Capture Provider Activation

Scope:

- Add initial `WindowsGraphicsCaptureProvider`.
- Activate capture for a selected monitor using the chosen monitor identity.
- Initialize provider resources without delivering production frames yet, if needed to keep the PR small.
- Report provider name and activation diagnostics.
- Add local probe or integration test path that validates activation on the primary monitor.

Acceptance criteria:

- Provider can activate against a valid selected monitor on a local Windows machine.
- Activation failure returns structured provider diagnostics.
- Provider name is reported as `WindowsGraphicsCaptureProvider`.
- Provider resources are released on stop or activation failure.
- Primary-monitor activation can be verified by a local probe or integration test.

Out of scope for this PR:

- Region capture.
- Full frame delivery into the V2 pipeline.
- HDR/color metadata mapping.
- DXGI Desktop Duplication provider.

### PR 003-J: Windows Graphics Capture Frame Delivery

Scope:

- Deliver frames from `WindowsGraphicsCaptureProvider` into `WindowsDesktopVideoSource`.
- Convert provider frame notifications into texture-backed V2 video samples.
- Add frame sequence numbers and source timestamp or arrival timestamp.
- Add integration/probe coverage for sample emission on the primary monitor.

Acceptance criteria:

- Full-monitor capture emits texture-backed samples.
- Sequence numbers increase monotonically.
- Each frame includes timestamp metadata.
- Provider resources remain valid while samples are retained downstream.
- Local probe can show frame emission count, dimensions, pixel format, and provider name.

Out of scope for this PR:

- Region crop.
- Dirty region optimization.
- Cursor policy changes.
- HDR/tone mapping.

### PR 003-K: Region Crop Integration

Scope:

- Implement region capture behavior when a valid region is configured.
- If the provider can capture the region directly, document and test that path.
- If the provider captures the full monitor, add a crop processor or source-owned crop stage.
- Preserve source id, stream id, timestamp, sequence number, and color metadata through crop.
- Add unit tests with fake frames and local probe coverage for region dimensions.

Acceptance criteria:

- Valid regions produce samples with region-sized media type dimensions.
- Invalid regions fail before provider start.
- Crop behavior is implemented in source/provider/processor code, not in file output.
- Cropped samples preserve identity and timing metadata.
- Local probe can capture a region and report expected dimensions.

Out of scope for this PR:

- Scaling.
- Encoder input negotiation.
- UI region selection.

### PR 003-L: Frame Timing and Source Diagnostics

Scope:

- Add frame timing diagnostics.
- Add counters for frames produced, duplicate frames, late frames, skipped frames, provider failures, and source-ended events where detectable.
- Add deterministic fake-provider tests for duplicate, late, and skipped frame scenarios.
- Add stop-result or source diagnostic integration hooks for the pipeline.

Acceptance criteria:

- Each frame includes source timestamp or arrival timestamp metadata.
- The source does not subtract pause duration.
- Duplicate, late, or skipped frames increment counters when detectable.
- Diagnostics include selected provider name, source id, stream id, effective output size, and requested region.
- Probe output reports timing cadence and diagnostic counters.

Out of scope for this PR:

- Core recording clock behavior.
- Sink backpressure policy.
- Real telemetry upload.

### PR 003-M: Cursor Capture Policy Placeholder

Scope:

- Add cursor capture policy placeholder to config/provider options.
- Document first implementation default.
- Pass cursor policy to the provider when supported.
- Include cursor behavior in diagnostics and probe output.
- Add tests for config propagation.

Acceptance criteria:

- Cursor capture behavior is explicit, not accidental.
- The first implementation documents whether cursor capture is included or excluded.
- Cursor policy does not change media type or frame timing.
- Probe output reports cursor policy.

Out of scope for this PR:

- Custom cursor compositing.
- UI cursor options.
- Per-frame cursor metadata unless the provider already exposes it cheaply.

### PR 003-N: Color Metadata and HDR Placeholder

Scope:

- Map known provider/display color metadata into V2 media type fields.
- Represent unknown color primaries, transfer function, and range explicitly as unknown.
- Emit diagnostics when HDR or wide-color input is detected but not fully handled.
- Document the first implementation's `HdrPolicy::Auto` behavior.
- Add unit tests for known and unknown color metadata mapping.

Acceptance criteria:

- Known color metadata is preserved in `VideoMediaType` where available.
- Unknown metadata is not guessed.
- HDR/wide-color detection emits diagnostics without claiming final tone mapping.
- The source keeps enough metadata for a future tone mapping processor.
- SDR capture reports sane defaults or unknowns.

Out of scope for this PR:

- Final HDR tone mapping.
- H.264 HDR metadata preservation.
- Display brightness or target-nits selection.

Implementation note:

- The first implementation treats `HdrPolicy::Auto` as a placeholder policy. Known provider/display color metadata is preserved on the V2 media type, unknown metadata remains unknown, and HDR or wide-color input raises diagnostics that downstream tone mapping is still pending.

### PR 003-O: Provider Failure and Monitor Change Handling

Scope:

- Handle monitor disappearance or provider failure during active capture.
- Report source-ended or source-failed event/result according to the chosen core contract.
- Stop producing frames after terminal provider failure.
- Add tests with fake provider failure injection.
- Add local probe notes for monitor configuration change behavior where practical.

Acceptance criteria:

- The source does not silently switch to another monitor.
- Provider failure transitions the source into a stable failed or ended state.
- No additional frames are emitted after terminal failure.
- Failure diagnostics include component, operation, source id, stream id, and provider name.

Out of scope for this PR:

- Automatic graph rebuild.
- Hot-switching to a new monitor.
- UI recovery prompts.

Implementation note:

- The first implementation treats monitor disappearance as a terminal provider failure. The desktop source records provider/source/stream identity in diagnostics, stops forwarding frames, and does not silently switch to a different monitor.

### PR 003-P: Local Probe and Integration Harness

Scope:

- Add or extend a local Windows probe for desktop video source behavior.
- Exercise primary-monitor full capture.
- Exercise region capture.
- Print provider name, output size, texture format, cursor policy, color metadata, timing cadence, and counters.
- Document how to run the probe and which checks are manual.

Acceptance criteria:

- Probe can start and stop capture on the primary monitor.
- Probe can emit at least one full-monitor sample and one region sample.
- Probe output includes dimensions, pixel format, provider name, cursor policy, color metadata, and timing counters.
- Probe does not require Media Foundation output or managed code.

Out of scope for this PR:

- Automated CI coverage requiring an interactive desktop.
- MP4 file output.
- UI integration.

Implementation note:

- The local native probe is `PrimaryMonitorSourceProbe_WhenEnabled_CapturesFullMonitorAndRegion` in `V2WindowsDesktopVideoSourceTests`. It is disabled by default; run it with `CAPTURETOOL_V2_DESKTOP_SOURCE_PROBE=1` and `vstest.console.exe x64\Debug\CaptureInterop.Tests.dll /TestCaseFilter:FullyQualifiedName~PrimaryMonitorSourceProbe_WhenEnabled_CapturesFullMonitorAndRegion`.
- The probe starts/stops the V2 desktop source twice, first for the primary monitor and then for a centered region. It logs provider name, output/sample dimensions, pixel format, cursor policy, color metadata, HDR/wide-color flags, timing counters, sequence, and timestamp through the MSTest logger.
- Manual checks: confirm activation succeeds on the local Windows desktop, at least one frame is observed for both full-monitor and region capture, and probe dimensions match the selected monitor/region. Monitor-change behavior is still manual: disconnecting or changing displays during capture should surface as provider/source failure diagnostics rather than silently switching monitors.

### Recommended Sequence

Implement these chunks in order:

```text
003-A -> 003-B -> 003-C -> 003-D -> 003-E -> 003-F
      -> 003-G -> 003-H -> 003-I -> 003-J -> 003-K
      -> 003-L -> 003-M -> 003-N -> 003-O -> 003-P
```

The order keeps the fake/testable contract ahead of real Windows capture, then adds D3D ownership, lifecycle, WGC activation, frame delivery, region handling, diagnostics, and probe coverage. Adjacent chunks may be combined only when the implementation is genuinely tiny after coding; the default expectation is one chunk per PR.

## Testing Requirements

Unit tests must cover:

- Source descriptor creation.
- Stream descriptor creation.
- Monitor/region validation.
- Region dimensions in media type.
- Texture sample lifetime with fake textures or fake reference-counted resources.
- Frame sequence increments.
- Timestamp propagation.
- Start/stop idempotency.
- No callbacks after stop.
- Source failure propagation.
- HDR/color metadata mapping for known and unknown values.

Integration/probe tests should cover:

- Start/stop on the primary monitor.
- Full-monitor sample emission.
- Region sample emission.
- Reported output size.
- D3D texture format.
- Provider name.
- Cursor capture behavior.
- Color metadata observed on SDR and HDR displays where available.
- Behavior when monitor configuration changes during capture, if practical.

## Milestones

### Milestone 1: Source Contract and Provider Boundary

- Add desktop source config mapping.
- Add `WindowsDesktopVideoSource`.
- Add `IDesktopCaptureProvider`.
- Add fake provider tests.

Exit criteria:

- Source and stream descriptors are stable.
- Fake provider can emit frames through the source.

### Milestone 2: D3D Sample Ownership

- Add D3D device dependency model.
- Add texture-backed video sample ownership.
- Add callback/token safety around frame delivery.

Exit criteria:

- Tests prove samples can outlive callbacks.
- Stop prevents further frame delivery.

### Milestone 3: Monitor and Region Capture

- Implement selected monitor capture through the chosen provider.
- Implement full-monitor output.
- Implement region validation and crop behavior.

Exit criteria:

- Primary monitor probe emits frames.
- Region probe emits expected dimensions.
- Invalid regions fail before start.

### Milestone 4: Frame Timing and Diagnostics

- Add frame sequence and timestamp metadata.
- Add source diagnostics counters.
- Add provider diagnostics.

Exit criteria:

- Fake timing tests pass.
- Probe output reports timing, provider, pixel format, and dimensions.

### Milestone 5: HDR Placeholder and Color Metadata

- Map known color metadata into V2 media type fields.
- Represent unknown metadata explicitly.
- Emit diagnostics for HDR/wide-color detection.

Exit criteria:

- SDR capture reports sane defaults or unknowns.
- HDR-capable probe path records available metadata without performing final tone mapping.

## Open Questions

- Should the first production implementation use Windows Graphics Capture, DXGI Desktop Duplication, or support both behind the provider interface?
- Should cursor capture be on by default for desktop recordings?
- Should monitor identity be represented by `HMONITOR`, device name, LUID plus output index, or a V2 monitor descriptor?
- Should region cropping be implemented directly in the provider where possible or always as an explicit processor stage?
- Should media type changes during capture fail the source or trigger a graph rebuild event?

## Definition of Done

- The desktop source can produce D3D-backed video samples for a selected local monitor.
- Optional physical-pixel region capture is validated and reflected in media type dimensions.
- Source and stream identity are stable.
- Texture lifetime is safe for asynchronous downstream processing.
- Frame timing metadata is emitted and testable.
- Source lifecycle is deterministic and no callbacks occur after stop.
- HDR/color metadata is represented honestly as known or unknown, with no fake tone mapping claims.
- Unit tests pass without real capture devices.
- At least one local Windows probe demonstrates full-monitor capture and region capture.
