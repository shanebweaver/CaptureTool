# CaptureInterop V2 Architecture Plan

## Purpose

CaptureInterop V2 is a forward-looking native capture architecture for Capture Tool. It is not a refactor plan for the current recorder. The goal is to design a professional media capture pipeline that can start with one focused workflow and grow into a flexible system for multiple sources, codecs, containers, color policies, and managed API consumers.

The first vertical slice is:

- Capture local desktop video.
- Capture local system audio through WASAPI loopback.
- Write H.264 video and optional AAC audio to MP4.
- Allow local audio to be muted and unmuted while recording.
- Allow pause and resume without timestamp drift.
- Expose the workflow through a robust C ABI consumed by C#.

The design should be C++20, Windows-first, dependency-light, and should use WIL for COM pointers, Win32 handles, HRESULT handling, and cleanup patterns wherever it improves safety.

## Design Principles

- Keep domain concepts generic: sources, streams, samples, codecs, containers, clocks, transforms, and sinks should not be named after one Windows API.
- Keep Windows details at the edge: Media Foundation, WASAPI, Windows Graphics Capture, D3D11, DXGI, and Win32 handles live in infrastructure implementations.
- Prefer capability-driven composition over hard-coded workflows.
- Treat media time as a first-class domain concept.
- Separate capture state from transport, encoding, and file output state.
- Do not expose COM objects, C++ classes, STL containers, exceptions, or object ownership ambiguity across the managed boundary.
- Make configuration versioned and extensible from day one.
- Make teardown, stop, pause, resume, and failure paths explicit and testable.

## Clean Architecture Shape

The architecture should be organized around four rings.

```text
Managed Application
  -> C# capture facade, use cases, view models

Native API Boundary
  -> C ABI, handle ownership, DTO marshaling, error reporting

Native Application/Core
  -> pipeline orchestration, state machine, config validation, clocks
  -> source/processor/sink interfaces and media value objects

Native Windows Infrastructure
  -> Windows Graphics Capture, WASAPI, Media Foundation, D3D11, DXGI
```

The core knows what a video stream, audio stream, sample timestamp, codec request, container request, and sink capability are. It does not know how Windows Graphics Capture produces frames, how WASAPI loopback buffers are read, or how Media Foundation writes MP4.

## Proposed Native Projects

This can be implemented as separate projects or as separate folders/namespaces inside the existing native library while the design is still being proven.

```text
src/CaptureInterop.V2.Core
  Media/
  Pipeline/
  Configuration/
  Diagnostics/

src/CaptureInterop.V2.Windows
  Capture/Desktop/
  Capture/Audio/
  Direct3D/
  MediaFoundation/
  Color/

src/CaptureInterop.V2.Api
  CaptureInteropV2Exports.h
  CaptureInteropV2Exports.cpp
  NativeDto/

src/CaptureInterop.V2.Tests
  Core/
  Windows/
  Api/
```

If we keep this inside the current C++ project initially, use a `CaptureInterop::V2` namespace and a clear folder boundary:

```text
src/CaptureInterop.Lib/V2/Core
src/CaptureInterop.Lib/V2/Windows
src/CaptureInterop/V2
```

## Core Domain Concepts

### Media Kinds

```cpp
enum class MediaKind
{
    Unknown,
    Video,
    Audio
};
```

### Time

Use one canonical internal timebase. Media Foundation already uses 100 nanosecond units, so `MediaTime` can wrap that without leaking Media Foundation concepts into every interface.

```cpp
struct MediaTime
{
    int64_t ticks100ns;
};

struct MediaDuration
{
    int64_t ticks100ns;
};

struct Rational
{
    uint32_t numerator;
    uint32_t denominator;
};
```

Rules:

- Capture sources may report source timestamps.
- The pipeline clock translates source timestamps into recording-relative timestamps.
- Paused wall-clock time is subtracted from output timestamps.
- Muting audio does not stop the recording clock.
- Timestamps sent to the sink are always recording-relative and monotonic per stream.

### Source and Stream Identity

