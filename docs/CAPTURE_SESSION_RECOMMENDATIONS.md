# Capture Session Code Improvements - Recommendations

## Executive Summary

The CaptureInterop.Lib project demonstrates a well-architected codebase following modern C++ principles with good dependency injection, clear interfaces, and RAII resource management. This document provides recommendations for further improvements focusing on SOLID principles, testability, maintainability, and modern C++ best practices.

**Overall Assessment:** The code is already well-structured with clear separation of concerns. The recommendations below focus on incremental improvements rather than fundamental restructuring.

---

## 1. Error Handling & Result Types

### Current State
The codebase uses a mixed error handling approach:
- Boolean return values with optional `HRESULT* outHr` parameters
- Direct `HRESULT` return values in some methods
- No structured error information beyond HRESULT codes

### Issues
1. **Inconsistent error handling patterns** across different methods
2. **Optional error output parameters** make error handling easy to ignore
3. **Limited error context** - HRESULT codes don't provide detailed diagnostic information
4. **Testing complexity** - Hard to verify specific error conditions in tests

### Recommendation: Introduce Result/Expected Type

Replace boolean + optional HRESULT pattern with a modern `Result<T, ErrorInfo>` type:

```cpp
// ErrorInfo.h
#pragma once
#include <string>
#include <Windows.h>

/// <summary>
/// Structured error information providing richer diagnostics than HRESULT alone.
/// </summary>
struct ErrorInfo
{
    HRESULT hr;
    std::string message;
    std::string context;  // e.g., "InitializeSinkWriter", "StartAudioCapture"
    
    static ErrorInfo Success() 
    { 
        return ErrorInfo{ S_OK, "", "" }; 
    }
    
    static ErrorInfo FromHResult(HRESULT hr, const char* context)
    {
        return ErrorInfo{ hr, HResultToString(hr), context };
    }
    
    bool IsSuccess() const { return SUCCEEDED(hr); }
    
private:
    static std::string HResultToString(HRESULT hr);
};

// Result.h
#pragma once
#include <variant>
#include <utility>

/// <summary>
/// Result type for operations that can succeed with a value or fail with an error.
/// Provides compile-time enforcement of error handling.
/// </summary>
template<typename T>
class Result
{
public:
    // Success constructor
    static Result Ok(T&& value)
    {
        return Result(std::forward<T>(value));
    }
    
    // Error constructor
    static Result Error(ErrorInfo error)
    {
        return Result(std::move(error));
    }
    
    bool IsOk() const { return std::holds_alternative<T>(m_data); }
    bool IsError() const { return !IsOk(); }
    
    // Access value (undefined behavior if error)
    T& Value() { return std::get<T>(m_data); }
    const T& Value() const { return std::get<T>(m_data); }
    
    // Access error (undefined behavior if ok)
    const ErrorInfo& Error() const { return std::get<ErrorInfo>(m_data); }
    
    // Safe access with callback
    template<typename OnOk, typename OnError>
    auto Match(OnOk&& onOk, OnError&& onError) const
    {
        if (IsOk())
            return onOk(std::get<T>(m_data));
        else
            return onError(std::get<ErrorInfo>(m_data));
    }
    
private:
    explicit Result(T&& value) : m_data(std::forward<T>(value)) {}
    explicit Result(ErrorInfo error) : m_data(std::move(error)) {}
    
    std::variant<T, ErrorInfo> m_data;
};

// Specialization for void operations
template<>
class Result<void>
{
public:
    static Result Ok() { return Result(ErrorInfo::Success()); }
    static Result Error(ErrorInfo error) { return Result(std::move(error)); }
    
    bool IsOk() const { return m_error.IsSuccess(); }
    bool IsError() const { return !IsOk(); }
    const ErrorInfo& Error() const { return m_error; }
    
private:
    explicit Result(ErrorInfo error) : m_error(std::move(error)) {}
    ErrorInfo m_error;
};
```

**Usage Example:**

```cpp
// Before
bool WindowsGraphicsCaptureSession::Initialize(HRESULT* outHr)
{
    if (!m_videoCaptureSource->Initialize(&hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }
    // ...
}

// After
Result<void> WindowsGraphicsCaptureSession::Initialize()
{
    auto videoResult = m_videoCaptureSource->Initialize();
    if (videoResult.IsError())
    {
        return Result<void>::Error(
            ErrorInfo::FromHResult(videoResult.Error().hr, "WindowsGraphicsCaptureSession::Initialize"));
    }
    // ...
    return Result<void>::Ok();
}
```

