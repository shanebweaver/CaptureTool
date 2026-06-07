# PRD 002: CaptureInterop V2 Native API and Managed Facade

## Status

Draft

## Related Documents

- [CaptureInterop V2 Architecture Plan](../architecture/capture-v2-architecture.md)
- [PRD 001: CaptureInterop V2 Core Pipeline](001-capture-v2-core-pipeline.md)

## Summary

Build the CaptureInterop V2 boundary between the native capture pipeline and managed Capture Tool code. This PRD covers the exported C ABI, opaque native handles, versioned DTOs, error reporting, C# options model, async managed facade, and callback ownership rules.

The boundary must make the V2 recorder usable from C# without exposing C++ classes, COM objects, STL containers, exceptions, or ambiguous object lifetimes. Native code remains authoritative for device, codec, graph, and file-output validation. Managed code provides application-friendly options, lifecycle methods, async control flow, and safe ownership of native handles and delegates.

## Problem

The current recorder interop surface is workflow-specific and singleton-oriented. It exposes individual P/Invoke functions for a fixed recording path, relies on global callback setters, and has limited structured diagnostics. That shape makes it hard to evolve CaptureInterop V2 toward multiple sources, richer output plans, versioned configuration, and safe managed consumption.

CaptureInterop V2 needs a boundary that can support future growth while preserving a simple first workflow:

- Create one recorder handle from managed code.
- Start desktop MP4 recording with optional system audio.
- Pause, resume, mute, adjust audio gain, and stop.
- Report stable result codes and rich diagnostics.
- Register optional callbacks without making recording depend on callbacks.
- Dispose safely even when the app is closing or a recording fails.

Without a stable native API and managed facade, V2 risks coupling managed application code directly to native implementation details and repeating the ownership issues that media pipelines tend to punish.

## Goals

- Define a stable exported C ABI for CaptureInterop V2 recorder lifecycle and runtime controls.
- Represent native recorder and callback registrations with opaque handles.
- Require versioned, size-prefixed DTOs for every exported struct.
- Return stable result codes from every native export and expose detailed diagnostics separately.
- Define clear string, array, buffer, callback, and handle ownership rules.
- Provide a managed C# options model that expresses capture intent without leaking native DTO layout.
- Provide an async C# facade that serializes recorder commands and is safe for UI callers.
- Root managed delegates for the full native callback registration lifetime.
- Keep the boundary compatible with the V2 core pipeline from PRD 001.

## Non-Goals

- Implement the V2 core pipeline.
- Implement Windows Graphics Capture, WASAPI, or Media Foundation components.
- Implement H.264, AAC, MP3, or HDR processing.
- Replace the current `IScreenRecorder` production path.
- Design UI screens or user-facing copy.
- Support non-Windows platforms in the first implementation.
- Expose arbitrary native graph composition to managed code.

## Users

Primary users:

- Managed infrastructure developers building the C# capture facade.
- Native capture developers implementing the exported API boundary.

Secondary users:

- Application developers wiring V2 behind a feature flag or adapter.
- Test authors validating interop behavior and lifetime rules.
- Future developers extending source, output, and callback DTOs.

## Scope

### In Scope

- V2 export header and implementation files.
- Opaque recorder handle lifecycle.
- Opaque callback registration handle lifecycle.
- Versioned native DTOs for recorder config, sources, output, encoding, tone mapping, controls, stop results, callbacks, and diagnostics.
- Stable native result codes.
- Last-error and detailed diagnostics retrieval.
- Managed `SafeHandle` wrappers.
- Managed P/Invoke declarations.
- Managed options records/classes and validation.
- Async managed recorder facade.
- Callback registration, unregistration, rooting, and exception handling.
- Unit and interop tests for boundary behavior.

### Out of Scope

- Real capture-device implementations unless needed as thin integration stubs.
- Full app feature-flag rollout.
- Native plugin loading or dynamic capability discovery beyond the first DTO version.
- Cross-process recording control.
- Binary compatibility with the existing non-V2 recorder exports.
- Public NuGet or SDK packaging.

## Requirements

### Native Export Shape

The native API must expose a small, explicit recorder lifecycle through `extern "C"` functions exported from `CaptureInterop.dll`.

Required exports:

