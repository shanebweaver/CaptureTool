# Session Architecture Analysis

## Purpose

This document analyzes the current capture session implementation against the architectural goals outlined in [ARCHITECTURE_GOALS.md](ARCHITECTURE_GOALS.md). It identifies areas where the code can be improved to better align with Clean Architecture principles and Rust-inspired patterns.

## Current Architecture Overview

The capture session follows a layered architecture with dependency injection:

```
ScreenRecorderImpl (Application Layer)
    ↓ uses factory
WindowsGraphicsCaptureSessionFactory (Infrastructure)
    ↓ creates
WindowsGraphicsCaptureSession (Infrastructure)
    ↓ composes
[IMediaClock, IAudioCaptureSource, IVideoCaptureSource, IMP4SinkWriter]
```

### Key Components

- **`ICaptureSession`**: Interface defining session lifecycle (Start, Stop, Pause, Resume, etc.)
- **`WindowsGraphicsCaptureSession`**: Concrete implementation for Windows Graphics Capture API
- **`ICaptureSessionFactory`**: Factory interface for creating sessions
- **`WindowsGraphicsCaptureSessionFactory`**: Concrete factory that injects dependencies
- **`ScreenRecorderImpl`**: Application-layer orchestrator
- **`CaptureSessionConfig`**: Plain data structure for configuration

## Strengths (Already Aligned with Goals)

### ✅ Dependency Inversion
- All major abstractions are interfaces (`IMediaClock`, `IAudioCaptureSource`, `IVideoCaptureSource`, `IMP4SinkWriter`)
- Concrete implementations depend on abstractions, not vice versa
- Factory pattern enables dependency injection

### ✅ Interface Segregation
- Interfaces are focused and role-based:
  - `IMediaClockReader` / `IMediaClockWriter` / `IMediaClockController` (split clock interface)
  - `ICaptureSession` has a clear, focused API
  - Callbacks are separate function pointer types

### ✅ RAII for Resource Management
- Smart pointers used throughout (`std::unique_ptr`, `wil::com_ptr`)
- Destructor calls `Stop()` to ensure cleanup
- COM objects managed with `wil::com_ptr` for ref-counting

### ✅ Factory Pattern
- `ICaptureSessionFactory` abstracts session creation
- Enables testing with mock implementations
- Constructor injection of dependencies

### ✅ Explicit Ownership
- `std::unique_ptr` for owned resources (sources, sinks, clock)
- Raw pointers for non-owning references (factories passed to session constructor)
- Move semantics in factory constructor (`std::move`)

## Areas for Improvement

### 1. Mixed Ownership Model (Factory References)

**Current Issue**: `WindowsGraphicsCaptureSession` stores raw pointers to factories, but the factories are owned by `WindowsGraphicsCaptureSessionFactory`. This creates a lifetime dependency that's not explicit.

```cpp
// In WindowsGraphicsCaptureSession.h
IMediaClockFactory* m_mediaClockFactory;              // Non-owning
IAudioCaptureSourceFactory* m_audioCaptureSourceFactory; // Non-owning
IVideoCaptureSourceFactory* m_videoCaptureSourceFactory; // Non-owning
IMP4SinkWriterFactory* m_mp4SinkWriterFactory;        // Non-owning
```

**Problem**: The session outlives the factories in some scenarios, leading to potential dangling pointers. The lifetime relationship is implicit.

**Alignment Issue**: Violates "Explicit Ownership and Lifetime Management" principle.

**Recommendation**:

**Option A**: Session should own factories (better for lifetime clarity)
```cpp
class WindowsGraphicsCaptureSession {
private:
    std::unique_ptr<IMediaClockFactory> m_mediaClockFactory;
    // ... etc
};

// Factory transfers ownership to session
auto session = std::make_unique<WindowsGraphicsCaptureSession>(
    config,
    std::move(mediaClockFactory),
    std::move(audioCaptureSourceFactory),
    std::move(videoCaptureSourceFactory),
    std::move(mp4SinkWriterFactory));
```

**Option B**: Don't store factories at all - create all sources during construction
```cpp
WindowsGraphicsCaptureSession::WindowsGraphicsCaptureSession(
    const CaptureSessionConfig& config,
    std::unique_ptr<IMediaClock> mediaClock,
    std::unique_ptr<IAudioCaptureSource> audioSource,
    std::unique_ptr<IVideoCaptureSource> videoSource,
    std::unique_ptr<IMP4SinkWriter> sinkWriter)
```
This is cleaner - the factory creates everything and hands ownership to the session. The session never needs to hold factory references.