**Benefits:**
- ✅ Consistent error handling across the codebase
- ✅ Compile-time enforcement (can't ignore errors)
- ✅ Rich error context for debugging
- ✅ Better testability - can verify specific error conditions
- ✅ Self-documenting - clear success/failure semantics

**Migration Strategy:**
1. Introduce `Result<T>` and `ErrorInfo` types
2. Update one subsystem at a time (start with new code)
3. Keep backward-compatible wrappers temporarily
4. Gradually migrate existing code

---

## 2. Interface Segregation Principle (ISP) Violations

### Current State
Some interfaces combine multiple responsibilities:

**IMediaClock** combines three interfaces:
- `IMediaClockReader` - Read-only access
- `IMediaClockController` - Lifecycle management
- `IMediaClockWriter` - Time advancement

### Assessment
This is actually **GOOD DESIGN**! The current implementation already follows ISP properly:
- Consumers use specific interfaces (`IMediaClockReader`, etc.)
- `IMediaClock` is just a convenience aggregation for implementation
- No violations detected

### Recommendation: Document the Pattern

Add documentation explaining this is an **Interface Segregation Pattern**:

```cpp
/// <summary>
/// Unified interface for MediaClock implementations.
/// 
/// DESIGN NOTE: This interface aggregates three role interfaces (Reader, Controller, Writer)
/// following the Interface Segregation Principle:
/// 
/// - Consumers depend only on the role interface they need (IMediaClockReader, etc.)
/// - Implementations implement all roles through this unified interface
/// - This prevents interface proliferation while maintaining segregation at usage sites
/// 
/// Example:
///   FrameHandler uses IMediaClockReader (read-only)
///   AudioSource uses IMediaClockWriter (write-only)
///   CaptureSession uses IMediaClockController (lifecycle)
/// 
/// This pattern is similar to C#'s Stream class (read/write) with separate reader/writer views.
/// </summary>
class IMediaClock : public IMediaClockReader, public IMediaClockController, public IMediaClockWriter
{
    // ...
};
```

---

## 3. Dependency Inversion & Testability

### Current State
The architecture uses constructor injection effectively, but testing is limited by concrete dependencies.

### Issues
1. **Hard to unit test in isolation** - Components require real implementations
2. **No mock/fake implementations** provided
3. **Integration tests dominate** - Few true unit tests
4. **Factory complexity** - Testing requires extensive setup

### Recommendation: Provide Test Doubles

Create a comprehensive test utilities library:

```cpp
// TestDoubles/MockMediaClock.h
#pragma once
#include "../IMediaClock.h"
#include <queue>

/// <summary>
/// Controllable media clock for testing.
/// Allows tests to precisely control time progression.
/// </summary>
class MockMediaClock : public IMediaClock
{
public:
    void SetCurrentTime(LONGLONG time) { m_currentTime = time; }
    void AdvanceTime(LONGLONG delta) { m_currentTime += delta; }
    
    // IMediaClockReader
    LONGLONG GetCurrentTime() const override { return m_currentTime; }
    LONGLONG GetStartTime() const override { return m_startTime; }
    LONGLONG GetRelativeTime(LONGLONG qpc) const override { return qpc - m_startTime; }
    bool IsRunning() const override { return m_isRunning; }
    LONGLONG GetQpcFrequency() const override { return 10000000LL; }
    
    // IMediaClockController
    void Start(LONGLONG startQpc) override 
    { 
        m_startTime = startQpc; 
        m_isRunning = true; 
    }
    void Reset() override 
    { 
        m_currentTime = 0; 
        m_startTime = 0; 
        m_isRunning = false; 
    }
    void Pause() override { m_isPaused = true; }
    void Resume() override { m_isPaused = false; }
    void SetClockAdvancer(IMediaClockAdvancer* advancer) override { m_advancer = advancer; }
    
    // IMediaClockWriter
    void AdvanceByAudioSamples(UINT32 numFrames, UINT32 sampleRate) override
    {
        if (!m_isPaused)
        {
            constexpr LONGLONG TICKS_PER_SECOND = 10'000'000;
            LONGLONG delta = (numFrames * TICKS_PER_SECOND) / sampleRate;
            m_currentTime += delta;
        }
    }
    
    // Test inspection
    bool WasStartCalled() const { return m_isRunning; }
    bool IsPaused() const { return m_isPaused; }
    
private:
    LONGLONG m_currentTime = 0;
    LONGLONG m_startTime = 0;
    bool m_isRunning = false;
    bool m_isPaused = false;
    IMediaClockAdvancer* m_advancer = nullptr;
};

// TestDoubles/MockAudioCaptureSource.h
#pragma once
#include "../IAudioCaptureSource.h"

class MockAudioCaptureSource : public IAudioCaptureSource
{
public:
    // Simulate audio sample generation
    void SimulateSample(const BYTE* data, UINT32 numFrames, LONGLONG timestamp)
    {
        if (m_callback && m_enabled)
        {
            AudioSampleReadyEventArgs args{};
            args.pData = data;
            args.numFrames = numFrames;
            args.timestamp = timestamp;
            args.pFormat = &m_format;
            m_callback(args);
        }
    }
    
    // IAudioCaptureSource implementation using Result types
    Result<void> Initialize() override 
    { 
        m_initialized = true; 
        return Result<void>::Ok(); 
    }
    
    Result<void> Start() override 
    { 
        m_running = true; 
        return Result<void>::Ok(); 
    }
    
    void Stop() override { m_running = false; }
    
    WAVEFORMATEX* GetFormat() const override 
    { 
        return const_cast<WAVEFORMATEX*>(&m_format); 
    }
    
    void SetAudioSampleReadyCallback(AudioSampleReadyCallback callback) override
    {
        m_callback = callback;
    }
    
    void SetEnabled(bool enabled) override { m_enabled = enabled; }
    bool IsEnabled() const override { return m_enabled; }
    bool IsRunning() const override { return m_running; }
    
    // IMediaClockAdvancer
    void SetClockWriter(IMediaClockWriter* clockWriter) override
    {
        m_clockWriter = clockWriter;
    }
    
    // Test inspection
    bool WasInitialized() const { return m_initialized; }
    bool IsCallbackSet() const { return m_callback != nullptr; }
    
private:
    bool m_initialized = false;
    bool m_running = false;
    bool m_enabled = true;
    AudioSampleReadyCallback m_callback;
    IMediaClockWriter* m_clockWriter = nullptr;
    // Default format: 16-bit stereo PCM at 48kHz
    WAVEFORMATEX m_format{ 
        WAVE_FORMAT_PCM,  // wFormatTag
        2,                // nChannels (stereo)
        48000,            // nSamplesPerSec
        192000,           // nAvgBytesPerSec (48000 * 2 * 2)
        4,                // nBlockAlign (2 channels * 2 bytes)
        16,               // wBitsPerSample
        0                 // cbSize
    };
};
```

**Example Unit Test:**

```cpp
// CaptureSessionUnitTests.cpp
TEST_METHOD(Session_Initialize_ConfiguresAllDependencies)
{
    // Arrange
    CaptureSessionConfig config(nullptr, L"test.mp4");
    auto mockClock = std::make_unique<MockMediaClock>();
    auto mockAudio = std::make_unique<MockAudioCaptureSource>();
    auto mockVideo = std::make_unique<MockVideoCaptureSource>();
    auto mockSink = std::make_unique<MockMP4SinkWriter>();
    
    auto* clockPtr = mockClock.get();
    auto* audioPtr = mockAudio.get();
    auto* videoPtr = mockVideo.get();
    auto* sinkPtr = mockSink.get();
    
    WindowsGraphicsCaptureSession session(
        config,
        std::move(mockClock),
        std::move(mockAudio),
        std::move(mockVideo),
        std::move(mockSink));
    
    // Act
    auto result = session.Initialize();
    
    // Assert
    Assert::IsTrue(result.IsOk());
    Assert::IsTrue(audioPtr->WasInitialized());
    Assert::IsTrue(videoPtr->WasInitialized());
    Assert::IsTrue(sinkPtr->WasInitialized());
    Assert::IsTrue(audioPtr->IsCallbackSet());
    Assert::IsTrue(videoPtr->IsCallbackSet());
}
```

**Benefits:**
- ✅ Fast unit tests (no real hardware/API dependencies)
- ✅ Test edge cases and error conditions easily
- ✅ Better isolation of components
- ✅ Easier to reproduce bugs in tests

---

## 4. State Management & State Machine Pattern

### Current State
State is managed through multiple boolean flags:
- `m_isInitialized`
- `m_isActive`
- `m_isShuttingDown` (atomic)
- `m_isPaused` (in media clock)

### Issues
1. **Implicit state transitions** - No clear state machine
2. **Potential for invalid states** - Flags can conflict
3. **Hard to reason about** - State spread across multiple variables
4. **Difficult to test** - State validation scattered throughout code

### Recommendation: Explicit State Machine

Introduce an explicit state machine for session lifecycle:

```cpp
// CaptureSessionState.h
#pragma once

/// <summary>
/// Explicit state machine for capture session lifecycle.
/// Defines valid states and transitions for compile-time safety.
/// </summary>
enum class CaptureSessionState
{
    /// <summary>
    /// Session created but not initialized. Initial state.
    /// Valid transitions: Initialized, Failed
    /// </summary>
    Created,
    
    /// <summary>
    /// Dependencies initialized, ready to start.
    /// Valid transitions: Active, Failed
    /// </summary>
    Initialized,
    
    /// <summary>
    /// Actively capturing audio/video.
    /// Valid transitions: Paused, Stopped, Failed
    /// </summary>
    Active,
    
    /// <summary>
    /// Capture paused, can resume.
    /// Valid transitions: Active, Stopped, Failed
    /// </summary>
    Paused,
    
    /// <summary>
    /// Capture stopped cleanly.
    /// Valid transitions: (terminal state)
    /// </summary>
    Stopped,
    
    /// <summary>
    /// Error occurred, session unusable.
    /// Valid transitions: (terminal state)
    /// </summary>
    Failed
};

/// <summary>
/// State machine that validates transitions and provides query methods.
/// </summary>
class CaptureSessionStateMachine
{
public:
    CaptureSessionStateMachine() : m_state(CaptureSessionState::Created) {}
    
    CaptureSessionState GetState() const 
    { 
        return m_state.load(); 
    }
    
    // State queries
    bool IsInitialized() const 
    { 
        auto s = m_state.load();
        return s != CaptureSessionState::Created && s != CaptureSessionState::Failed;
    }
    
    bool IsActive() const 
    { 
        auto s = m_state.load();
        return s == CaptureSessionState::Active || s == CaptureSessionState::Paused;
    }
    
    bool CanTransitionTo(CaptureSessionState newState) const
    {
        return IsValidTransition(m_state.load(), newState);
    }
    
    // Atomic state transitions with validation
    bool TryTransitionTo(CaptureSessionState newState)
    {
        auto currentState = m_state.load();
        
        if (!IsValidTransition(currentState, newState))
            return false;
            
        m_state.store(newState);
        return true;
    }
    
private:
    std::atomic<CaptureSessionState> m_state;
    
    static bool IsValidTransition(CaptureSessionState from, CaptureSessionState to)
    {
        switch (from)
        {
        case CaptureSessionState::Created:
            return to == CaptureSessionState::Initialized || to == CaptureSessionState::Failed;
            
        case CaptureSessionState::Initialized:
            return to == CaptureSessionState::Active || to == CaptureSessionState::Failed;
            
        case CaptureSessionState::Active:
            return to == CaptureSessionState::Paused || 
                   to == CaptureSessionState::Stopped || 
                   to == CaptureSessionState::Failed;
            
        case CaptureSessionState::Paused:
            return to == CaptureSessionState::Active || 
                   to == CaptureSessionState::Stopped || 
                   to == CaptureSessionState::Failed;
            
        case CaptureSessionState::Stopped:
        case CaptureSessionState::Failed:
            return false; // Terminal states
            
        default:
            return false;
        }
    }
};
```

**Usage in Session:**

```cpp
class WindowsGraphicsCaptureSession : public ICaptureSession
{
public:
    bool Initialize(HRESULT* outHr = nullptr) override
    {
        // Validate state transition
        if (!m_stateMachine.CanTransitionTo(CaptureSessionState::Initialized))
        {
            if (outHr) *outHr = E_ILLEGAL_STATE_CHANGE;
            return false;
        }
        
        // ... initialization logic ...
        
        if (success)
            m_stateMachine.TryTransitionTo(CaptureSessionState::Initialized);
        else
            m_stateMachine.TryTransitionTo(CaptureSessionState::Failed);
            
        return success;
    }
    
    bool Start(HRESULT* outHr = nullptr) override
    {
        if (!m_stateMachine.CanTransitionTo(CaptureSessionState::Active))
        {
            if (outHr) *outHr = E_ILLEGAL_STATE_CHANGE;
            return false;
        }
        
        // ... start logic ...
    }
    
    bool IsActive() const override 
    { 
        return m_stateMachine.IsActive(); 
    }
    
private:
    CaptureSessionStateMachine m_stateMachine;
    // Remove: m_isInitialized, m_isActive, m_isShuttingDown
};
```

**Benefits:**
- ✅ Explicit state transitions
- ✅ Compile-time state validation
- ✅ Easier to test state logic
- ✅ Self-documenting lifecycle
- ✅ Prevents invalid state combinations

---

## 5. Callback Safety & Lifetime Management

### Current State
Callbacks use raw function pointers with mutex protection:
```cpp
VideoFrameCallback m_videoFrameCallback;
AudioSampleCallback m_audioSampleCallback;
std::mutex m_callbackMutex;
```

### Issues
1. **Manual lifetime management** - Easy to create dangling pointers
2. **No callback lifetime guarantees** - Caller must manage lifetime
3. **Race conditions possible** - Callback could be cleared while executing
4. **No cancellation token** - Hard to interrupt long-running callbacks

### Recommendation: Callback Handle Pattern

Introduce a handle-based callback system with automatic lifetime management:

```cpp
// CallbackHandle.h
#pragma once
#include <memory>
#include <atomic>

/// <summary>
/// Handle for a registered callback that automatically unregisters on destruction.
/// Provides RAII for callback lifetime management.
/// </summary>
class CallbackHandle
{
public:
    CallbackHandle() = default;
    
    // Create handle with unregister function
    explicit CallbackHandle(std::function<void()> unregisterFn)
        : m_unregister(std::make_shared<UnregisterToken>(std::move(unregisterFn)))
    {}
    
    // Move-only type
    CallbackHandle(CallbackHandle&&) = default;
    CallbackHandle& operator=(CallbackHandle&&) = default;
    CallbackHandle(const CallbackHandle&) = delete;
    CallbackHandle& operator=(const CallbackHandle&) = delete;
    
    // Explicit unregister (also happens automatically on destruction)
    void Unregister()
    {
        if (m_unregister)
        {
            m_unregister->Unregister();
            m_unregister.reset();
        }
    }
    
private:
    struct UnregisterToken
    {
        explicit UnregisterToken(std::function<void()> fn)
            : unregisterFn(std::move(fn)) {}
            
        ~UnregisterToken()
        {
            Unregister();
        }
        
        void Unregister()
        {
            if (!called.exchange(true))
            {
                unregisterFn();
            }
        }
        
        std::atomic<bool> called{false};
        std::function<void()> unregisterFn;
    };
    
    std::shared_ptr<UnregisterToken> m_unregister;
};

// CallbackRegistry.h
#pragma once
#include <mutex>
#include <unordered_map>
#include <functional>
#include <atomic>

/// <summary>
/// Thread-safe registry for callbacks with automatic lifetime management.
/// Guarantees callbacks won't be invoked after being unregistered.
/// </summary>
template<typename TArgs>
class CallbackRegistry
{
public:
    using CallbackFn = std::function<void(const TArgs&)>;
    using CallbackId = uint64_t;
    
    /// <summary>
    /// Register a callback and return a handle.
    /// Callback will be automatically unregistered when handle is destroyed.
    /// </summary>
    CallbackHandle Register(CallbackFn callback)
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        
        CallbackId id = m_nextId++;
        m_callbacks[id] = std::move(callback);
        
        // Return handle that will unregister on destruction
        return CallbackHandle([this, id]() { this->Unregister(id); });
    }
    
    /// <summary>
    /// Invoke all registered callbacks with the given arguments.
    /// Thread-safe and guarantees callbacks exist for duration of invocation.
    /// </summary>
    void Invoke(const TArgs& args)
    {
        // Copy callbacks under lock, then invoke without lock to prevent deadlocks
        std::vector<CallbackFn> callbacks;
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            callbacks.reserve(m_callbacks.size());
            for (const auto& pair : m_callbacks)
            {
                callbacks.push_back(pair.second);
            }
        }
        
        // Invoke without holding lock
        for (const auto& callback : callbacks)
        {
            callback(args);
        }
    }
    
    /// <summary>
    /// Clear all callbacks. Useful during shutdown.
    /// </summary>
    void Clear()
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        m_callbacks.clear();
    }
    
private:
    void Unregister(CallbackId id)
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        m_callbacks.erase(id);
    }
    
    std::mutex m_mutex;
    std::unordered_map<CallbackId, CallbackFn> m_callbacks;
    std::atomic<CallbackId> m_nextId{1};
};
```

**Usage:**

```cpp
class WindowsGraphicsCaptureSession : public ICaptureSession
{
public:
    /// <summary>
    /// Register a callback for video frames.
    /// Returns a handle that will automatically unregister when destroyed.
    /// </summary>
    CallbackHandle RegisterVideoFrameCallback(std::function<void(const VideoFrameData&)> callback)
    {
        return m_videoCallbacks.Register(std::move(callback));
    }
    
    CallbackHandle RegisterAudioSampleCallback(std::function<void(const AudioSampleData&)> callback)
    {
        return m_audioCallbacks.Register(std::move(callback));
    }
    
private:
    void OnVideoFrameReady(const VideoFrameReadyEventArgs& args)
    {
        if (m_isShuttingDown.load())
            return;
            
        // Write to sink...
        
        // Invoke callbacks safely
        VideoFrameData frameData{};
        // ... populate frameData ...
        m_videoCallbacks.Invoke(frameData);
    }
    
    void Stop() override
    {
        m_isShuttingDown.store(true);
        // ... stop sources ...
        
        // Clear all callbacks
        m_videoCallbacks.Clear();
        m_audioCallbacks.Clear();
    }
    
    CallbackRegistry<VideoFrameData> m_videoCallbacks;
    CallbackRegistry<AudioSampleData> m_audioCallbacks;
    
    // Remove: m_videoFrameCallback, m_audioSampleCallback, m_callbackMutex
};
```

**Client Usage:**

```cpp
// Callback automatically unregistered when handle goes out of scope
{
    auto handle = session->RegisterVideoFrameCallback([](const VideoFrameData& frame) {
        // Process frame
    });
    
    session->Start();
    // ... recording ...
    
} // Callback automatically unregistered here

session->Stop();
```

**Benefits:**
- ✅ RAII callback lifetime management
- ✅ No dangling callback pointers
- ✅ Thread-safe invocation
- ✅ Prevents use-after-free bugs
- ✅ Multiple callbacks supported (extensible)

---

## 6. Modern C++ Features

### Current State
The code uses modern C++ but could leverage additional C++20/23 features.

### Recommendations

#### 6.1 Use `std::expected` (C++23) or Result Type
See Recommendation #1 above.

#### 6.2 Use `std::span` for Buffer Passing

Replace raw pointers with `std::span` for safer buffer handling:

```cpp
// Before
virtual long WriteAudioSample(const uint8_t* pData, uint32_t numFrames, int64_t timestamp) = 0;

// After
#include <span>

virtual long WriteAudioSample(std::span<const uint8_t> data, int64_t timestamp) = 0;

// Usage
std::span<const uint8_t> audioData(pData, numFrames * bytesPerFrame);
WriteAudioSample(audioData, timestamp);
```

**Benefits:**
- ✅ Size information bundled with pointer
- ✅ Prevents buffer overruns
- ✅ Compiler can perform bounds checking
- ✅ Works with containers seamlessly

#### 6.3 Use `constexpr` for Compile-Time Constants

```cpp
// Before
static constexpr DWORD ENCODER_DRAIN_TIMEOUT_MS = 200;
static constexpr LONGLONG TICKS_PER_SECOND = 10000000LL;

// Better - Use constexpr functions for clarity
namespace Constants
{
    constexpr DWORD EncoderDrainTimeoutMs() { return 200; }
    constexpr LONGLONG TicksPerSecond() { return 10'000'000; }
    constexpr LONGLONG TicksPerMillisecond() { return TicksPerSecond() / 1000; }
}
```

#### 6.4 Use Concepts for Template Constraints (C++20)

If you add generic code, use concepts instead of SFINAE:

```cpp
// Clock requirement concept
template<typename T>
concept MediaClockLike = requires(T t, LONGLONG qpc)
{
    { t.GetCurrentTime() } -> std::convertible_to<LONGLONG>;
    { t.IsRunning() } -> std::convertible_to<bool>;
    { t.Start(qpc) } -> std::same_as<void>;
};

// Usage
template<MediaClockLike TClock>
class CaptureSession
{
    // ...
};
```

#### 6.5 Use Designated Initializers for Config Structs

```cpp
// Make config more readable with C++20 designated initializers
CaptureSessionConfig config
{
    .hMonitor = hMonitor,
    .outputPath = L"output.mp4",
    .audioEnabled = true,
    .frameRate = 30,
    .videoBitrate = 5'000'000,
    .audioBitrate = 128'000
};
```

---

## 7. Logging & Observability

### Current State
Limited diagnostic information available during runtime. Errors are returned as HRESULTs with no logging.

### Issues
1. **No structured logging** - Hard to diagnose issues in production
2. **No performance metrics** - Can't identify bottlenecks
3. **No debug information** - Limited visibility into capture pipeline

### Recommendation: Structured Logging & Telemetry

```cpp
// Logger.h
#pragma once
#include <string>
#include <chrono>
#include <functional>

enum class LogLevel
{
    Trace,
    Debug,
    Info,
    Warning,
    Error,
    Critical
};

struct LogEntry
{
    LogLevel level;
    std::string message;
    std::string context;
    std::chrono::system_clock::time_point timestamp;
    HRESULT errorCode{S_OK};
};

/// <summary>
/// Interface for logging implementations.
/// Allows injection of different logging backends (console, file, ETW, etc.)
/// </summary>
class ILogger
{
public:
    virtual ~ILogger() = default;
    virtual void Log(const LogEntry& entry) = 0;
    
    // Convenience methods
    void Trace(const std::string& message, const std::string& context = "")
    {
        Log({LogLevel::Trace, message, context, std::chrono::system_clock::now()});
    }
    
    void Error(const std::string& message, HRESULT hr, const std::string& context = "")
    {
        Log({LogLevel::Error, message, context, std::chrono::system_clock::now(), hr});
    }
};

// Performance metrics
struct CaptureMetrics
{
    uint64_t framesProcessed{0};
    uint64_t framesDropped{0};
    uint64_t audioSamplesProcessed{0};
    double averageFrameTime{0.0};
    double peakFrameTime{0.0};
    
    void RecordFrameTime(double timeMs)
    {
        framesProcessed++;
        averageFrameTime = (averageFrameTime * (framesProcessed - 1) + timeMs) / framesProcessed;
        if (timeMs > peakFrameTime)
            peakFrameTime = timeMs;
    }
};
```

**Usage:**

```cpp
class WindowsGraphicsCaptureSession : public ICaptureSession
{
public:
    WindowsGraphicsCaptureSession(
        const CaptureSessionConfig& config,
        std::unique_ptr<IMediaClock> mediaClock,
        std::unique_ptr<IAudioCaptureSource> audioCaptureSource,
        std::unique_ptr<IVideoCaptureSource> videoCaptureSource,
        std::unique_ptr<IMP4SinkWriter> sinkWriter,
        ILogger* logger = nullptr)  // Optional logger
        : m_config(config)
        , m_mediaClock(std::move(mediaClock))
        , m_audioCaptureSource(std::move(audioCaptureSource))
        , m_videoCaptureSource(std::move(videoCaptureSource))
        , m_sinkWriter(std::move(sinkWriter))
        , m_logger(logger)
    {
        if (m_logger)
            m_logger->Trace("CaptureSession created", "WindowsGraphicsCaptureSession");
    }
    
    bool Start(HRESULT* outHr = nullptr) override
    {
        if (m_logger)
            m_logger->Info("Starting capture session", "Start");
            
        HRESULT hr = S_OK;
        
        if (!StartAudioCapture(&hr))
        {
            if (m_logger)
                m_logger->Error("Failed to start audio capture", hr, "Start");
            if (outHr) *outHr = hr;
            return false;
        }
        
        if (m_logger)
        {
            auto metrics = GetMetrics();
            std::string message = "Capture session started successfully - " +
                                std::to_string(metrics.framesProcessed) + " frames processed";
            m_logger->Info(message, "Start");
        }
        
        return true;
    }
    
    CaptureMetrics GetMetrics() const { return m_metrics; }
    
private:
    ILogger* m_logger;  // Non-owning pointer (optional dependency)
    CaptureMetrics m_metrics;
};
```

**Benefits:**
- ✅ Diagnostic information in production
- ✅ Performance monitoring
- ✅ Easier debugging
- ✅ Pluggable logging backends

---

## 8. Configuration Validation

### Current State
Configuration validation is minimal:
```cpp
bool IsValid() const
{
    return hMonitor != nullptr && !outputPath.empty();
}
```

### Issues
1. **Incomplete validation** - Doesn't check bitrates, frame rates, etc.
2. **No validation feedback** - Doesn't explain what's invalid
3. **No range checking** - Values could be out of reasonable bounds

### Recommendation: Comprehensive Validation

```cpp
// CaptureSessionConfig.h

/// <summary>
/// Result of configuration validation with detailed error information.
/// </summary>
struct ConfigValidationResult
{
    bool isValid;
    std::vector<std::string> errors;
    std::vector<std::string> warnings;
    
    static ConfigValidationResult Ok()
    {
        return ConfigValidationResult{true, {}, {}};
    }
    
    void AddError(const std::string& error)
    {
        errors.push_back(error);
        isValid = false;
    }
    
    void AddWarning(const std::string& warning)
    {
        warnings.push_back(warning);
    }
};

struct CaptureSessionConfig
{
    // ... existing fields ...
    
    /// <summary>
    /// Comprehensive validation with detailed feedback.
    /// </summary>
    ConfigValidationResult Validate() const
    {
        ConfigValidationResult result = ConfigValidationResult::Ok();
        
        // Required fields
        if (hMonitor == nullptr)
            result.AddError("hMonitor is required");
            
        if (outputPath.empty())
            result.AddError("outputPath is required");
        else if (!IsValidOutputPath(outputPath))
            result.AddError("outputPath is not writable or parent directory doesn't exist");
            
        // Frame rate validation
        if (frameRate < 1 || frameRate > 120)
            result.AddError("frameRate must be between 1 and 120 (got " + 
                          std::to_string(frameRate) + ")");
        else if (frameRate < 15)
            result.AddWarning("frameRate is very low (" + 
                            std::to_string(frameRate) + "), video may appear choppy");
                            
        // Video bitrate validation
        constexpr uint32_t MIN_VIDEO_BITRATE = 100'000;    // 100 kbps
        constexpr uint32_t MAX_VIDEO_BITRATE = 50'000'000; // 50 Mbps
        
        if (videoBitrate < MIN_VIDEO_BITRATE || videoBitrate > MAX_VIDEO_BITRATE)
            result.AddError("videoBitrate must be between " + 
                          std::to_string(MIN_VIDEO_BITRATE) + " and " + 
                          std::to_string(MAX_VIDEO_BITRATE));
        else if (videoBitrate < 1'000'000)
            result.AddWarning("videoBitrate is low (" + 
                            std::to_string(videoBitrate) + "), quality may be poor");
                            
        // Audio bitrate validation
        constexpr uint32_t MIN_AUDIO_BITRATE = 32'000;    // 32 kbps
        constexpr uint32_t MAX_AUDIO_BITRATE = 320'000;   // 320 kbps
        
        if (audioEnabled)
        {
            if (audioBitrate < MIN_AUDIO_BITRATE || audioBitrate > MAX_AUDIO_BITRATE)
                result.AddError("audioBitrate must be between " + 
                              std::to_string(MIN_AUDIO_BITRATE) + " and " + 
                              std::to_string(MAX_AUDIO_BITRATE));
        }
        
        return result;
    }
    
    /// <summary>
    /// Simple validation for backward compatibility.
    /// </summary>
    bool IsValid() const
    {
        return Validate().isValid;
    }
    
private:
    static bool IsValidOutputPath(const std::wstring& path)
    {
        // Check if parent directory exists and is writable
        // Implementation would use filesystem APIs
        return true; // Placeholder
    }
};
```

**Benefits:**
- ✅ Comprehensive validation
- ✅ Clear error messages
- ✅ Warnings for suboptimal configurations
- ✅ Easier to diagnose configuration issues

---

## 9. Resource Management & Cleanup

### Current State
Resources are managed with RAII using `std::unique_ptr` and WIL (`wil::com_ptr`).

### Assessment
**This is excellent!** The current approach is already best practice.

### Minor Recommendation: Add Resource Statistics

```cpp
/// <summary>
/// Resource tracking for diagnostics and leak detection in debug builds.
/// </summary>
class ResourceTracker
{
public:
    static ResourceTracker& Instance()
    {
        static ResourceTracker instance;
        return instance;
    }
    
    void TrackAllocation(const char* resourceType, void* ptr)
    {
#ifdef _DEBUG
        std::lock_guard<std::mutex> lock(m_mutex);
        m_allocations[resourceType]++;
        m_activeResources[ptr] = resourceType;
#endif
    }
    
    void TrackDeallocation(void* ptr)
    {
#ifdef _DEBUG
        std::lock_guard<std::mutex> lock(m_mutex);
        auto it = m_activeResources.find(ptr);
        if (it != m_activeResources.end())
        {
            m_deallocations[it->second]++;
            m_activeResources.erase(it);
        }
#endif
    }
    
    void DumpStatistics()
    {
#ifdef _DEBUG
        std::lock_guard<std::mutex> lock(m_mutex);
        OutputDebugStringA("=== Resource Statistics ===\n");
        for (const auto& [type, count] : m_allocations)
        {
            char buffer[256];
            sprintf_s(buffer, "%s: %zu allocated, %zu deallocated, %zu leaked\n",
                     type, count, m_deallocations[type], 
                     count - m_deallocations[type]);
            OutputDebugStringA(buffer);
        }
#endif
    }
    
private:
    std::mutex m_mutex;
    std::unordered_map<std::string, size_t> m_allocations;
    std::unordered_map<std::string, size_t> m_deallocations;
    std::unordered_map<void*, std::string> m_activeResources;
};
```

---

## 10. Code Organization & File Structure

### Current State
Good separation of interfaces and implementations. Clear file naming conventions.

### Recommendations

#### 10.1 Group Related Files in Subdirectories

```
CaptureInterop.Lib/
├── Interfaces/
│   ├── ICaptureSession.h
│   ├── IAudioCaptureSource.h
│   ├── IVideoCaptureSource.h
│   ├── IMediaClock.h
│   └── ...
├── MediaClock/
│   ├── SimpleMediaClock.h
│   ├── SimpleMediaClock.cpp
│   ├── IMediaClockReader.h
│   ├── IMediaClockController.h
│   └── ...
├── CaptureSources/
│   ├── Audio/
│   │   ├── WindowsLocalAudioCaptureSource.h
│   │   └── ...
│   └── Video/
│       ├── WindowsDesktopVideoCaptureSource.h
│       └── ...
├── Session/
│   ├── WindowsGraphicsCaptureSession.h
│   ├── WindowsGraphicsCaptureSession.cpp
│   ├── CaptureSessionConfig.h
│   └── ...
├── Encoders/
│   ├── WindowsMFMP4SinkWriter.h
│   └── ...
└── Utilities/
    ├── CallbackTypes.h
    └── ...
```

#### 10.2 Create Forward Declaration Header

```cpp
// ForwardDeclarations.h
#pragma once

// Interfaces
class ICaptureSession;
class IAudioCaptureSource;
class IVideoCaptureSource;
class IMediaClock;
class IMP4SinkWriter;

// Implementations  
class WindowsGraphicsCaptureSession;
class SimpleMediaClock;

// Reduces compilation dependencies and speeds up builds
```

---

## Summary & Prioritization

### High Priority (Implement First)
1. **Error Handling with Result Types** - Improves reliability and debuggability
2. **Test Doubles & Mocks** - Critical for unit testing
3. **Callback Handle Pattern** - Prevents lifetime bugs
4. **Configuration Validation** - Catches errors early

### Medium Priority
5. **State Machine Pattern** - Improves clarity and maintainability
6. **Logging & Observability** - Essential for production diagnosis
7. **Modern C++ Features** - Incremental improvements

### Low Priority (Future Enhancements)
8. **Code Organization** - Beneficial but not critical
9. **Resource Tracking** - Useful for debugging only

### Overall Assessment

The CaptureInterop.Lib codebase is **well-architected** with:
- ✅ Good separation of concerns
- ✅ Effective use of dependency injection
- ✅ RAII resource management
- ✅ Clear interfaces
- ✅ Thread safety considerations

The recommendations above are **incremental improvements** rather than fundamental restructuring. The code already follows SOLID principles reasonably well.

### Next Steps

1. Review these recommendations with the team
2. Choose 2-3 recommendations to implement first
3. Create implementation plan with estimates
4. Update architecture documentation
5. Add new patterns to coding guidelines