```cpp
extern "C"
{
    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_GetApiVersion(
        CtCaptureV2_ApiVersion* outVersion) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_CreateRecorder(
        CtCaptureV2_RecorderHandle* outHandle) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_DestroyRecorder(
        CtCaptureV2_RecorderHandle handle) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_Start(
        CtCaptureV2_RecorderHandle handle,
        const CtCaptureV2_Config* config) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_Pause(
        CtCaptureV2_RecorderHandle handle) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_Resume(
        CtCaptureV2_RecorderHandle handle) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_SetAudioMuted(
        CtCaptureV2_RecorderHandle handle,
        uint32_t sourceId,
        uint8_t muted) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_SetAudioGain(
        CtCaptureV2_RecorderHandle handle,
        uint32_t sourceId,
        float gainDb) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_Stop(
        CtCaptureV2_RecorderHandle handle,
        CtCaptureV2_StopResult* result) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_GetLastError(
        CtCaptureV2_RecorderHandle handle,
        CtCaptureV2_ErrorInfo* errorInfo,
        char16_t* messageBuffer,
        uint32_t messageBufferLength,
        uint32_t* requiredMessageLength) noexcept;
}
```

Acceptance criteria:

- Every exported function returns `int32_t`.
- `0` represents success.
- Non-zero values map to stable `CtCaptureV2_ResultCode` values.
- Every exported function is marked `noexcept`.
- No C++ exception crosses the export boundary.
- No C++ class, STL type, COM interface, WinRT type, or ownership-bearing native pointer appears in an exported DTO.
- Null pointer validation happens before dereferencing export arguments.
- Native implementation catches standard, WIL, COM, and unknown exceptions at the boundary and converts them to stable result codes plus diagnostics.
- The calling convention is defined by a single macro and mirrored in C# P/Invoke declarations.
- The V2 export names are distinct from the current recorder exports.

### Opaque Handles

Native object identity must cross the ABI only through opaque handles.

Required handle types:

```cpp
typedef struct CtCaptureV2_Recorder_t* CtCaptureV2_RecorderHandle;
typedef struct CtCaptureV2_CallbackRegistration_t* CtCaptureV2_CallbackRegistrationHandle;
```

Acceptance criteria:

- `CtCaptureV2_CreateRecorder` returns one recorder handle through an output parameter.
- `CtCaptureV2_DestroyRecorder` releases the native recorder object associated with the handle.
- Managed code never deletes, dereferences, casts, or inspects a native handle.
- Managed code owns handles through `SafeHandle` subclasses.
- Destroying a null recorder handle is a stable no-op success.
- Destroying the same live recorder twice is prevented by managed `SafeHandle` ownership.
- The native implementation detects obviously invalid handles where practical and returns a stable invalid-handle result.
- A recorder handle owns zero or one active recording session.
- A stopped or failed session is not restarted; a subsequent `Start` creates a new native session under the same recorder handle only if the recorder has returned to an idle state.
- `DestroyRecorder` is a final cleanup path, not the normal way to complete output. The managed facade must call `StopAsync` before destroy when a recording is active.

### Versioned DTOs

Every exported struct must be size-prefixed and versioned.

Common layout rules:

- The first field is `uint32_t size`.
- The second field is `uint32_t version`.
- Fixed-width integer types are used for every numeric field.
- ABI booleans are `uint8_t`, where `0` is false and `1` is true.
- Enums have fixed underlying `int32_t` values.
- Arrays use pointer plus count.
- Strings use UTF-16 pointers unless a DTO explicitly documents UTF-8.
- Reserved fields must be zero on input.
- New fields are appended, not inserted or reordered.
- Native code must ignore trailing fields it does not understand when `size` is smaller than the newest struct.
- Native code must reject unsupported `version` values with a stable version-mismatch result.

Required first-version DTOs:

```cpp
struct CtCaptureV2_Config
{
    uint32_t size;
    uint32_t version;
    const CtCaptureV2_SourceConfig* sources;
    uint32_t sourceCount;
    CtCaptureV2_OutputConfig output;
    CtCaptureV2_ToneMappingConfig toneMapping;
    CtCaptureV2_ControlConfig controls;
    CtCaptureV2_CallbackConfig callbacks;
    uint32_t reserved;
};

struct CtCaptureV2_SourceConfig
{
    uint32_t size;
    uint32_t version;
    uint32_t sourceId;
    int32_t sourceKind;
    CtCaptureV2_Rect captureRect;
    void* platformHandle;
    uint8_t enabled;
    uint8_t reserved0;
    uint16_t reserved1;
};

struct CtCaptureV2_OutputConfig
{
    uint32_t size;
    uint32_t version;
    const char16_t* outputPath;
    int32_t containerFormat;
    CtCaptureV2_VideoEncodingConfig video;
    CtCaptureV2_AudioEncodingConfig audio;
};

struct CtCaptureV2_ControlConfig
{
    uint32_t size;
    uint32_t version;
    uint8_t startMuted;
    uint8_t reserved0;
    uint16_t reserved1;
    const CtCaptureV2_AudioGainConfig* audioGains;
    uint32_t audioGainCount;
};
```

Acceptance criteria:

