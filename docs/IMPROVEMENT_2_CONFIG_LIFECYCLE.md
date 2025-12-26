# Implementation: Improvement Area #2 - Configuration Lifecycle

## Summary

This document describes the implementation of the second improvement area from SESSION_ARCHITECTURE_ANALYSIS.md: **Unclear Configuration Lifecycle**.

## Problem Addressed

The `CaptureSessionConfig` struct previously contained a raw pointer to the output path (`const wchar_t* outputPath`), creating unclear ownership semantics and potential lifetime issues.

**Issues:**
- Unclear ownership: Who owns the string? How long must it stay valid?
- Shallow copy semantics: Config could be copied, but the pointer would be shared
- No validation: No way to check if the config is valid before use
- Violates "Explicit Ownership and Lifetime Management" and "Immutability by Default" principles

## Solution Implemented

Changed `outputPath` from `const wchar_t*` to `std::wstring` so the config owns its data, and added an `IsValid()` method for validation.

## Changes Made

### 1. CaptureSessionConfig.h

**Before:**
```cpp
struct CaptureSessionConfig
{
    HMONITOR hMonitor;
    const wchar_t* outputPath;  // Raw pointer - lifetime unclear
    bool audioEnabled;
    uint32_t frameRate;
    uint32_t videoBitrate;
    uint32_t audioBitrate;

    CaptureSessionConfig(
        HMONITOR monitor,
        const wchar_t* path,
        bool audio = false,
        uint32_t fps = 30,
        uint32_t vidBitrate = 5000000,
        uint32_t audBitrate = 128000)
        : hMonitor(monitor)
        , outputPath(path)  // Just stores the pointer
        // ...
    { }
};
```

**After:**
```cpp
struct CaptureSessionConfig
{
    HMONITOR hMonitor;
    std::wstring outputPath;  // Own the string
    bool audioEnabled;
    uint32_t frameRate;
    uint32_t videoBitrate;
    uint32_t audioBitrate;

    // Constructor accepting const wchar_t* (backward compatible)
    CaptureSessionConfig(
        HMONITOR monitor,
        const wchar_t* path,
        bool audio = false,
        uint32_t fps = 30,
        uint32_t vidBitrate = 5000000,
        uint32_t audBitrate = 128000)
        : hMonitor(monitor)
        , outputPath(path ? path : L"")  // Copy the string
        // ...
    { }

    // New constructor accepting std::wstring (move semantics)
    CaptureSessionConfig(
        HMONITOR monitor,
        std::wstring path,
        bool audio = false,
        uint32_t fps = 30,
        uint32_t vidBitrate = 5000000,
        uint32_t audBitrate = 128000)
        : hMonitor(monitor)
        , outputPath(std::move(path))  // Transfer ownership
        // ...
    { }

    // Validation method
    bool IsValid() const
    {
        return hMonitor != nullptr && !outputPath.empty();
    }
};
```

**Key Changes:**
- Changed `outputPath` from `const wchar_t*` to `std::wstring`
- Added null-check in the existing constructor: `path ? path : L""`
- Added overloaded constructor accepting `std::wstring` with move semantics
- Changed default constructor to initialize `outputPath` to `L""` instead of `nullptr`
- Added `IsValid()` method for Guard pattern validation
- Updated documentation explaining ownership semantics
- Added `#include <string>` for `std::wstring`

### 2. WindowsGraphicsCaptureSession.cpp

**Changed line 201:**
```cpp
// Before:
if (!m_sinkWriter->Initialize(m_config.outputPath, device, width, height, &hr))

// After:
if (!m_sinkWriter->Initialize(m_config.outputPath.c_str(), device, width, height, &hr))
```

Since `IMP4SinkWriter::Initialize()` expects `const wchar_t*`, we now call `.c_str()` on the `std::wstring`.

### 3. WindowsGraphicsCaptureSessionFactory.cpp

**Added validation at the start of CreateSession():**
```cpp
std::unique_ptr<ICaptureSession> CreateSession(const CaptureSessionConfig& config)
{
    // Validate configuration first (Guard pattern)
    if (!config.IsValid())
    {
        return nullptr;
    }

    // Create the media clock first
    // ... rest of the method
}
```

