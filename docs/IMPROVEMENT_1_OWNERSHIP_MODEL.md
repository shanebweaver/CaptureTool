# Implementation: Improvement Area #1 - Mixed Ownership Model

## Summary

This document describes the implementation of the first improvement area from SESSION_ARCHITECTURE_ANALYSIS.md: **Mixed Ownership Model (Factory References)**.

## Problem Addressed

The `WindowsGraphicsCaptureSession` previously stored raw pointers to factory objects (`IMediaClockFactory*`, `IAudioCaptureSourceFactory*`, etc.), creating an implicit lifetime dependency. The session would use these factories during `Start()` to create dependencies.

**Issues:**
- Unclear ownership semantics (raw pointers, non-owning references)
- Potential for dangling pointers if factory outlives session
- Complex initialization logic mixed into `Start()` method
- Violates "Explicit Ownership and Lifetime Management" principle

## Solution Implemented (Option B)

Implemented **Option B** from the analysis: "Don't store factories at all - create all sources during construction"

The factory now creates all dependencies and transfers ownership to the session. The session never needs to hold factory references.

## Changes Made

### 1. WindowsGraphicsCaptureSession.h

**Before:**
```cpp
WindowsGraphicsCaptureSession(
    const CaptureSessionConfig& config,
    IMediaClockFactory* mediaClockFactory,
    IAudioCaptureSourceFactory* audioCaptureSourceFactory,
    IVideoCaptureSourceFactory* videoCaptureSourceFactory,
    IMP4SinkWriterFactory* mp4SinkWriterFactory);

private:
    // Factories
    IMediaClockFactory* m_mediaClockFactory;
    IAudioCaptureSourceFactory* m_audioCaptureSourceFactory;
    IVideoCaptureSourceFactory* m_videoCaptureSourceFactory;
    IMP4SinkWriterFactory* m_mp4SinkWriterFactory;
```

**After:**
```cpp
WindowsGraphicsCaptureSession(
    const CaptureSessionConfig& config,
    std::unique_ptr<IMediaClock> mediaClock,
    std::unique_ptr<IAudioCaptureSource> audioCaptureSource,
    std::unique_ptr<IVideoCaptureSource> videoCaptureSource,
    std::unique_ptr<IMP4SinkWriter> sinkWriter);

private:
    // No factory storage - dependencies are received fully-initialized
```

**Removed:**
- Raw pointer members for factories
- Forward declarations for factory interfaces

**Added:**
- Documentation explaining the new ownership model
- Constructor parameters accepting `std::unique_ptr` for all dependencies

### 2. WindowsGraphicsCaptureSession.cpp

**Constructor changes:**
```cpp
// Before: Initialize factory pointers
: m_mediaClockFactory(mediaClockFactory)
, m_audioCaptureSourceFactory(audioCaptureSourceFactory)
// ... etc
, m_audioCaptureSource(nullptr)
, m_videoCaptureSource(nullptr)
, m_sinkWriter(nullptr)

// After: Transfer ownership of dependencies
: m_mediaClock(std::move(mediaClock))
, m_audioCaptureSource(std::move(audioCaptureSource))
, m_videoCaptureSource(std::move(videoCaptureSource))
, m_sinkWriter(std::move(sinkWriter))
```

**Start() method simplification:**

Removed ~40 lines of dependency creation logic:
- ❌ `m_mediaClock = m_mediaClockFactory->CreateClock()`
- ❌ `m_audioCaptureSource = m_audioCaptureSourceFactory->CreateAudioCaptureSource(...)`
- ❌ `m_videoCaptureSource = m_videoCaptureSourceFactory->CreateVideoCaptureSource(...)`
- ❌ `m_sinkWriter = m_mp4SinkWriterFactory->CreateSinkWriter()`

Added validation that dependencies were provided:
- ✅ `if (!m_mediaClock || !m_audioCaptureSource || !m_videoCaptureSource || !m_sinkWriter)`

The `Start()` method is now focused solely on:
1. Validating dependencies exist
2. Connecting the audio source as clock advancer
3. Starting the clock
4. Initializing sources
5. Initializing sink writer
6. Setting up callbacks
7. Starting capture

### 3. WindowsGraphicsCaptureSessionFactory.cpp