- The DTO set can represent the first vertical slice: desktop source, optional system audio source, MP4 output, H.264 video request, optional AAC audio request, initial mute, and initial source gain.
- Native code copies all config data needed after `Start` returns.
- Native code does not store borrowed managed string, array, or DTO pointers after an export returns.
- DTO validation rejects missing required struct size, unsupported version, invalid enum values, invalid counts, null required pointers, non-zero reserved fields, and unsupported source/output combinations.
- DTO validation returns structured validation errors rather than relying only on a generic failure code.
- A native helper or documented initializer exists for each DTO so C++ tests can initialize version and size consistently.
- Managed marshaling code initializes every DTO `size` and `version` field explicitly.

### String, Array, and Buffer Ownership

The ABI must document whether each pointer is borrowed or owned.

Acceptance criteria:

- Input strings are borrowed for the duration of the native call.
- Input arrays are borrowed for the duration of the native call.
- Native code copies any input value needed asynchronously or after the call returns.
- Output buffers use caller-allocated pointer plus capacity and return required length when capacity is insufficient.
- Native code never frees managed memory.
- Managed code never frees native memory except through explicit V2 destroy or unregister exports.
- UTF-16 string lengths are counted in code units, including clear rules for whether the null terminator is included.
- `GetLastError` supports a sizing call where `messageBuffer` is null and `messageBufferLength` is zero.

### Result Codes and Error Reporting

Every native operation must return a stable result code and preserve rich diagnostics for managed callers.

Minimum result codes:

- `Success`.
- `InvalidArgument`.
- `InvalidHandle`.
- `InvalidState`.
- `UnsupportedVersion`.
- `UnsupportedOperation`.
- `ValidationFailed`.
- `NotFound`.
- `AlreadyStarted`.
- `AlreadyStopped`.
- `BufferTooSmall`.
- `NativeFailure`.
- `ExternalApiFailure`.
- `CallbackRegistrationFailed`.
- `CallbackInvocationFailed`.

Detailed error DTO:

```cpp
struct CtCaptureV2_ErrorInfo
{
    uint32_t size;
    uint32_t version;
    int32_t resultCode;
    int32_t errorCode;
    int32_t nativeStatus;
    int32_t stage;
    const char* component;
    const char* operation;
};
```

Acceptance criteria:

- Result codes are app-level and stable across implementation refactors.
- HRESULT or native status is optional detail, not the primary managed contract.
- Last-error state is kept per recorder handle.
- Create-recorder failures that occur before a handle exists can be read through a documented thread-local or process-level error path, or are fully represented by the create result code.
- Error details include result code, native status, component, operation, stage, and human-readable message where available.
- Managed exceptions include result code, native status, component, operation, and message.
- `Stop` returns a `CtCaptureV2_StopResult` even when finalization encounters a recoverable or reportable failure.
- Validation can return multiple errors when feasible; otherwise it returns the first meaningful validation failure and records details.
- No native export writes to stdout, stderr, or UI dialogs for error reporting.

### Callback Registration and Ownership

Callbacks are optional observer paths. Recording must not require callback registration.

Required callback APIs:

```cpp
CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_RegisterCallbacks(
    CtCaptureV2_RecorderHandle recorder,
    const CtCaptureV2_CallbackConfig* callbacks,
    CtCaptureV2_CallbackRegistrationHandle* outRegistration) noexcept;

CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_UnregisterCallbacks(
    CtCaptureV2_CallbackRegistrationHandle registration) noexcept;
```

Callback DTO shape:

```cpp
typedef void (CTCAPTUREV2_CALL* CtCaptureV2_EventCallback)(
    const CtCaptureV2_Event* event,
    void* userData);

struct CtCaptureV2_CallbackConfig
{
    uint32_t size;
    uint32_t version;
    CtCaptureV2_EventCallback eventCallback;
    void* eventUserData;
    uint32_t eventMask;
    uint32_t reserved;
};
```

Acceptance criteria:

- Callback registrations return opaque registration handles.
- Registration handles are owned by managed `SafeHandle` wrappers.
- Unregistering a callback synchronously prevents future callback invocation for that registration.
- If native callback dispatch can already be in progress, unregister waits for in-flight invocation to drain or documents and exposes an explicit drain mechanism.
- Native code does not invoke managed callbacks while holding internal graph locks.
- Native callbacks are invoked on native worker or source threads, never assumed to be the UI thread.
- Managed delegates remain rooted until native unregister completes.
- Managed callback payloads are copied before being exposed to application code if the native payload lifetime is limited to the callback invocation.
- Exceptions thrown by managed event handlers do not escape into native code.
- The managed facade reports callback exceptions through a controlled error/event path.
- Callback registration is allowed before `Start` and during an idle recorder state.
- Callback registration after `DestroyRecorder` returns invalid handle.
- Stopping or destroying a recorder unregisters remaining callback registrations before releasing session-owned objects.