**Recommended**: **Option B** - Keep factories in the factory, have the factory create all dependencies and pass them to the session. This follows "Composability Over Inheritance" and "Separation of Concerns".

### 2. Unclear Configuration Lifecycle

**Current Issue**: `CaptureSessionConfig` is stored as a value member in the session, but it contains raw pointers (`outputPath`, `hMonitor`).

```cpp
struct CaptureSessionConfig {
    HMONITOR hMonitor;          // Raw Windows handle - who owns this?
    const wchar_t* outputPath;  // Raw pointer - lifetime unclear
    // ...
};
```

**Problem**: 
- Who owns the string pointed to by `outputPath`? How long must it stay valid?
- What if the monitor is disconnected? Do we need to validate `hMonitor`?
- Config is copied by value, but contains pointers - shallow copy semantics

**Alignment Issue**: Violates "Explicit Ownership and Lifetime Management" and "Immutability by Default".

**Recommendation**:

```cpp
struct CaptureSessionConfig {
    HMONITOR hMonitor;
    std::wstring outputPath;  // Own the string
    bool audioEnabled;
    uint32_t frameRate;
    uint32_t videoBitrate;
    uint32_t audioBitrate;
    
    // Explicit validation
    bool IsValid() const {
        return hMonitor != nullptr && !outputPath.empty();
    }
};
```

Benefits:
- Clear ownership (config owns the string)
- No dangling pointers
- Can be safely copied/moved
- Validation can be explicit

### 3. State Management and Error Handling

**Current Issue**: The `Start()` method has a complex initialization sequence with many failure points, but error handling is inconsistent.

```cpp
bool WindowsGraphicsCaptureSession::Start(HRESULT* outHr) {
    // Many steps, each can fail
    // Creates clock
    // Creates audio source
    // Creates video source
    // Initializes sources
    // Initializes sink writer
    // Sets up callbacks
    // Starts capture
    
    // Some failures clean up, some don't
}
```

**Problems**:
- If initialization fails halfway, resources may be partially allocated
- No clear rollback/cleanup path
- State (`m_isActive`) may be inconsistent on error
- Complex initialization logic mixed with session lifetime management

**Alignment Issue**: Violates "Fail Fast and Explicit Error Handling" and "Separation of Concerns".

**Recommendation**:

**Pattern**: Separate initialization from starting

```cpp
class WindowsGraphicsCaptureSession {
public:
    // Factory creates and initializes everything
    WindowsGraphicsCaptureSession(/* fully initialized dependencies */);
    
    // Start just begins capture - resources already initialized
    bool Start(HRESULT* outHr = nullptr);
    
    // Or use a builder pattern
    class Builder {
    public:
        Builder& WithConfig(const CaptureSessionConfig& config);
        Builder& WithMediaClock(std::unique_ptr<IMediaClock> clock);
        Builder& WithAudioSource(std::unique_ptr<IAudioCaptureSource> source);
        Builder& WithVideoSource(std::unique_ptr<IVideoCaptureSource> source);
        Builder& WithSinkWriter(std::unique_ptr<IMP4SinkWriter> writer);
        
        // Build validates all required dependencies are set
        std::unique_ptr<WindowsGraphicsCaptureSession> Build(HRESULT* outHr = nullptr);
    };
};
```

This ensures:
- Resources are fully initialized before session is created
- If initialization fails, session is never created (fail fast)
- No partially-initialized state
- `Start()` is simple - just begins capture

### 4. CRITICAL_SECTION Raw Handle

**Current Issue**: Manual management of `CRITICAL_SECTION` with `InitializeCriticalSection` / `DeleteCriticalSection`.

```cpp
class WindowsGraphicsCaptureSession {
private:
    CRITICAL_SECTION m_callbackCriticalSection;  // Raw handle
};

WindowsGraphicsCaptureSession::WindowsGraphicsCaptureSession(...) {
    InitializeCriticalSection(&m_callbackCriticalSection);
}

WindowsGraphicsCaptureSession::~WindowsGraphicsCaptureSession() {
    DeleteCriticalSection(&m_callbackCriticalSection);
}
```