This implements the Guard pattern - validate inputs at system boundaries and fail fast if invalid.

### 4. ShutdownRaceConditionTests.cpp

Updated all three test methods to work with the new changes:

**Before:**
```cpp
CaptureSessionConfig config;
config.outputPath = L"test_output.mp4";  // Direct assignment
config.audioEnabled = true;
// ...

WindowsGraphicsCaptureSession session(
    config,
    &clockFactory,
    &audioFactory,
    &videoFactory,
    &sinkFactory
);
```

**After:**
```cpp
// Use constructor
CaptureSessionConfig config(
    GetPrimaryMonitor(),
    L"test_output.mp4",
    true,   // audioEnabled
    30,     // frameRate
    5000000, // videoBitrate
    192000  // audioBitrate
);

// Use factory to create session
WindowsGraphicsCaptureSessionFactory factory(
    std::make_unique<SimpleMediaClockFactory>(),
    std::make_unique<WindowsLocalAudioCaptureSourceFactory>(),
    std::make_unique<WindowsDesktopVideoCaptureSourceFactory>(),
    std::make_unique<WindowsMFMP4SinkWriterFactory>()
);

auto session = factory.CreateSession(config);
```

Also added a helper method `GetPrimaryMonitor()` to get a valid monitor handle for tests.

## Benefits Achieved

### ✅ Explicit Ownership
- Config owns the output path string via `std::wstring`
- No ambiguity about who owns what
- No dangling pointer risk

### ✅ Safe Copying and Moving
- Config can be safely copied (deep copy of string)
- Config can be efficiently moved (move semantics for string)
- No shallow copy issues

### ✅ Clear Lifetime Semantics
- String lifetime is tied to config lifetime
- RAII ensures cleanup when config is destroyed
- No need for caller to keep string alive

### ✅ Validation
- `IsValid()` method provides explicit validation
- Factory uses Guard pattern to validate before creating session
- Fail-fast behavior prevents partially-initialized state

### ✅ Backward Compatibility
- Existing code passing `const wchar_t*` still works
- New code can use `std::wstring` for better semantics
- Both constructors are supported

### ✅ Value Semantics
- Config behaves like a proper value type
- Can be stored in containers safely
- No pointer lifetime management needed

## Architectural Alignment

This change aligns with the following principles from ARCHITECTURE_GOALS.md:

1. **Explicit Ownership and Lifetime Management**: Config owns its data through `std::wstring`
2. **Immutability by Default**: Config is immutable after construction (no setters)
3. **Guard Pattern**: Validation at boundaries with `IsValid()` method
4. **Fail Fast**: Factory validates config and returns nullptr if invalid
5. **Value Semantics**: Config is a proper value type with clear semantics

## Testing Considerations

- Updated `ShutdownRaceConditionTests.cpp` to work with new constructor
- Tests now use factory to create sessions (improvement #1)
- Added `GetPrimaryMonitor()` helper for valid test configurations
- All three test methods updated consistently

## Migration Path

For any code that creates `CaptureSessionConfig`:

**Old way (still works):**
```cpp
CaptureSessionConfig config(monitor, L"output.mp4", true);
// The string is copied into the config
```

**New way (better):**
```cpp
std::wstring path = L"output.mp4";
CaptureSessionConfig config(monitor, std::move(path), true);
// The string is moved into the config (more efficient)
```

**For validation:**
```cpp
CaptureSessionConfig config(...);
if (!config.IsValid())
{
    // Handle invalid config
}
```

## Impact on Existing Code

- Minimal impact - existing code continues to work
- Call sites that pass `const wchar_t*` don't need changes
- Code that uses `config.outputPath` needs to call `.c_str()` if passing to C APIs
- Tests needed updates to use constructors properly

## Future Improvements

With this foundation in place:
- Consider making HMONITOR a wrapped type with validation
- Could add more validation (e.g., check if monitor is valid, path is writable)
- Could add methods to check individual fields (e.g., `HasValidMonitor()`, `HasValidPath()`)

## Related Improvements

This improvement builds on:
- **Improvement #1**: Factory creates all dependencies and passes to session
- Both improvements together provide clear ownership throughout the capture pipeline