```cpp
enum class SourceKind
{
    Desktop,
    Window,
    Camera,
    Microphone,
    SystemAudio,
    File
};

struct SourceId
{
    uint32_t value;
};

struct StreamId
{
    uint32_t value;
};

struct SourceDescriptor
{
    SourceId id;
    SourceKind kind;
    std::string name;
};

struct StreamDescriptor
{
    StreamId id;
    SourceId sourceId;
    MediaKind kind;
    std::string name;
};
```

### Video Media Type

```cpp
enum class VideoPixelFormat
{
    Unknown,
    Bgra8,
    Rgba16Float,
    Nv12,
    P010
};

enum class ColorPrimaries
{
    Unknown,
    Srgb,
    Rec709,
    Rec2020
};

enum class TransferFunction
{
    Unknown,
    Srgb,
    Gamma22,
    St2084Pq,
    Hlg
};

enum class ColorRange
{
    Unknown,
    Full,
    Limited
};

struct VideoMediaType
{
    uint32_t width;
    uint32_t height;
    Rational frameRate;
    VideoPixelFormat pixelFormat;
    ColorPrimaries colorPrimaries;
    TransferFunction transferFunction;
    ColorRange range;
};
```

### Audio Media Type

```cpp
enum class AudioSampleFormat
{
    Unknown,
    Pcm16,
    Pcm24,
    Pcm32,
    Float32
};

struct AudioMediaType
{
    uint32_t sampleRate;
    uint16_t channels;
    uint16_t bitsPerSample;
    uint16_t blockAlign;
    AudioSampleFormat sampleFormat;
};
```

### Codec and Container Requests

```cpp
enum class VideoCodec
{
    None,
    H264,
    Hevc,
    Av1
};

enum class AudioCodec
{
    None,
    Aac,
    Mp3,
    Pcm
};

enum class ContainerFormat
{
    Mp4,
    Mp3,
    Wav
};

struct VideoEncodingSettings
{
    VideoCodec codec;
    uint32_t bitrate;
    Rational frameRate;
    uint32_t gopLength;
    bool hardwareAccelerationPreferred;
};

struct AudioEncodingSettings
{
    AudioCodec codec;
    uint32_t bitrate;
    uint32_t sampleRate;
    uint16_t channels;
};

struct OutputSettings
{
    ContainerFormat container;
    std::wstring outputPath;
    std::optional<VideoEncodingSettings> video;
    std::optional<AudioEncodingSettings> audio;
};
```

The key professional rule is that a container profile defines what streams it accepts.

Examples:

- MP4 accepts H.264 video plus AAC audio for the first slice.
- MP3 accepts audio only. It should advertise no video track support.
- WAV accepts PCM audio only.

The graph builder should prune unsupported streams when possible, and the sink should defensively ignore or reject samples for stream kinds it did not accept. For MP3, this means only audio frames are written. Video samples must not become the responsibility of the MP3 writer.

### HDR and Tone Matching

HDR handling belongs in the video processing stage, not in the source or the file writer.

```cpp
enum class HdrPolicy
{
    Auto,
    Preserve,
    MapToSdr,
    MatchDisplay,
    ForceSdr
};

struct ToneMappingSettings
{
    HdrPolicy policy;
    float targetNits;
    bool preserveMetadataWhenPossible;
};
```

Initial slice recommendation:

- Default to SDR output for H.264 MP4.
- Detect source color information where available.
- Insert a color transform/tone mapping processor only when the source is HDR or when the requested output color profile differs from the capture source.
- Keep the actual tone mapping implementation behind `IVideoProcessor`.

HDR requires isolated investigation before implementation because Windows capture, D3D texture formats, display color spaces, encoder support, and metadata propagation interact in non-obvious ways.

## Pipeline Roles

### Sources

Sources produce media samples.

```cpp
class IMediaSource
{
public:
    virtual ~IMediaSource() = default;
    virtual SourceDescriptor Describe() const = 0;
    virtual std::vector<StreamDescriptor> Streams() const = 0;
    virtual HRESULT Start() noexcept = 0;
    virtual HRESULT Stop() noexcept = 0;
};
```

Specialized source contracts can keep implementation code readable:

```cpp
class IVideoCaptureSource : public IMediaSource
{
public:
    virtual void SetFrameArrivedHandler(VideoFrameHandler handler) = 0;
};

class IAudioCaptureSource : public IMediaSource
{
public:
    virtual void SetSampleArrivedHandler(AudioSampleHandler handler) = 0;
};
```

Initial implementations:

- `WindowsDesktopCaptureSource`: Windows Graphics Capture or DXGI-based desktop capture, backed by D3D11 textures.
- `WasapiLoopbackAudioSource`: WASAPI loopback capture from the default render endpoint or a selected render endpoint.

Future implementations:

- Window capture source.
- Region capture source.
- Camera capture source.
- Microphone input source.
- File input source.
- Virtual source for tests.

### Processors

Processors transform samples.

```cpp
class IMediaProcessor
{
public:
    virtual ~IMediaProcessor() = default;
    virtual MediaKind Kind() const = 0;
    virtual HRESULT Configure(const MediaType& input, const MediaType& output) noexcept = 0;
    virtual HRESULT Process(const MediaSample& sample) noexcept = 0;
    virtual void SetOutputHandler(MediaSampleHandler handler) = 0;
};
```

Initial processors:

- Video frame scaler/cropper.
- Video format converter from BGRA or HDR texture formats to encoder-compatible formats.
- HDR tone mapper placeholder.
- Audio mute gate.
- Audio format converter if WASAPI output format does not match the encoder input format.

The audio mute gate is important for the first scenario. If the audio track is armed, toggling audio off should write silence with correct duration rather than stopping the audio stream and creating A/V drift.

### Encoders and Sinks

Conceptually, encoding and container writing are separate roles. In Media Foundation, `IMFSinkWriter` can perform both encoding and muxing. The architecture should still model these concepts separately so configuration stays professional and future-friendly.

```cpp
class IOutputSink
{
public:
    virtual ~IOutputSink() = default;
    virtual SinkCapabilities Capabilities() const = 0;
    virtual HRESULT Open(const OutputPlan& plan) noexcept = 0;
    virtual HRESULT WriteSample(StreamId streamId, const MediaSample& sample) noexcept = 0;
    virtual HRESULT Finalize() noexcept = 0;
};
```

Initial implementation:

- `MediaFoundationFileSink`
  - Uses `MFCreateSinkWriterFromURL`.
  - Configures an MP4 output profile for H.264 video and AAC audio.
  - Creates only the streams accepted by the selected output profile.
  - Serializes writes into Media Foundation.
  - Finalizes exactly once.

Future implementations:

- `MediaFoundationMp3FileSink`.
- `MediaFoundationWavFileSink`.
- `PreviewSink` for UI preview frames.
- `NullSink` for tests.
- `SegmentedFileSink` for future rolling recordings.

### Pipeline Session

`CapturePipelineSession` owns one recording run.

Responsibilities:

- Validate configuration.
- Resolve source and sink capabilities.
- Build an output plan.
- Create sources, processors, and sink.
- Own the media clock.
- Own session state.
- Coordinate pause, resume, stop, and error propagation.
- Ensure deterministic teardown.

It should not know the details of WASAPI packet reading or Media Foundation media type construction.

## Object Ownership and Lifetime

Media Foundation and WASAPI both punish vague ownership. V2 should make ownership explicit in constructors, member fields, and API handles.

### Ownership Model

Use single ownership for the active graph:

```text
CaptureRecorderHandle
  owns CaptureRecorder
    owns zero or one CapturePipelineSession
      owns RecordingClock
      owns SourceCoordinator
      owns IVideoCaptureSource instances
      owns IAudioCaptureSource instances
      owns IMediaProcessor instances
      owns IOutputSink
      owns worker queues and cancellation state
```

Rules:

- `CaptureRecorder` is long-lived and reusable.
- `CapturePipelineSession` is per recording and is not reused after stop, finalization, or failure.
- Sources, processors, clocks, and sinks are owned by exactly one session.
- Factories are shared or injected, but products created by factories are session-owned.
- Native API handles own native objects; managed code never owns native C++ objects directly.
- Shared ownership should be avoided inside the pipeline. Prefer `std::unique_ptr`, references for non-owning constructor dependencies, and raw pointers only when lifetime is already enforced by the owner.
- COM objects are owned by `wil::com_ptr<T>` and released in deterministic teardown order.
- Win32 handles are owned by `wil::unique_handle` or a more specific WIL handle wrapper.
- Callback registrations return RAII tokens; destroying the token unregisters the callback.