### Managed Options Model

Managed code must expose application-friendly options rather than native DTO wrappers.

Required shape:

```csharp
public sealed record CapturePipelineOptions
{
    public required IReadOnlyList<CaptureSourceOptions> Sources { get; init; }
    public required CaptureOutputOptions Output { get; init; }
    public CaptureToneMappingOptions ToneMapping { get; init; } = CaptureToneMappingOptions.Auto;
    public CaptureControlOptions Controls { get; init; } = CaptureControlOptions.Default;
}

public abstract record CaptureSourceOptions
{
    public required CaptureSourceId SourceId { get; init; }
    public bool Enabled { get; init; } = true;
}

public sealed record DesktopCaptureSourceOptions : CaptureSourceOptions
{
    public nint MonitorHandle { get; init; }
    public System.Drawing.Rectangle CaptureArea { get; init; }
}

public sealed record SystemAudioCaptureSourceOptions : CaptureSourceOptions
{
    public bool Armed { get; init; } = true;
}

public sealed record CaptureOutputOptions
{
    public required string OutputPath { get; init; }
    public required CaptureContainerFormat Container { get; init; }
    public VideoEncodingOptions? Video { get; init; }
    public AudioEncodingOptions? Audio { get; init; }
}
```

Acceptance criteria:

- The managed options model validates obvious app-level errors before marshaling.
- Managed validation rejects empty output paths, duplicate source ids, invalid rectangles, missing output streams, unsupported gain ranges, and invalid enum values.
- Native validation remains authoritative for device handles, codec support, file-system access, and graph capability checks.
- The managed options model does not expose DTO `size`, `version`, `reserved`, raw pointer, or native enum layout details to application code.
- Options records are immutable after creation.
- The facade maps managed source ids to native `uint32_t` source ids deterministically.
- Defaults are explicit for video codec, audio codec, initial mute, gain, tone mapping, and diagnostics.
- The model can represent the current app workflow without requiring UI changes.

### Async Managed Facade

The managed facade must provide an async API that is safe for UI callers and native lifecycle constraints.

Required shape:

```csharp
public sealed class CaptureRecorder : IAsyncDisposable
{
    public Task StartAsync(
        CapturePipelineOptions options,
        CancellationToken cancellationToken = default);

    public Task PauseAsync(CancellationToken cancellationToken = default);

    public Task ResumeAsync(CancellationToken cancellationToken = default);

    public Task SetAudioMutedAsync(
        CaptureSourceId sourceId,
        bool muted,
        CancellationToken cancellationToken = default);

    public Task SetAudioGainAsync(
        CaptureSourceId sourceId,
        float gainDb,
        CancellationToken cancellationToken = default);

    public Task<CaptureStopResult> StopAsync(
        CancellationToken cancellationToken = default);
}
```

Acceptance criteria:

- The facade owns exactly one native recorder handle.
- Native commands for a recorder are serialized by the facade.
- Async methods do not block the UI thread while native calls perform potentially slow work.
- Cancellation before native dispatch prevents the native call.
- Cancellation after native dispatch does not abandon native cleanup; stop and dispose still complete deterministically.
- Invalid managed state transitions return managed exceptions or stable operation results consistently.
- `StopAsync` is idempotent or returns a stable already-stopped result.
- `DisposeAsync` unregisters callbacks, stops active recording when needed, destroys the recorder handle, and suppresses finalization.
- SafeHandle finalization is a last-resort cleanup path and does not replace explicit `DisposeAsync`.
- Managed code translates native result codes to `CaptureNativeException`, `CaptureValidationException`, or operation result objects according to severity.
- The facade can be adapted to the existing `IScreenRecorder` interface while V2 is behind a feature flag.

### Managed P/Invoke and Marshaling

The managed infrastructure layer must isolate unsafe marshaling details.

Acceptance criteria:

- P/Invoke declarations are internal.
- Native DTO structs are internal and used only by the interop layer.
- Native DTO structs use explicit layout where needed to preserve ABI layout.
- The interop layer pins arrays and strings only for the duration of native calls.
- Any unmanaged allocations made for marshaling are released in `finally` blocks or owned by safe wrappers.
- The interop layer verifies runtime API version compatibility before first recorder use.
- The interop layer centralizes native result-code translation.
- Tests verify struct sizes where practical.

### State and Lifecycle Contract

The boundary must align with the V2 core state machine.

Acceptance criteria:

- `Start` is valid only while the recorder is idle.
- `Pause` is valid only while recording.
- `Resume` is valid only while paused.
- `SetAudioMuted` and `SetAudioGain` are valid while recording or paused.
- `Stop` is valid while recording or paused.
- `DestroyRecorder` is valid from any recorder state, but normal managed disposal calls `Stop` first for active sessions.
- Invalid transitions return `InvalidState` or a more specific stable result code.
- Native state transitions are serialized per recorder handle.
- Concurrent managed calls cannot interleave native recorder state transitions.