**Factory now creates all dependencies before creating session:**

```cpp
std::unique_ptr<ICaptureSession> CreateSession(const CaptureSessionConfig& config)
{
    // Create the media clock first
    auto mediaClock = m_mediaClockFactory->CreateClock();
    if (!mediaClock) return nullptr;

    // Create audio capture source with clock reader
    auto audioCaptureSource = m_audioCaptureSourceFactory->CreateAudioCaptureSource(mediaClock.get());
    if (!audioCaptureSource) return nullptr;

    // Create video capture source with clock reader
    auto videoCaptureSource = m_videoCaptureSourceFactory->CreateVideoCaptureSource(config, mediaClock.get());
    if (!videoCaptureSource) return nullptr;

    // Create sink writer
    auto sinkWriter = m_mp4SinkWriterFactory->CreateSinkWriter();
    if (!sinkWriter) return nullptr;

    // Create session with all dependencies - ownership is transferred
    return std::make_unique<WindowsGraphicsCaptureSession>(
        config,
        std::move(mediaClock),
        std::move(audioCaptureSource),
        std::move(videoCaptureSource),
        std::move(sinkWriter));
}
```

## Benefits Achieved

### ✅ Explicit Ownership
- Dependencies are owned by the session via `std::unique_ptr`
- No raw pointers, no ambiguity about who owns what
- Ownership transfer is explicit through `std::move`

### ✅ Clear Lifetime Semantics
- No dangling pointer risk - dependencies are owned by the session
- Session cannot outlive its dependencies (they're members)
- RAII ensures proper cleanup when session is destroyed

### ✅ Separation of Concerns
- Factory is responsible for creating dependencies
- Session is responsible for using dependencies
- Initialization logic is in the factory, not mixed into `Start()`

### ✅ Simplified Start() Method
- Reduced from ~120 lines to ~80 lines
- Removed dependency creation logic
- Clearer focus on initialization and starting capture
- Easier to understand and maintain

### ✅ Fail Fast
- If factory cannot create dependencies, session creation fails immediately
- No partially-initialized session state
- Errors are caught at construction time, not during `Start()`

### ✅ Better Testability
- Can inject mock dependencies directly into session constructor
- Don't need to mock factories anymore
- Easier to test session in isolation

## Architectural Alignment

This change aligns with the following principles from ARCHITECTURE_GOALS.md:

1. **Explicit Ownership and Lifetime Management**: Ownership is now explicit through `std::unique_ptr`
2. **Composability Over Inheritance**: Factory composes dependencies and hands them to session
3. **Separation of Concerns**: Creation logic is in factory, usage logic is in session
4. **RAII**: Dependencies are managed through smart pointers
5. **Fail Fast**: Session creation fails if dependencies cannot be created

## Future Improvements

With this foundation in place, the remaining improvements from the analysis become easier:

- **Improvement #3**: State management can be further simplified since Start() is already cleaner
- **Improvement #4**: CRITICAL_SECTION RAII wrapper can be added independently
- **Improvement #5**: Lambda lifetime contracts are already clear (session owns dependencies)

## Testing Notes

- Existing tests should continue to work with factories
- New tests can inject mock dependencies directly into session constructor
- Windows build environment required for full validation

## Migration Path

For any code that directly constructs `WindowsGraphicsCaptureSession`:

**Before:**
```cpp
auto session = std::make_unique<WindowsGraphicsCaptureSession>(
    config,
    mediaClockFactory.get(),
    audioSourceFactory.get(),
    videoSourceFactory.get(),
    sinkWriterFactory.get());
```

**After:** Use the factory instead
```cpp
auto factory = std::make_unique<WindowsGraphicsCaptureSessionFactory>(
    std::move(mediaClockFactory),
    std::move(audioSourceFactory),
    std::move(videoSourceFactory),
    std::move(sinkWriterFactory));

auto session = factory->CreateSession(config);
```

Or for tests, construct session directly with mocks:
```cpp
auto session = std::make_unique<WindowsGraphicsCaptureSession>(
    config,
    std::move(mockClock),
    std::move(mockAudioSource),
    std::move(mockVideoSource),
    std::move(mockSinkWriter));
```