### Session Teardown Order

Stop should be staged and observable because failures often happen during finalization, not during capture.

```text
1. Transition to Stopping so no new control commands mutate the graph.
2. Stop accepting source callbacks.
3. Signal source loops and worker queues to drain or cancel.
4. Stop audio sources and video sources.
5. Flush processor queues.
6. Flush the sink serialization queue.
7. Finalize the sink exactly once.
8. Release sink COM objects.
9. Release processors.
10. Release capture sources.
11. Release D3D and WASAPI resources.
12. Transition to Finalized or Failed with the first meaningful failure.
```

Important Media Foundation rules:

- `BeginWriting` happens only after all accepted streams are configured.
- No stream is added after writing begins.
- No samples are written after `Finalize` starts.
- `Finalize` is called at most once.
- The sink remains alive until all queued writes have completed or have been cancelled.
- `MFShutdown` is not tied to a single sink object's destructor unless the lifecycle owner can prove no other Media Foundation object is still alive.

The recommended pattern is a process-wide or module-wide `MediaFoundationRuntime` with reference-counted RAII ownership. Each Media Foundation component acquires a runtime lease before creating MF objects and releases that lease after all owned MF COM objects are released. This keeps `MFStartup` and `MFShutdown` out of individual sink writer destructors.

### Callback Ownership

Callbacks are the most likely place to accidentally keep dead objects alive or call into released objects.

Rules:

- Source callbacks should capture a weak session token or route through a session-owned dispatcher that can be shut down.
- `Stop` invalidates callback tokens before source teardown.
- Native-to-managed callbacks should never be invoked while holding internal graph locks.
- Managed delegates must remain rooted by the managed facade until callback unregister completes.
- Callback unregister should be synchronous from the caller's perspective, or clearly documented as async with a drain step.

### D3D and Texture Ownership

Video samples may reference GPU resources, so their lifetime must be as explicit as audio buffers.

Rules:

- The session owns the D3D device and immediate-context strategy for the graph.
- A video sample holding a texture must own a `wil::com_ptr<ID3D11Texture2D>` or equivalent reference until the sink has finished writing the sample.
- Texture processors must declare whether they mutate in place or produce new textures.
- The sink queue must retain texture references, not borrowed pointers, when writes are asynchronous.
- Cross-component D3D access should be serialized or documented as thread-safe at the component boundary.

### Buffer Ownership

Audio buffers should be copied or pooled before leaving a source callback.

Rules:

- WASAPI packet memory is borrowed only until `ReleaseBuffer`.
- The audio source should copy packet data into a session-owned sample buffer before publishing it.
- Pooled buffers are owned by the sample until the downstream component releases them.
- A processor that changes sample format or volume produces a new buffer unless it can prove safe in-place mutation.

## Configuration Model

The config should describe intent, not implementation steps.

```cpp
struct CapturePipelineConfig
{
    std::vector<SourceConfig> sources;
    OutputSettings output;
    RecordingControlSettings controls;
    ToneMappingSettings toneMapping;
    DiagnosticsSettings diagnostics;
};
```

### Source Config

```cpp
struct DesktopSourceConfig
{
    SourceId id;
    HMONITOR monitor;
    RECT captureArea;
    Rational frameRate;
};

struct SystemAudioSourceConfig
{
    SourceId id;
    AudioDeviceSelection deviceSelection;
    bool armed;
    bool initiallyMuted;
    float initialGainDb;
};
```

The distinction between `armed` and `initiallyMuted` matters:

- `armed = false`: no audio source and no audio track are created. Audio cannot be enabled later without starting a new session.
- `armed = true, initiallyMuted = true`: audio source and output track exist, but silence is written until audio is enabled.
- `armed = true, initiallyMuted = false`: audio is captured and written immediately.

This is the clean way to support "audio can be enabled/disabled during recording" without timestamp or muxing problems.

### Audio Volume Controls

Volume control should be a recording-pipeline concept, not an implicit change to the user's Windows endpoint volume.