### Diagnostics and Observability

The boundary should preserve enough information for app telemetry and developer debugging.

Acceptance criteria:

- Stop results include final state, failure stage, duration, dropped video frame count, audio discontinuity count, and final output path when available.
- Error stages map to validation, graph build, source start, sink write, pause, resume, runtime control, stop, flush, finalize, callback registration, callback dispatch, and teardown.
- Managed exceptions and stop results can be logged without inspecting native memory.
- Diagnostic strings are safe for UI logging but do not expose unmanaged pointer values as primary identifiers.
- The first implementation can leave counters at zero when the core does not populate them yet, but the DTO fields must exist or be planned through a versioned extension.

## User Stories

### Managed Developer: Start Recording Through Options

As a managed developer, I want to create `CapturePipelineOptions` and call `StartAsync` so that application code can express recording intent without knowing native DTO layout.

Acceptance criteria:

- A desktop MP4 options object maps to a valid `CtCaptureV2_Config`.
- Managed validation catches missing output path before P/Invoke.
- Native validation catches unsupported source or output details after P/Invoke.

### Native Developer: Evolve DTOs Safely

As a native developer, I want all DTOs to include size and version so that new fields can be added without breaking older managed callers.

Acceptance criteria:

- Adding an appended field does not change the meaning of earlier fields.
- Unsupported versions return `UnsupportedVersion`.
- Smaller known struct sizes can be accepted when required fields are present.

### Application Developer: Dispose Without Leaks

As an application developer, I want `CaptureRecorder.DisposeAsync` to release native resources and callbacks so that app shutdown does not leak native capture objects.

Acceptance criteria:

- Active recordings are stopped before handle destruction during explicit disposal.
- Registered callbacks are unregistered before managed delegates are released.
- SafeHandle finalization does not throw.

### Test Author: Verify Callback Lifetime

As a test author, I want callback registration to have a concrete handle so that I can prove no callback fires after unregistration.

Acceptance criteria:

- A test can register a callback, trigger native events, unregister, trigger more events, and observe no additional managed callback invocations.
- Managed callback delegates remain alive until unregister completes.
- Managed callback exceptions do not crash native code.

## Technical Constraints

- C++20.
- C# on the current project target framework.
- Windows-first ABI.
- No new third-party dependencies.
- WIL should be used for native cleanup and HRESULT handling where useful.
- The ABI must be dependency-light and callable from plain C-compatible consumers.
- The managed facade must keep unsafe code isolated to the infrastructure layer.
- The V2 boundary must not require UI thread affinity.

## Proposed Deliverables

- `CaptureInterop::V2` API boundary namespace or folder.
- `CaptureInteropV2Exports.h`.
- `CaptureInteropV2Exports.cpp`.
- Native DTO headers for V2 config, callbacks, errors, and stop results.
- Native result-code enum.
- Recorder and callback opaque handle wrappers.
- Boundary exception/result conversion helpers.
- Managed native-method declarations.
- Managed native DTO structs.
- Managed `SafeHandle` implementations for recorder and callback registrations.
- Managed options records/classes.
- Managed async `CaptureRecorder` facade.
- Managed error and validation exception types.
- Interop tests for handle lifecycle, DTO validation, result-code mapping, callback rooting, and async facade state.

## Testing Requirements

Native API tests must cover:

- API version query.
- Create and destroy recorder handle.
- Destroy null handle.
- Invalid handle behavior where practical.
- Start with null config.
- Start with unsupported config version.
- Start with missing output path.
- Start with duplicate source ids.
- Pause, resume, runtime control, and stop invalid transitions.
- Last-error retrieval with exact buffer and too-small buffer.
- Stop result population for success and failure paths.
- Callback registration and unregistration.
- No callbacks after unregister returns.
- No callback while internal graph locks are held, verified through deadlock-oriented tests where feasible.

Managed tests must cover:

- Options validation.
- DTO mapping and struct size checks.
- Result-code to exception mapping.
- SafeHandle release behavior.
- Serialized async command execution.
- Cancellation before native dispatch.
- Dispose with idle recorder.
- Dispose with active recorder.
- Delegate rooting until callback unregister.
- Managed callback exception containment.

## Targeted PR Chunks

Each chunk should be small enough for a single focused pull request. Later chunks may adjust names or DTO fields if earlier implementation discoveries require it, but each PR should avoid mixing native ABI, managed facade, callbacks, and feature rollout work unless explicitly listed.

Default execution rule: one chunk equals one PR. Combining adjacent chunks should require an explicit reason, such as a trivial mechanical follow-up with no additional behavior.