**Problem**: Not following RAII - manual initialization/cleanup.

**Alignment Issue**: Violates "RAII for resource management".

**Recommendation**:

Create an RAII wrapper for `CRITICAL_SECTION`:

```cpp
class CriticalSection {
public:
    CriticalSection() { InitializeCriticalSection(&m_cs); }
    ~CriticalSection() { DeleteCriticalSection(&m_cs); }
    
    CriticalSection(const CriticalSection&) = delete;
    CriticalSection& operator=(const CriticalSection&) = delete;
    
    void Enter() { EnterCriticalSection(&m_cs); }
    void Leave() { LeaveCriticalSection(&m_cs); }
    
    // RAII lock guard
    class Lock {
    public:
        explicit Lock(CriticalSection& cs) : m_cs(cs) { m_cs.Enter(); }
        ~Lock() { m_cs.Leave(); }
        Lock(const Lock&) = delete;
        Lock& operator=(const Lock&) = delete;
    private:
        CriticalSection& m_cs;
    };
    
private:
    CRITICAL_SECTION m_cs;
};

// Usage:
class WindowsGraphicsCaptureSession {
private:
    CriticalSection m_callbackLock;  // RAII - no manual init/cleanup
};

void SetVideoFrameCallback(VideoFrameCallback callback) {
    CriticalSection::Lock lock(m_callbackLock);  // RAII lock
    m_videoFrameCallback = callback;
    // Lock released automatically
}
```

Or use `std::mutex` + `std::lock_guard` if C++11 is available:

```cpp
class WindowsGraphicsCaptureSession {
private:
    std::mutex m_callbackMutex;
};

void SetVideoFrameCallback(VideoFrameCallback callback) {
    std::lock_guard<std::mutex> lock(m_callbackMutex);
    m_videoFrameCallback = callback;
}
```

### 5. Lambda Captures and Lifetime

**Current Issue**: Lambdas in `Start()` capture `this` and access member variables. If the session is destroyed while callbacks are in flight, this could cause use-after-free.

```cpp
m_audioCaptureSource->SetAudioSampleReadyCallback(
    [this](const AudioSampleReadyEventArgs& args) {
        // What if session is destroyed while this is running?
        if (m_isShuttingDown.load(...)) { return; }
        m_sinkWriter->WriteAudioSample(...);  // Accessing m_sinkWriter
        // ...
    }
);
```

**Current Protection**: The `m_isShuttingDown` atomic flag provides some protection, but the sequence is:
1. Set shutdown flag
2. Stop sources (waits for callbacks to complete)
3. Clear callbacks

This is correct, but relies on careful ordering.

**Alignment Issue**: Could be more explicit about lifetime guarantees.

**Recommendation**:

Make the lifetime contract explicit in the interface documentation and consider using `shared_ptr` for shared ownership if needed:

```cpp
// Option 1: Document the contract clearly (current approach is fine if documented)
/// <summary>
/// Set audio sample callback. The session guarantees:
/// 1. Callback will not be invoked after Stop() returns
/// 2. Callback may be invoked on any thread
/// 3. Callback must not block or take locks
/// </summary>
void SetAudioSampleReadyCallback(AudioSampleReadyCallback callback);

// Option 2: Use weak_ptr pattern if session can be destroyed during callbacks
class WindowsGraphicsCaptureSession 
    : public ICaptureSession, 
      public std::enable_shared_from_this<WindowsGraphicsCaptureSession> {
    
    void Start() {
        std::weak_ptr<WindowsGraphicsCaptureSession> weakThis = shared_from_this();
        
        m_audioCaptureSource->SetAudioSampleReadyCallback(
            [weakThis](const AudioSampleReadyEventArgs& args) {
                auto session = weakThis.lock();
                if (!session) return;  // Session destroyed
                session->HandleAudioSample(args);
            }
        );
    }
};
```

The current approach with `m_isShuttingDown` is actually quite good, but it should be documented clearly.

### 6. Configuration Validation

**Current Issue**: No explicit validation of configuration parameters.

```cpp
bool WindowsGraphicsCaptureSession::Start(HRESULT* outHr) {
    // Assumes m_config is valid
    // What if hMonitor is null? outputPath is null/empty?
}
```