Use three separate concepts:

- `armed`: whether the audio source and output track exist.
- `muted`: whether the source currently writes silence.
- `gain`: how much the captured samples are amplified or attenuated before encoding.

Recommended gain model:

```cpp
struct AudioGainSettings
{
    float gainDb;
    float minGainDb;
    float maxGainDb;
};
```

Rules:

- Default gain is `0.0 dB`, meaning unity.
- Runtime volume changes update an audio gain processor for a specific source or track.
- Mute overrides gain and emits silence while preserving timestamps.
- Gain is applied to the recorded stream only.
- The pipeline must not change system endpoint volume, application session volume, or microphone hardware volume unless a future API explicitly requests that side effect.
- Gain should be clamped to a supported range, for example `-60.0 dB` to `+12.0 dB`.
- Gain may be represented in the UI as 0-100 percent, but the native pipeline should prefer dB or a well-defined scalar.
- A volume value of zero should not be treated as the same thing as "not armed"; it is still an audio track with silent samples.

Processor order for one audio source:

```text
Audio source
  -> format normalizer if needed
  -> gain processor
  -> mute gate
  -> encoder/sink
```

For multiple audio inputs, each source should have independent gain and mute before any mixer stage:

```text
System audio -> gain -> mute -
                              -> mixer -> output track
Microphone   -> gain -> mute -
```

This keeps future microphone/system-audio balancing straightforward without changing the first MP4 workflow.

### Output Profiles

The graph builder should convert user intent into an `OutputPlan`.

```text
CapturePipelineConfig
  -> Validate requested source and output combinations
  -> Resolve source media types
  -> Resolve encoder/container capabilities
  -> Produce OutputPlan
  -> Build graph
```

Example plans:

```text
MP4 desktop recording
  Desktop source -> Video processor -> MF sink video stream H.264
  System audio source -> Audio mute gate -> MF sink audio stream AAC

MP3 audio recording
  System audio source -> Audio processor -> MF sink audio stream MP3
  No video stream connected to the sink
```

## State Machine

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

Commands:

- `Start`: Created -> Prepared -> Recording.
- `Pause`: Recording -> Paused.
- `Resume`: Paused -> Recording.
- `SetAudioMuted`: Recording or Paused, updates the audio gate only.
- `SetAudioGain`: Recording or Paused, updates the audio gain processor only.
- `Stop`: Recording or Paused -> Stopping -> Finalized.
- `Destroy`: allowed after Finalized or Failed; active sessions should stop first.

Pause/resume rules:

- Sources may continue producing samples while paused, but the pipeline drops them before the sink.
- The recording clock does not advance while paused.
- The first sample after resume uses the next recording-relative timestamp.
- Sink finalization must not include paused duration.

Audio toggle rules:

- Muting audio during recording does not pause the session.
- If an audio track exists, muted intervals produce silence samples with correct timestamps.
- If no audio track exists, toggle requests fail with a clear error or become no-ops according to API policy.
- The managed layer should expose this as audio mute state, not as adding/removing an audio track.

Audio volume rules:

- Volume changes do not pause the session.
- Volume changes do not reconfigure Media Foundation streams.
- Volume changes affect only samples produced after the command is applied.
- The command targets a source or track id rather than all audio by default.
- If no matching audio track exists, the command returns a clear unsupported-operation result.

## Threading Model

Initial recommendation:

- One session controller thread or serialized executor for state transitions.
- One video capture callback path from Windows Graphics Capture.
- One WASAPI capture loop thread, ideally using MMCSS where appropriate.
- One sink serialization queue so Media Foundation receives ordered writes.
- No blocking UI calls from native callbacks.

Backpressure policy:

- Video may drop late frames if encoding or writing falls behind.
- Audio should not drop samples except on unrecoverable failure; if muted, generate silence cheaply.
- The sink queue should have bounded memory.
- The pipeline should expose dropped frame counters and late sample diagnostics.

## Error Handling

Native internals can use HRESULTs and WIL, but the exported API must return stable result codes and expose detailed diagnostics separately.

Recommended patterns:

- Use `wil::com_ptr<T>` for COM ownership.
- Use `wil::unique_handle` for Win32 handles.
- Use `wil::scope_exit` for staged cleanup.
- Use WIL result macros inside implementation code.
- Catch all exceptions at the C ABI boundary.
- Never let C++ exceptions cross into C#.
- Make `Stop` and `Finalize` idempotent where possible.

Core error object:

```cpp
struct CaptureError
{
    HRESULT hresult;
    CaptureErrorCode code;
    std::string component;
    std::string operation;
    std::wstring message;
};
```

The API should allow managed callers to get the last error for a recorder/session handle.

## Managed API Boundary

The managed layer should not P/Invoke a long list of workflow-specific functions. It should own a versioned configuration model and call a small set of lifecycle methods on an opaque native handle.

### Native C ABI

Example export shape:

```cpp
extern "C"
{
    __declspec(dllexport)
    int32_t CtCaptureV2_CreateRecorder(CtCaptureV2_RecorderHandle* outHandle) noexcept;

    __declspec(dllexport)
    int32_t CtCaptureV2_DestroyRecorder(CtCaptureV2_RecorderHandle handle) noexcept;

    __declspec(dllexport)
    int32_t CtCaptureV2_Start(
        CtCaptureV2_RecorderHandle handle,
        const CtCaptureV2_Config* config) noexcept;

    __declspec(dllexport)
    int32_t CtCaptureV2_Pause(CtCaptureV2_RecorderHandle handle) noexcept;

    __declspec(dllexport)
    int32_t CtCaptureV2_Resume(CtCaptureV2_RecorderHandle handle) noexcept;

    __declspec(dllexport)
    int32_t CtCaptureV2_SetAudioMuted(
        CtCaptureV2_RecorderHandle handle,
        bool muted) noexcept;

    __declspec(dllexport)
    int32_t CtCaptureV2_SetAudioGain(
        CtCaptureV2_RecorderHandle handle,
        uint32_t sourceId,
        float gainDb) noexcept;

    __declspec(dllexport)
    int32_t CtCaptureV2_Stop(
        CtCaptureV2_RecorderHandle handle,
        CtCaptureV2_StopResult* result) noexcept;

    __declspec(dllexport)
    int32_t CtCaptureV2_GetLastError(
        CtCaptureV2_RecorderHandle handle,
        CtCaptureV2_ErrorBuffer* buffer) noexcept;
}
```

### ABI DTO Rules

Every exported struct should include size and version fields.

```cpp
struct CtCaptureV2_Config
{
    uint32_t size;
    uint32_t version;
    CtCaptureV2_SourceConfig* sources;
    uint32_t sourceCount;
    CtCaptureV2_OutputConfig output;
    CtCaptureV2_ToneMappingConfig toneMapping;
    CtCaptureV2_ControlConfig controls;
};
```

Rules:

- Strings passed from C# are borrowed for the duration of the call unless documented otherwise.
- Arrays include pointer plus count.
- Native code copies config values during `Start`.
- Native code owns native session objects.
- Managed code owns delegates and must keep them alive for callback registration.
- Native callbacks should be optional and should not be required for recording.
- ABI additions should append fields to size-versioned structs, not reorder existing fields.

### Managed C# Shape

The managed layer should expose application-friendly objects and hide native DTO details.

```csharp
public sealed class CaptureRecorder : IAsyncDisposable
{
    public Task StartAsync(CapturePipelineOptions options, CancellationToken cancellationToken);
    public Task PauseAsync();
    public Task ResumeAsync();
    public Task SetAudioMutedAsync(bool muted);
    public Task SetAudioGainAsync(CaptureSourceId sourceId, float gainDb);
    public Task<CaptureStopResult> StopAsync();
}
```

Configuration:

```csharp
public sealed record CapturePipelineOptions
{
    public required IReadOnlyList<CaptureSourceOptions> Sources { get; init; }
    public required CaptureOutputOptions Output { get; init; }
    public CaptureToneMappingOptions ToneMapping { get; init; } = CaptureToneMappingOptions.Auto;
}

public sealed record CaptureOutputOptions
{
    public required string OutputPath { get; init; }
    public required CaptureContainerFormat Container { get; init; }
    public VideoEncodingOptions? Video { get; init; }
    public AudioEncodingOptions? Audio { get; init; }
}
```