### PR 002-01: Native ABI Foundation

Objective:

- Establish the V2 ABI naming, calling convention, versioning, and result-code foundation without recorder lifecycle behavior.

Deliverables:

- `CaptureInteropV2Exports.h`.
- `CTCAPTUREV2_API` and `CTCAPTUREV2_CALL` macros.
- `CtCaptureV2_ResultCode` enum.
- `CtCaptureV2_ApiVersion` DTO.
- `CtCaptureV2_GetApiVersion` export.
- Native tests for version query and result-code stability.

Acceptance criteria:

- The V2 export header compiles from C++.
- `CtCaptureV2_GetApiVersion` returns success and a non-zero major version.
- Export names are distinct from existing recorder exports.
- No recorder handle, config DTO, managed facade, or callback code is introduced in this PR.

Suggested tests:

- Native API version query returns expected major/minor.
- Result-code numeric values are stable.
- Export header can be included without pulling in C++ classes or STL ownership types.

### PR 002-02: Recorder Handle Lifecycle

Objective:

- Add opaque native recorder handles and minimal managed ownership without implementing recording commands.

Deliverables:

- `CtCaptureV2_RecorderHandle` type.
- Native recorder wrapper with idle state only.
- `CtCaptureV2_CreateRecorder` export.
- `CtCaptureV2_DestroyRecorder` export.
- Basic invalid/null handle behavior.
- Internal managed P/Invoke declarations for version/create/destroy.
- Managed `CaptureRecorderSafeHandle`.

Acceptance criteria:

- Native tests can create and destroy a recorder handle.
- Destroying a null handle returns success.
- Destroying an obviously invalid handle returns a stable invalid-handle result where practical.
- Managed tests can create and dispose a `SafeHandle`.
- No `Start`, `Stop`, config DTOs, callbacks, or async facade are included in this PR.

Suggested tests:

- Native create/destroy success.
- Native destroy null success.
- Managed `SafeHandle.ReleaseHandle` calls destroy exactly once.
- Managed version compatibility check can run before handle creation.

### PR 002-03: Native DTO Contracts and Validation

Objective:

- Add first-version native DTOs and native validation helpers without wiring lifecycle commands.

Deliverables:

- `CtCaptureV2_Config`.
- `CtCaptureV2_SourceConfig`.
- `CtCaptureV2_OutputConfig`.
- `CtCaptureV2_VideoEncodingConfig`.
- `CtCaptureV2_AudioEncodingConfig`.
- `CtCaptureV2_ToneMappingConfig`.
- `CtCaptureV2_ControlConfig`.
- `CtCaptureV2_AudioGainConfig`.
- `CtCaptureV2_StopResult`.
- DTO initializer helpers for native tests.
- Native validation helper for size, version, reserved fields, required pointers, enum ranges, duplicate source ids, output path, and gain range.

Acceptance criteria:

- Every exported DTO starts with `uint32_t size` and `uint32_t version`.
- All ABI booleans are `uint8_t`.
- Enums have fixed `int32_t` values.
- Validation rejects unsupported versions and missing required fields.
- Validation can represent the first desktop MP4 workflow config.
- No managed options model, P/Invoke mapping, lifecycle exports, or callbacks are included in this PR.

Suggested tests:

- Valid first-slice config passes validation.
- Missing size/version fails.
- Unsupported version fails.
- Non-zero reserved field fails.
- Duplicate source ids fail.
- Empty output path fails.
- Gain outside the supported range fails.

### PR 002-04: Managed Options Model

Objective:

- Add application-friendly managed options and validation without unsafe native marshaling.

Deliverables:

- `CapturePipelineOptions`.
- `CaptureSourceOptions`.
- `DesktopCaptureSourceOptions`.
- `SystemAudioCaptureSourceOptions`.
- `CaptureOutputOptions`.
- `VideoEncodingOptions`.
- `AudioEncodingOptions`.
- `CaptureToneMappingOptions`.
- `CaptureControlOptions`.
- `CaptureSourceId`.
- Managed validation helpers.

Acceptance criteria:

- Managed options are immutable records/classes as appropriate.
- Managed validation rejects empty output paths, duplicate source ids, invalid rectangles, missing output streams, invalid enum values, and gain outside supported range.
- Managed options do not expose native DTO `size`, `version`, `reserved`, raw pointer, or native enum layout details.
- No native DTO marshaling, lifecycle commands, async facade, or callbacks are included in this PR.

Suggested tests:

- Valid desktop MP4 options pass managed validation.
- Duplicate source ids fail.
- Invalid capture rectangle fails.
- Missing output path fails.
- Unsupported gain range fails.
- Defaults for codecs, tone mapping, mute, and gain are explicit.

### PR 002-05: Managed DTO Marshaling

Objective:

- Map managed options to internal native DTOs safely, with pinned strings and arrays scoped to a call.

Deliverables:

- Internal managed native DTO structs.
- Internal enum mapping helpers.
- Marshaling builder for `CtCaptureV2_Config`.
- Scoped pinning/allocation helper if needed.
- Struct layout and size tests where practical.

Acceptance criteria:

- Native DTO structs are internal to the infrastructure layer.
- DTO structs initialize `size` and `version` explicitly.
- Strings and arrays are pinned or allocated only for the duration of the marshaling scope.
- Managed code can build a native config from a valid `CapturePipelineOptions`.
- Native memory ownership rules are documented in code comments or test names.
- No `Start` P/Invoke call, async facade, or callbacks are included in this PR.

Suggested tests:

- Managed options map to expected native enum values.
- Source ids map deterministically to `uint32_t`.
- Output path maps as UTF-16.
- Struct sizes match expected layout on the target platform.
- Marshaling scope releases pins or unmanaged allocations.

### PR 002-06: Native Lifecycle Command Exports

Objective:

- Add lifecycle exports against a native test recorder/session abstraction, without the managed async facade.

Deliverables:

- `CtCaptureV2_Start`.
- `CtCaptureV2_Pause`.
- `CtCaptureV2_Resume`.
- `CtCaptureV2_SetAudioMuted`.
- `CtCaptureV2_SetAudioGain`.
- `CtCaptureV2_Stop`.
- Native recorder state machine integration or test double.
- Stop result population for success and invalid-state paths.

Acceptance criteria:

- `Start` copies config data needed after the call returns.
- Invalid state transitions return stable result codes.
- `Stop` returns a populated `CtCaptureV2_StopResult`.
- Runtime audio commands target source id.
- Exports catch exceptions and return stable result codes.
- No managed async facade or callback registration is included in this PR.

Suggested tests:

- Start from idle succeeds with a valid DTO.
- Start while already recording returns `AlreadyStarted` or `InvalidState`.
- Pause/resume valid transitions pass.
- Pause while idle fails.
- Audio mute/gain while idle fails.
- Stop while recording succeeds.
- Stop while idle returns stable already-stopped or invalid-state result according to final policy.

### PR 002-07: Native Error Reporting

Objective:

- Add detailed native error storage and retrieval through `GetLastError`.

Deliverables:

- Per-recorder last-error storage.
- `CtCaptureV2_ErrorInfo`.
- `CtCaptureV2_GetLastError`.
- Error message sizing-call behavior.
- Boundary conversion helpers for validation failure, invalid state, native failure, and external API failure.

Acceptance criteria:

- Last-error state is stored per recorder handle.
- `GetLastError` supports a sizing call with null message buffer and zero capacity.
- Buffer-too-small returns required message length.
- Error details include result code, native status, component, operation, stage, and message where available.
- Create-recorder failure behavior is documented if no handle exists.
- No managed exception translation or callbacks are included in this PR.

Suggested tests:

- Invalid `Start` stores last error.
- Exact-size message buffer succeeds.
- Too-small message buffer returns `BufferTooSmall` and required length.
- Null handle behavior is stable.
- Successful operation clears or preserves last error according to documented policy.

### PR 002-08: Managed Result Translation

Objective:

- Translate native result codes and detailed errors into managed exceptions or result objects.

Deliverables:

- Managed result-code enum.
- `CaptureNativeException`.
- `CaptureValidationException`.
- Error retrieval helper.
- Native result translation service.
- Managed tests for error messages and metadata.

Acceptance criteria:

- Managed exceptions include result code, native status, component, operation, stage, and message.
- Validation failures map to validation-specific managed errors.
- Native failures map to native exceptions.
- Result translation is centralized.
- No async recorder facade or callback handling is included in this PR.

Suggested tests:

- Native `InvalidArgument` maps to expected managed exception.
- Validation failure maps to `CaptureValidationException`.
- Buffer-too-small path is invisible to callers except through successful retrieval.
- Exceptions can be logged without reading native memory.

### PR 002-09: Managed Async Recorder Facade

Objective:

- Add the managed `CaptureRecorder` facade and serialize commands over the native lifecycle exports.

Deliverables:

- `CaptureRecorder`.
- Async `StartAsync`, `PauseAsync`, `ResumeAsync`, `SetAudioMutedAsync`, `SetAudioGainAsync`, `StopAsync`.
- Per-recorder command serialization.
- Cancellation-before-dispatch behavior.
- `DisposeAsync`.
- Idle/recording/paused managed state tracking if needed.

Acceptance criteria:

- The facade owns exactly one native recorder `SafeHandle`.
- Commands for one recorder do not interleave.
- Cancellation before native dispatch prevents the native call.
- Cancellation after native dispatch does not abandon native cleanup.
- `DisposeAsync` stops active recording when needed and destroys the recorder handle.
- The facade does not implement callback registration in this PR.