**Alignment Issue**: Violates "Guard Pattern" - should validate at boundaries.

**Recommendation**:

```cpp
bool WindowsGraphicsCaptureSession::Start(HRESULT* outHr) {
    // Guard: Validate configuration first
    if (!m_config.hMonitor) {
        if (outHr) *outHr = E_INVALIDARG;
        return false;
    }
    
    if (!m_config.outputPath || m_config.outputPath[0] == L'\0') {
        if (outHr) *outHr = E_INVALIDARG;
        return false;
    }
    
    // Or use the validation method suggested earlier
    if (!m_config.IsValid()) {
        if (outHr) *outHr = E_INVALIDARG;
        return false;
    }
    
    // Proceed with initialization...
}
```

### 7. Const-Correctness

**Current Issue**: Some methods that don't modify state are not marked `const`.

**Example**: `IsActive()` is correctly `const`, but the interface could enforce const-correctness more broadly.

**Alignment Issue**: "Immutability by Default" principle.

**Recommendation**:

Review all getter methods and mark them `const`:

```cpp
class ICaptureSession {
public:
    virtual bool IsActive() const = 0;
    virtual bool IsPaused() const = 0;  // If this method exists
    // etc.
};
```

### 8. Sleep(200) Magic Number

**Current Issue**: Hardcoded sleep in `Stop()` method.

```cpp
void WindowsGraphicsCaptureSession::Stop() {
    // ...
    Sleep(200);  // Why 200ms? What are we waiting for?
    m_sinkWriter->Finalize();
}
```

**Problem**: Magic number, unclear intent, brittle timing assumption.

**Alignment Issue**: Violates "Explicit is better than implicit".

**Recommendation**:

```cpp
// Option 1: Named constant with documentation
namespace {
    constexpr DWORD ENCODER_DRAIN_TIMEOUT_MS = 200;
    // Allow encoder time to process remaining queued frames
    // (200ms is sufficient for ~6 frames at 30fps)
}

void WindowsGraphicsCaptureSession::Stop() {
    // ...
    Sleep(ENCODER_DRAIN_TIMEOUT_MS);
    m_sinkWriter->Finalize();
}

// Option 2: Better - make the sink writer handle this
// Add a Flush() method that blocks until queue is drained
m_sinkWriter->Flush();
m_sinkWriter->Finalize();
```

## Recommended Refactoring Plan

### Phase 1: Low-Hanging Fruit (High Value, Low Risk)
1. ✅ Replace `CRITICAL_SECTION` with RAII wrapper or `std::mutex`
2. ✅ Make `CaptureSessionConfig` own the output path string (`std::wstring`)
3. ✅ Add configuration validation with `IsValid()` method
4. ✅ Replace magic number with named constant
5. ✅ Review and improve const-correctness

### Phase 2: Ownership Refactoring (Medium Risk)
1. ✅ Refactor factory pattern to pass fully-initialized objects to session
2. ✅ Remove factory storage from session
3. ✅ Update factory to create all dependencies before creating session

### Phase 3: Initialization Refactoring (Higher Risk)
1. ✅ Separate construction from initialization (Builder pattern or factory does all init)
2. ✅ Ensure session is never in partially-initialized state
3. ✅ Simplify `Start()` method to just begin capture
4. ✅ Add comprehensive error handling with proper cleanup

### Phase 4: Documentation and Testing
1. ✅ Document lifetime contracts clearly
2. ✅ Document thread safety guarantees
3. ✅ Add unit tests for error cases
4. ✅ Add integration tests for full lifecycle

## Summary

The current capture session architecture is already quite good and follows many Clean Architecture principles:
- ✅ Dependency inversion through interfaces
- ✅ Interface segregation
- ✅ Factory pattern for creation
- ✅ Smart pointers for ownership
- ✅ Separation of concerns

Key improvements to align with architectural goals:
- **Explicit ownership**: Factory should create fully-initialized objects, not pass raw pointers
- **RAII everywhere**: Use RAII wrapper for `CRITICAL_SECTION` (or `std::mutex`)
- **Value semantics for config**: Config should own its strings
- **Guard pattern**: Validate configuration at entry points
- **Simpler initialization**: Separate construction from starting capture

These changes will make the code more robust, easier to test, and clearer about resource ownership and lifetime.