The C# model should validate obvious application-level mistakes before crossing into native code, but native code remains authoritative because it owns device and codec capability checks.

## First Vertical Slice

### Scenario

Desktop MP4 recording with optional local audio:

```text
Desktop video source
  -> video frame processor
  -> Media Foundation MP4 sink, H.264 stream

WASAPI loopback source
  -> audio mute gate
  -> Media Foundation MP4 sink, AAC stream
```

### Scope

Implement:

- Versioned native config for one desktop source and one system audio source.
- `CapturePipelineSession`.
- Recording clock with pause/resume support.
- WASAPI loopback audio source.
- Desktop video source.
- Audio mute gate with silence generation.
- Audio gain processor for recorded-source volume.
- Media Foundation MP4 file sink with H.264 video and AAC audio.
- C ABI handle lifecycle.
- C# facade and options objects.
- Unit tests for config validation, state transitions, pause/resume time, audio mute behavior, and sink stream selection.
- Unit tests for audio gain clamping and source-specific volume updates.
- Integration tests for native MP4 output where possible.

Defer:

- Camera capture.
- Microphone capture.
- MP3 output.
- HDR implementation.
- Multi-track audio.
- Multiple simultaneous video sources.
- Runtime output switching.
- Advanced encoder tuning.

## Isolated Investigations

These should be researched and tested separately before being folded into the main implementation.

### Windows Graphics Capture vs DXGI Desktop Duplication

Questions:

- Which API gives the most reliable desktop capture behavior for this app's target Windows versions?
- How do they differ for HDR displays, protected content, cursor capture, minimized windows, and monitor changes?
- What texture formats and color metadata are actually available?

Deliverable:

- Small native probe that captures frames and logs format, color space, frame timing, cursor behavior, and monitor identity.

### Media Foundation H.264 MP4 Profile

Questions:

- Which H.264 encoder MFT is selected by default?
- How reliably can bitrate, frame rate, GOP length, and hardware acceleration be controlled?
- What input formats should the video processor produce for best encoder compatibility?
- What HRESULTs occur for common bad combinations?

Deliverable:

- Native sink writer test harness that writes synthetic frames to MP4 across a small matrix of sizes and bitrates.

### WASAPI Loopback Timing

Questions:

- How stable are WASAPI packet timestamps for default render devices?
- Should timestamps come from WASAPI device position, QPC, or the pipeline clock?
- What happens across device changes, format changes, and silence periods?

Deliverable:

- Audio capture probe that logs packet durations, gaps, discontinuities, and endpoint format.

### Audio Toggle Semantics

Questions:

- Should UI "audio enabled" mean armed/unarmed or muted/unmuted?
- Should a recording started with audio disabled be able to enable audio later?
- Do we prefer a silent audio track for the entire recording when toggling is available?

Recommendation:

- Use "armed" for whether an audio track exists.
- Use "muted" for runtime enable/disable.
- For the first workflow, arm audio when the UI may toggle it during recording.

### Audio Volume Semantics

Questions:

- Should managed volume be represented as dB, scalar, or UI percent?
- Should volume control target a source, an output track, or both?
- How should clipping be handled when positive gain is applied?
- Should a future microphone source expose endpoint hardware gain separately from recording gain?

Recommendation:

- Use dB internally and expose a source-specific recording gain command.
- Keep endpoint or hardware volume out of scope for the first slice.
- Add metering later so the UI can show levels and warn about clipping.

### HDR Tone Matching

Questions:

- How should source HDR metadata be detected?
- Can H.264 MP4 preserve useful HDR metadata for the desired target players, or should H.264 default to SDR tone mapping?
- Which tone mapping operator gives acceptable visual output for desktop captures?
- How should display brightness and target nits be selected?

Deliverable:

- D3D11 color pipeline prototype with test textures and captured HDR frames, producing side-by-side SDR outputs.

### MP3 Output

Questions:

- Which Media Foundation path is most reliable for MP3 output?
- Should MP3 be represented as a separate audio-only file sink or as a profile of a general file sink?
- How should the graph builder behave if video sources are requested with MP3 output?