Suggested tests:

- Start/pause/resume/stop through facade succeeds against native test implementation.
- Concurrent managed commands are serialized.
- Cancellation before dispatch prevents native call.
- Dispose with idle recorder releases handle.
- Dispose with active recorder calls stop before destroy.
- Invalid transitions produce consistent managed errors or results.

### PR 002-10: Native Callback Registration

Objective:

- Add native callback registration and unregister exports, with RAII callback ownership.

Deliverables:

- `CtCaptureV2_CallbackRegistrationHandle`.
- `CtCaptureV2_CallbackConfig`.
- `CtCaptureV2_Event`.
- `CtCaptureV2_RegisterCallbacks`.
- `CtCaptureV2_UnregisterCallbacks`.
- Native callback registry and RAII token.
- Native test hook for triggering events.

Acceptance criteria:

- Callback registration returns an opaque registration handle.
- Unregister synchronously prevents future invocations for that registration.
- Native code does not invoke callbacks while holding graph locks.
- Callback registration after recorder destroy returns invalid handle.
- Stopping or destroying a recorder unregisters remaining callback registrations.
- No managed delegate rooting is included in this PR.

Suggested tests:

- Register callback and receive native test event.
- Unregister then trigger event and receive no callback.
- Double unregister is stable or prevented by handle ownership.
- Callback is not invoked while a test lock is held.

### PR 002-11: Managed Callback Ownership

Objective:

- Add managed callback registration wrappers, delegate rooting, and exception containment.

Deliverables:

- Managed callback registration `SafeHandle`.
- Managed event/callback subscription API on `CaptureRecorder`.
- Delegate rooting for registration lifetime.
- Managed callback payload copy.
- Managed callback exception containment.

Acceptance criteria:

- Delegates remain rooted until native unregister completes.
- No callback fires after managed registration disposal completes.
- Managed callback exceptions do not escape into native code.
- Callback payloads exposed to application code do not reference invalid native memory.
- Callback disposal happens before recorder handle disposal.

Suggested tests:

- Callback delegate is not collected while registration is active.
- Callback fires before unregister.
- Callback does not fire after unregister.
- Throwing managed callback does not crash native code.
- Recorder disposal unregisters active callback registrations.

### PR 002-12: Existing App Adapter Readiness

Objective:

- Prepare V2 for app integration without switching production behavior.

Deliverables:

- Adapter from V2 facade shape toward the existing screen recorder abstraction, if needed.
- Feature-flag-ready registration or dependency injection hook.
- Minimal smoke tests proving current production path remains unchanged.
- Documentation notes for how the first workflow PRD will consume the facade.

Acceptance criteria:

- V2 can be registered or constructed without replacing the current recorder by default.
- Existing recorder tests still pass.
- The adapter does not add UI behavior or full rollout.
- No real desktop/audio/sink implementation is required in this PR.

Suggested tests:

- Dependency injection can resolve existing recorder unchanged.
- V2 facade can be constructed in isolation.
- Feature flag off path uses current recorder.
- Feature flag on path can be wired to a test/dummy V2 recorder.

Implementation note:

- `AddWindowsCaptureDomains` keeps `WindowsScreenRecorder` as the default `IScreenRecorder`.
- Workflow PRDs can opt into V2 by configuring `WindowsCaptureInfrastructureOptions.UseCaptureV2ScreenRecorder` after a feature flag is evaluated by the app layer.
- The first workflow integration should replace the placeholder monitor geometry in `CaptureV2ScreenRecorderAdapter` with source metadata from the desktop-source PRD before enabling production capture.

## Open Questions

- Should `CtCaptureV2_GetLastError` support handle-null thread-local create errors, or should create failures be fully represented by result code alone?
- Should callback registration be separate from `Start` for every callback type, or should first-version callbacks be configured only before start?
- Should managed `StopAsync` return an already-stopped result or throw for already-stopped calls?
- Should `DestroyRecorder` attempt to finalize active output as a native fallback, or should explicit managed disposal be the only guaranteed finalization path?
- Should first-version DTOs use UTF-16 only, or include explicit UTF-8 variants for future non-Windows consumers?

## Definition of Done

- The V2 native API boundary compiles and exports the required functions.
- The managed facade can create, start, control, stop, and dispose a V2 recorder through opaque handles.
- Every exported DTO has size and version fields.
- Every native export returns a stable result code and catches boundary exceptions.
- Detailed error reporting works from managed code.
- Managed callbacks remain rooted while registered and are not invoked after unregister completes.
- Boundary tests pass without requiring real capture devices.
- The architecture document is updated if implementation decisions materially change the planned V2 boundary.