Recommendation:

- Model MP3 as an audio-only output profile.
- Prune video streams during graph construction unless they are needed for preview.
- Keep the MP3 sink defensive: it accepts audio samples only.

### Managed API Growth

Questions:

- Should managed config be marshaled as explicit structs or serialized into a native-owned config parser?
- How much should the managed facade expose as immutable records vs mutable builder objects?
- How should native capability enumeration be exposed?

Recommendation:

- Start with explicit size-versioned structs for the first slice.
- Add capability enumeration APIs before adding more source and codec options.

## Testing Strategy

Core tests:

- Config validation rejects unsupported source/container/codec combinations.
- MP3 output plans contain no video stream.
- MP4 output plans contain video and optional audio streams.
- Pause/resume clock excludes paused duration.
- Audio mute gate emits silence with the correct duration.
- State machine rejects invalid transitions.

Infrastructure tests:

- Media Foundation MP4 sink writes a playable synthetic H.264/AAC MP4.
- MP4 sink can write video-only.
- MP4 sink can write audio-only if the profile supports it.
- WASAPI source can start and stop cleanly.
- Desktop source can start and stop cleanly.
- Finalize is called once under normal stop, failure stop, and destructor cleanup.

API tests:

- C ABI creates and destroys recorder handles.
- `Start` copies config data and does not retain borrowed managed pointers.
- Invalid config returns a stable error code and detailed last error.
- Stop result contains finalization status and output path.

Managed tests:

- C# options map correctly to native DTOs.
- Managed facade keeps callbacks alive while registered.
- Managed facade prevents obvious invalid calls.
- Native errors map to domain exceptions or result objects consistently.

## Suggested Implementation Sequence

1. Create V2 folders, namespace, and core value objects.
2. Implement config DTOs and validation for the first MP4 desktop workflow.
3. Implement `RecordingClock` and state machine tests.
4. Implement `OutputProfileResolver` for MP4 and MP3 capability modeling.
5. Implement ownership-safe callback tokens and teardown-stage reporting.
6. Implement `NullSink`, fake video source, fake audio source, and core pipeline tests.
7. Implement audio gain and mute processors.
8. Implement Media Foundation MP4 sink with synthetic sample tests.
9. Implement WASAPI loopback source.
10. Implement desktop video source.
11. Connect the first real pipeline.
12. Add C ABI handle lifecycle and C# facade.
13. Wire one managed use case to V2 behind a feature flag.
14. Expand with MP3 output and HDR experiments only after the first slice is stable.

## Open Architecture Decisions

- Whether V2 should be separate projects immediately or separate namespaces/folders inside the current native projects.
- Whether the initial desktop source should use Windows Graphics Capture, DXGI Desktop Duplication, or support both behind one source interface.
- Whether C# should expose "audio enabled" as a simple bool or a richer `AudioTrackMode`.
- Whether managed volume should be exposed as dB directly or as UI percent mapped by the application layer.
- Whether the sink queue should be a single queue for all streams or separate queues merged by timestamp.
- Whether Media Foundation errors should be surfaced as HRESULTs directly in managed results or translated to app-level error codes first.
- Whether preview callbacks should be part of the recording pipeline or a separate observer sink.

## Initial Recommendation

Start with V2 as a separate namespace and folder boundary inside the current native projects. That reduces build-system churn while the design is still taking shape. Keep the API exports clearly named as V2 and build the C# facade as a separate managed implementation so the current recorder can remain untouched until the new vertical slice proves itself.

For the first workflow, use these defaults:

- Desktop video source: selected monitor plus optional physical-pixel capture area.
- Audio source: WASAPI loopback on the default render endpoint.
- Output: MP4.
- Video codec: H.264.
- Audio codec: AAC when audio is armed.
- Runtime audio command: mute/unmute, implemented by an audio gate that writes silence while muted.
- Runtime volume command: source-specific recording gain, implemented by an audio gain processor and not by changing Windows endpoint volume.
- Pause/resume: recording clock excludes paused time and drops captured samples while paused.
- HDR policy: Auto, currently resolving to SDR output for H.264 MP4 until HDR investigation is complete.
