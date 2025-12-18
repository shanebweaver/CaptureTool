# Phase 2: Multiple Source Support - Detailed Development Plan

**Phase Duration:** 3-4 weeks  
**Goal:** Enable multiple audio sources (desktop, microphone, per-application) to be captured simultaneously with proper coordination and management.

---

## Table of Contents

1. [Overview](#overview)
2. [Phase 1 Recap](#phase-1-recap)
3. [Phase 2 Objectives](#phase-2-objectives)
4. [Development Tasks](#development-tasks)
5. [Implementation Sequence](#implementation-sequence)
6. [Testing Strategy](#testing-strategy)
7. [Risk Mitigation](#risk-mitigation)
8. [Success Criteria](#success-criteria)

---

## Overview

Phase 2 builds upon the source abstraction foundation established in Phase 1. It adds support for multiple audio sources that can capture simultaneously, enabling users to record desktop audio, microphone, and per-application audio independently.

### Key Principles

1. **Multiple Sources:** Support 3+ audio sources running concurrently
2. **Independent Control:** Each source can be started/stopped independently
3. **Source Discovery:** Enumerate available audio devices and applications
4. **Coordinated Management:** SourceManager orchestrates multiple sources
5. **Backward Compatible:** Existing single-source recording still works
6. **C# Integration:** Expose source management to C# layer

### What Phase 2 Achieves

- ✅ MicrophoneAudioSource for capturing microphone input
- ✅ ApplicationAudioSource for per-app audio capture (Windows 11+)
- ✅ Audio device enumeration and selection
- ✅ SourceManager for coordinating multiple sources
- ✅ C# interfaces for source discovery and management
- ✅ Updated ScreenRecorder to use source abstraction
- ✅ Support for simultaneous desktop + microphone recording

### What Phase 2 Does NOT Do

- ❌ Audio mixing (that's Phase 3)
- ❌ Multi-track recording (that's Phase 3)
- ❌ Per-source volume control (Phase 3)
- ❌ Audio routing (Phase 3)
- ❌ UI for source management (Phase 5)

---

## Phase 1 Recap

### What We Built in Phase 1

**Interfaces:**
- `IMediaSource` - Base interface with lifecycle management
- `IVideoSource` - Video capture with frame callbacks
- `IAudioSource` - Audio capture with sample callbacks

**Implementations:**
- `ScreenCaptureSource` - Screen recording via Windows.Graphics.Capture
- `DesktopAudioSource` - Desktop audio via WASAPI loopback

**Infrastructure:**
- `FrameArrivedHandler` - Dual-path support (callback + legacy)
- Reference counting for all sources
- Callback-based data delivery

### Current State

The architecture is ready for multiple sources:
- Sources are independent (not coupled to MP4SinkWriter)
- Callbacks allow multiple consumers
- Thread-safe lifecycle management
- Pattern established for adding new sources

---

## Phase 2 Objectives

### Primary Goals

1. **Add MicrophoneAudioSource**
   - Capture from microphone (WASAPI capture endpoint, not loopback)
   - Device enumeration and selection
   - Same quality and timing as DesktopAudioSource

2. **Add ApplicationAudioSource**
   - Per-process audio capture via Audio Session API
   - Application enumeration (running apps with audio)
   - Process lifecycle tracking
   - Fallback for Windows 10 (no per-app capture)

3. **Create SourceManager**
   - Central coordinator for all sources
   - Thread-safe registration and unregistration
   - Unified start/stop for multiple sources
   - Source discovery and enumeration

4. **Update ScreenRecorder**
   - Migrate from legacy path to use new source classes
   - Support multiple audio sources
   - Maintain backward compatibility for single-source scenarios

5. **C# Integration**
   - Expose source enumeration to C# layer
   - P/Invoke wrappers for new functionality
   - Domain interfaces for audio device management

### Secondary Goals

- Device change notification (e.g., microphone unplugged)
- Hot-swap sources during recording
- Source state persistence (remember last selected devices)
- Error handling for device unavailability

---

## Development Tasks

### Task 1: Implement MicrophoneAudioSource

**Goal:** Create a microphone audio source that captures from WASAPI capture endpoint.

#### 1.1 Header File

**File:** `src/CaptureInterop/MicrophoneAudioSource.h`

**Class Structure:**
```cpp
#pragma once
#include "IAudioSource.h"
#include "AudioCaptureDevice.h"
#include <thread>
#include <atomic>
#include <vector>
#include <string>

/// <summary>
/// Audio source for microphone capture using WASAPI capture endpoint.
/// Similar to DesktopAudioSource but captures from microphone instead of loopback.
/// </summary>
class MicrophoneAudioSource : public IAudioSource
{
public:
    MicrophoneAudioSource();
    ~MicrophoneAudioSource();

    /// <summary>
    /// Set the device ID to capture from.
    /// Must be called before Initialize(). If not set, uses default microphone.
    /// </summary>
    /// <param name="deviceId">WASAPI device ID string.</param>
    void SetDeviceId(const std::wstring& deviceId);
    
    /// <summary>
    /// Get the currently selected device ID.
    /// </summary>
    std::wstring GetDeviceId() const;

    // IAudioSource implementation
    WAVEFORMATEX* GetFormat() const override;
    void SetAudioCallback(AudioSampleCallback callback) override;
    void SetEnabled(bool enabled) override;
    bool IsEnabled() const override;

    // IMediaSource implementation
    bool Initialize() override;
    bool Start() override;
    void Stop() override;
    bool IsRunning() const override;
    ULONG AddRef() override;
    ULONG Release() override;

private:
    // Reference counting
    volatile long m_ref = 1;

    // Configuration
    std::wstring m_deviceId;  // Empty = default device
    
    // Audio capture
    AudioCaptureDevice m_device;
    AudioSampleCallback m_callback;
    
    // Capture thread
    std::thread m_captureThread;
    std::atomic<bool> m_isRunning{false};
    std::atomic<bool> m_isEnabled{true};
    std::atomic<bool> m_isInitialized{false};
    
    // Synchronization
    LONGLONG m_startQpc = 0;
    LARGE_INTEGER m_qpcFrequency{};
    LONGLONG m_nextAudioTimestamp = 0;
    
    // Silent buffer management
    std::atomic<bool> m_wasDisabled{false};
    std::atomic<int> m_samplesToSkip{0};
    std::vector<BYTE> m_silentBuffer;

    // Thread procedure
    void CaptureThreadProc();
    
    // Cleanup
    void Cleanup();
};
```

#### 1.2 Implementation

**File:** `src/CaptureInterop/MicrophoneAudioSource.cpp`

**Key Differences from DesktopAudioSource:**
- Calls `m_device.Initialize(false, &hr)` (false = capture mode, not loopback)
- Optional device ID selection via SetDeviceId()
- No special handling for AUDCLNT_BUFFERFLAGS_SILENT (microphone silence is real)

**Implementation Notes:**
```cpp
bool MicrophoneAudioSource::Initialize()
{
    if (m_isInitialized)
    {
        return true;
    }

    HRESULT hr = S_OK;
    
    // Initialize in capture mode (false = capture endpoint, not loopback)
    // If m_deviceId is set, AudioCaptureDevice should use that specific device
    // Otherwise, use default capture device
    if (!m_device.Initialize(false, &hr))
    {
        return false;
    }

    // Pre-allocate silent buffer
    WAVEFORMATEX* format = m_device.GetFormat();
    if (format)
    {
        UINT32 bufferSize = (format->nSamplesPerSec / 100) * format->nBlockAlign;
        m_silentBuffer.resize(bufferSize, 0);
    }

    m_isInitialized = true;
    return true;
}
```

#### 1.3 AudioCaptureDevice Enhancement

**File:** `src/CaptureInterop/AudioCaptureDevice.h`

**Add Method:**
```cpp
/// <summary>
/// Initialize with specific device ID.
/// </summary>
/// <param name="deviceId">WASAPI device ID. Empty string for default device.</param>
/// <param name="loopback">True for loopback (desktop audio), false for capture (microphone).</param>
/// <param name="outHr">Optional pointer to receive error code.</param>
/// <returns>True if initialization succeeded.</returns>
bool InitializeWithDevice(const std::wstring& deviceId, bool loopback, HRESULT* outHr = nullptr);
```

**Implementation:**
```cpp
bool AudioCaptureDevice::InitializeWithDevice(const std::wstring& deviceId, bool loopback, HRESULT* outHr)
{
    // Initialize COM
    HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    if (FAILED(hr) && hr != RPC_E_CHANGED_MODE)
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Create device enumerator
    hr = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL,
                          __uuidof(IMMDeviceEnumerator), m_deviceEnumerator.put_void());
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Get device by ID or default
    if (!deviceId.empty())
    {
        hr = m_deviceEnumerator->GetDevice(deviceId.c_str(), m_device.put());
    }
    else
    {
        EDataFlow dataFlow = loopback ? eRender : eCapture;
        hr = m_deviceEnumerator->GetDefaultAudioEndpoint(dataFlow, eConsole, m_device.put());
    }
    
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Rest of initialization (activate, get format, etc.)
    // ... (existing code)
}
```

#### 1.4 Testing

**Unit Tests:**
- Initialize with default microphone
- Initialize with specific device ID
- Start/Stop lifecycle
- Callback invocation
- Enable/Disable control
- Timestamp accuracy

**Integration Tests:**
- Capture actual microphone audio
- Verify format matches device capability
- Test device change scenarios

**Acceptance Criteria:**
- ✅ Microphone audio captured successfully
- ✅ Device selection works (default or by ID)
- ✅ Same timestamp precision as DesktopAudioSource
- ✅ Enable/Disable muting works
- ✅ Compiles without warnings

---

### Task 2: Implement Audio Device Enumeration

**Goal:** Provide ability to list available audio devices (microphones, speakers).

#### 2.1 Device Enumerator Class

**File:** `src/CaptureInterop/AudioDeviceEnumerator.h`

**Structure:**
```cpp
#pragma once
#include <vector>
#include <string>
#include <mmdeviceapi.h>

/// <summary>
/// Information about an audio device.
/// </summary>
struct AudioDeviceInfo
{
    std::wstring deviceId;       // Unique device identifier
    std::wstring friendlyName;   // Human-readable name
    std::wstring description;    // Device description
    bool isDefault;              // True if this is the default device
    bool isLoopback;             // True for render devices (desktop audio), false for capture
};

/// <summary>
/// Enumerates audio devices using WASAPI.
/// </summary>
class AudioDeviceEnumerator
{
public:
    AudioDeviceEnumerator();
    ~AudioDeviceEnumerator();

    /// <summary>
    /// Enumerate all audio capture devices (microphones).
    /// </summary>
    /// <param name="devices">Output vector to receive device information.</param>
    /// <returns>True if enumeration succeeded.</returns>
    bool EnumerateCaptureDevices(std::vector<AudioDeviceInfo>& devices);
    
    /// <summary>
    /// Enumerate all audio render devices (speakers, for loopback).
    /// </summary>
    /// <param name="devices">Output vector to receive device information.</param>
    /// <returns>True if enumeration succeeded.</returns>
    bool EnumerateRenderDevices(std::vector<AudioDeviceInfo>& devices);
    
    /// <summary>
    /// Get the default capture device.
    /// </summary>
    /// <param name="deviceInfo">Output parameter to receive device information.</param>
    /// <returns>True if successful.</returns>
    bool GetDefaultCaptureDevice(AudioDeviceInfo& deviceInfo);
    
    /// <summary>
    /// Get the default render device.
    /// </summary>
    /// <param name="deviceInfo">Output parameter to receive device information.</param>
    /// <returns>True if successful.</returns>
    bool GetDefaultRenderDevice(AudioDeviceInfo& deviceInfo);

private:
    bool EnumerateDevices(EDataFlow dataFlow, std::vector<AudioDeviceInfo>& devices);
    bool GetDeviceInfo(IMMDevice* device, bool isLoopback, AudioDeviceInfo& info);
    
    wil::com_ptr<IMMDeviceEnumerator> m_enumerator;
};
```

#### 2.2 C++ Export Functions

**File:** `src/CaptureInterop/ScreenRecorder.h` (add to exports)

```cpp
extern "C"
{
    // Existing exports...
    
    /// <summary>
    /// Enumerate audio capture devices (microphones).
    /// </summary>
    /// <param name="devices">Pointer to receive array of device info.</param>
    /// <param name="count">Pointer to receive device count.</param>
    /// <returns>True if enumeration succeeded.</returns>
    __declspec(dllexport) bool EnumerateAudioCaptureDevices(
        AudioDeviceInfo** devices,
        int* count
    );
    
    /// <summary>
    /// Free memory allocated by EnumerateAudioCaptureDevices.
    /// </summary>
    /// <param name="devices">Pointer to device array.</param>
    __declspec(dllexport) void FreeAudioDeviceInfo(AudioDeviceInfo* devices);
}
```

#### 2.3 C# Integration

**File:** `src/CaptureTool.Domains.Capture.Interfaces/IAudioDeviceEnumerator.cs` (new)

```csharp
namespace CaptureTool.Domains.Capture.Interfaces;

public record AudioDeviceInfo(
    string DeviceId,
    string FriendlyName,
    string Description,
    bool IsDefault,
    AudioDeviceType DeviceType
);

public enum AudioDeviceType
{
    Capture,   // Microphone
    Render     // Desktop audio
}

public interface IAudioDeviceEnumerator
{
    /// <summary>
    /// Get all available capture devices (microphones).
    /// </summary>
    Task<IReadOnlyList<AudioDeviceInfo>> EnumerateCaptureDevicesAsync();
    
    /// <summary>
    /// Get all available render devices (for desktop audio).
    /// </summary>
    Task<IReadOnlyList<AudioDeviceInfo>> EnumerateRenderDevicesAsync();
    
    /// <summary>
    /// Get the default capture device.
    /// </summary>
    Task<AudioDeviceInfo?> GetDefaultCaptureDeviceAsync();
    
    /// <summary>
    /// Get the default render device.
    /// </summary>
    Task<AudioDeviceInfo?> GetDefaultRenderDeviceAsync();
}
```

**File:** `src/CaptureTool.Domains.Capture.Implementations.Windows/WindowsAudioDeviceEnumerator.cs` (new)

```csharp
using System.Runtime.InteropServices;

namespace CaptureTool.Domains.Capture.Implementations.Windows;

internal partial class WindowsAudioDeviceEnumerator : IAudioDeviceEnumerator
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeAudioDeviceInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string DeviceId;
        
        [MarshalAs(UnmanagedType.LPWStr)]
        public string FriendlyName;
        
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Description;
        
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsDefault;
        
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsLoopback;
    }
    
    [LibraryImport("CaptureInterop.dll", EntryPoint = "EnumerateAudioCaptureDevices")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumerateAudioCaptureDevices(
        out IntPtr devices,
        out int count
    );
    
    [LibraryImport("CaptureInterop.dll", EntryPoint = "FreeAudioDeviceInfo")]
    private static partial void FreeAudioDeviceInfo(IntPtr devices);
    
    public async Task<IReadOnlyList<AudioDeviceInfo>> EnumerateCaptureDevicesAsync()
    {
        return await Task.Run(() =>
        {
            if (!EnumerateAudioCaptureDevices(out IntPtr devicesPtr, out int count))
            {
                return Array.Empty<AudioDeviceInfo>();
            }
            
            try
            {
                var devices = new List<AudioDeviceInfo>(count);
                int structSize = Marshal.SizeOf<NativeAudioDeviceInfo>();
                
                for (int i = 0; i < count; i++)
                {
                    IntPtr ptr = IntPtr.Add(devicesPtr, i * structSize);
                    var nativeInfo = Marshal.PtrToStructure<NativeAudioDeviceInfo>(ptr);
                    
                    devices.Add(new AudioDeviceInfo(
                        nativeInfo.DeviceId,
                        nativeInfo.FriendlyName,
                        nativeInfo.Description,
                        nativeInfo.IsDefault,
                        nativeInfo.IsLoopback ? AudioDeviceType.Render : AudioDeviceType.Capture
                    ));
                }
                
                return devices;
            }
            finally
            {
                FreeAudioDeviceInfo(devicesPtr);
            }
        });
    }
    
    // Similar implementations for other methods...
}
```

#### 2.4 Acceptance Criteria

- ✅ Enumerate all connected microphones
- ✅ Identify default microphone
- ✅ Device names are user-friendly
- ✅ C# integration works
- ✅ Memory properly freed (no leaks)

---

### Task 3: Implement ApplicationAudioSource

**Goal:** Capture per-application audio on Windows 11 22H2+.

#### 3.1 Windows Version Detection

**File:** `src/CaptureInterop/WindowsVersionHelper.h` (new)

```cpp
#pragma once
#include <Windows.h>

/// <summary>
/// Helper for detecting Windows version.
/// </summary>
class WindowsVersionHelper
{
public:
    /// <summary>
    /// Check if running on Windows 11 22H2 or later.
    /// Required for per-application audio capture.
    /// </summary>
    static bool IsWindows11_22H2OrLater();
    
    /// <summary>
    /// Get Windows build number.
    /// </summary>
    static DWORD GetBuildNumber();
};
```

**Implementation:**
```cpp
bool WindowsVersionHelper::IsWindows11_22H2OrLater()
{
    // Windows 11 22H2 is build 22621
    return GetBuildNumber() >= 22621;
}

DWORD WindowsVersionHelper::GetBuildNumber()
{
    // Use RtlGetVersion to get true Windows version
    // (GetVersionEx lies about version)
    typedef LONG(WINAPI* RtlGetVersionPtr)(PRTL_OSVERSIONINFOW);
    
    HMODULE ntdll = GetModuleHandleW(L"ntdll.dll");
    if (!ntdll)
        return 0;
    
    auto RtlGetVersion = (RtlGetVersionPtr)GetProcAddress(ntdll, "RtlGetVersion");
    if (!RtlGetVersion)
        return 0;
    
    RTL_OSVERSIONINFOW versionInfo = { sizeof(versionInfo) };
    if (RtlGetVersion(&versionInfo) == 0)
    {
        return versionInfo.dwBuildNumber;
    }
    
    return 0;
}
```

#### 3.2 Application Audio Session API

**File:** `src/CaptureInterop/ApplicationAudioSource.h`

**Key Concepts:**
- Windows Audio Session API (WASAPI2) added per-process audio in Windows 11 22H2
- Each running application with audio has an audio session
- Can capture specific session's audio stream

**Class Structure:**
```cpp
#pragma once
#include "IAudioSource.h"
#include <audioclient.h>
#include <audiopolicy.h>
#include <thread>
#include <atomic>
#include <string>

/// <summary>
/// Audio source for per-application audio capture (Windows 11 22H2+).
/// Uses Audio Session API to isolate specific application's audio.
/// </summary>
class ApplicationAudioSource : public IAudioSource
{
public:
    ApplicationAudioSource();
    ~ApplicationAudioSource();

    /// <summary>
    /// Set the process ID to capture audio from.
    /// Must be called before Initialize().
    /// </summary>
    void SetProcessId(DWORD processId);
    
    /// <summary>
    /// Get the current process ID.
    /// </summary>
    DWORD GetProcessId() const;
    
    /// <summary>
    /// Check if per-application capture is supported on this system.
    /// </summary>
    static bool IsSupported();

    // IAudioSource implementation
    WAVEFORMATEX* GetFormat() const override;
    void SetAudioCallback(AudioSampleCallback callback) override;
    void SetEnabled(bool enabled) override;
    bool IsEnabled() const override;

    // IMediaSource implementation
    bool Initialize() override;
    bool Start() override;
    void Stop() override;
    bool IsRunning() const override;
    ULONG AddRef() override;
    ULONG Release() override;

private:
    volatile long m_ref = 1;
    DWORD m_processId = 0;
    
    // WASAPI2 components
    wil::com_ptr<IAudioClient3> m_audioClient;
    wil::com_ptr<IAudioCaptureClient> m_captureClient;
    WAVEFORMATEX* m_format = nullptr;
    
    AudioSampleCallback m_callback;
    std::thread m_captureThread;
    std::atomic<bool> m_isRunning{false};
    std::atomic<bool> m_isEnabled{true};
    std::atomic<bool> m_isInitialized{false};
    
    // Synchronization
    LONGLONG m_startQpc = 0;
    LARGE_INTEGER m_qpcFrequency{};
    LONGLONG m_nextAudioTimestamp = 0;
    
    std::atomic<bool> m_wasDisabled{false};
    std::atomic<int> m_samplesToSkip{0};
    std::vector<BYTE> m_silentBuffer;

    void CaptureThreadProc();
    void Cleanup();
    
    bool InitializeAudioSessionForProcess();
};
```

#### 3.3 Process Audio Session Enumeration

**File:** `src/CaptureInterop/AudioSessionEnumerator.h` (new)

```cpp
#pragma once
#include <vector>
#include <string>
#include <Windows.h>

struct AudioSessionInfo
{
    DWORD processId;
    std::wstring processName;
    std::wstring displayName;
    bool isActive;
};

/// <summary>
/// Enumerates active audio sessions (applications with audio).
/// </summary>
class AudioSessionEnumerator
{
public:
    /// <summary>
    /// Get all active audio sessions.
    /// </summary>
    static bool EnumerateActiveSessions(std::vector<AudioSessionInfo>& sessions);
    
    /// <summary>
    /// Check if a specific process has an audio session.
    /// </summary>
    static bool HasAudioSession(DWORD processId);
};
```

**Implementation Notes:**
- Use `IMMDeviceEnumerator` to get default render device
- Use `IAudioSessionManager2` to enumerate sessions
- Query `IAudioSessionControl2::GetProcessId()` for each session
- Filter for active sessions only

#### 3.4 Fallback for Windows 10

For Windows 10 (no per-app audio), provide graceful degradation:

```cpp
bool ApplicationAudioSource::Initialize()
{
    if (!IsSupported())
    {
        // Fallback: Log warning and fail gracefully
        // Or: Capture all desktop audio with a warning
        return false;
    }
    
    // Windows 11 22H2+ path...
}

bool ApplicationAudioSource::IsSupported()
{
    return WindowsVersionHelper::IsWindows11_22H2OrLater();
}
```

#### 3.5 C# Integration

**File:** `src/CaptureTool.Domains.Capture.Interfaces/IApplicationAudioEnumerator.cs` (new)

```csharp
public record ApplicationAudioInfo(
    int ProcessId,
    string ProcessName,
    string DisplayName,
    bool IsActive
);

public interface IApplicationAudioEnumerator
{
    /// <summary>
    /// Check if per-application audio capture is supported on this system.
    /// </summary>
    bool IsSupported { get; }
    
    /// <summary>
    /// Get all applications with active audio sessions.
    /// </summary>
    Task<IReadOnlyList<ApplicationAudioInfo>> EnumerateActiveApplicationsAsync();
    
    /// <summary>
    /// Check if a specific process has audio.
    /// </summary>
    Task<bool> HasAudioSessionAsync(int processId);
}
```

#### 3.6 Acceptance Criteria

- ✅ ApplicationAudioSource compiles
- ✅ IsSupported() returns correct result based on Windows version
- ✅ On Windows 11 22H2+: Can capture specific app's audio
- ✅ On Windows 10: Fails gracefully with clear error
- ✅ Audio session enumeration works
- ✅ Process lifecycle tracked (app closes → source stops)

---

### Task 4: Create SourceManager

**Goal:** Central coordinator for managing multiple sources.

#### 4.1 Source Handle System

**File:** `src/CaptureInterop/SourceHandle.h` (new)

```cpp
#pragma once
#include <cstdint>

/// <summary>
/// Opaque handle to a registered source.
/// </summary>
using SourceHandle = uint64_t;

constexpr SourceHandle INVALID_SOURCE_HANDLE = 0;
```

#### 4.2 SourceManager Class

**File:** `src/CaptureInterop/SourceManager.h`

```cpp
#pragma once
#include "IMediaSource.h"
#include "IVideoSource.h"
#include "IAudioSource.h"
#include "SourceHandle.h"
#include <vector>
#include <unordered_map>
#include <mutex>
#include <memory>

/// <summary>
/// Manages lifecycle and coordination of multiple capture sources.
/// Thread-safe singleton for global source management.
/// </summary>
class SourceManager
{
public:
    /// <summary>
    /// Get the singleton instance.
    /// </summary>
    static SourceManager& Instance();
    
    /// <summary>
    /// Register a new source.
    /// Takes ownership of the source pointer.
    /// </summary>
    /// <param name="source">Source to register. Must not be null.</param>
    /// <returns>Handle to the registered source, or INVALID_SOURCE_HANDLE on failure.</returns>
    SourceHandle RegisterSource(IMediaSource* source);
    
    /// <summary>
    /// Unregister and release a source.
    /// Safe to call even if source is running (will stop first).
    /// </summary>
    /// <param name="handle">Handle of source to unregister.</param>
    void UnregisterSource(SourceHandle handle);
    
    /// <summary>
    /// Get a source by handle.
    /// </summary>
    /// <param name="handle">Source handle.</param>
    /// <returns>Pointer to source, or nullptr if handle invalid.</returns>
    IMediaSource* GetSource(SourceHandle handle);
    
    /// <summary>
    /// Get all video sources.
    /// </summary>
    std::vector<IVideoSource*> GetVideoSources();
    
    /// <summary>
    /// Get all audio sources.
    /// </summary>
    std::vector<IAudioSource*> GetAudioSources();
    
    /// <summary>
    /// Start all registered sources.
    /// </summary>
    /// <returns>True if all sources started successfully.</returns>
    bool StartAll();
    
    /// <summary>
    /// Stop all registered sources.
    /// </summary>
    void StopAll();
    
    /// <summary>
    /// Get count of registered sources.
    /// </summary>
    size_t GetSourceCount() const;
    
    /// <summary>
    /// Clear all sources (stops and unregisters).
    /// </summary>
    void Clear();

private:
    SourceManager() = default;
    ~SourceManager();
    
    // Non-copyable
    SourceManager(const SourceManager&) = delete;
    SourceManager& operator=(const SourceManager&) = delete;
    
    struct SourceEntry
    {
        IMediaSource* source;
        SourceHandle handle;
    };
    
    std::unordered_map<SourceHandle, SourceEntry> m_sources;
    mutable std::mutex m_mutex;
    SourceHandle m_nextHandle = 1;
    
    SourceHandle GenerateHandle();
};
```

#### 4.3 Implementation

**File:** `src/CaptureInterop/SourceManager.cpp`

**Key Implementation Details:**

```cpp
SourceManager& SourceManager::Instance()
{
    static SourceManager instance;
    return instance;
}

SourceHandle SourceManager::RegisterSource(IMediaSource* source)
{
    if (!source)
    {
        return INVALID_SOURCE_HANDLE;
    }
    
    std::lock_guard<std::mutex> lock(m_mutex);
    
    SourceHandle handle = GenerateHandle();
    
    SourceEntry entry;
    entry.source = source;
    entry.handle = handle;
    
    m_sources[handle] = entry;
    
    // AddRef since we're holding a reference
    source->AddRef();
    
    return handle;
}

void SourceManager::UnregisterSource(SourceHandle handle)
{
    IMediaSource* source = nullptr;
    
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        
        auto it = m_sources.find(handle);
        if (it == m_sources.end())
        {
            return;
        }
        
        source = it->second.source;
        m_sources.erase(it);
    }
    
    // Stop and release outside the lock to avoid deadlock
    if (source)
    {
        if (source->IsRunning())
        {
            source->Stop();
        }
        source->Release();
    }
}

std::vector<IVideoSource*> SourceManager::GetVideoSources()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    std::vector<IVideoSource*> videoSources;
    
    for (const auto& [handle, entry] : m_sources)
    {
        if (entry.source->GetSourceType() == MediaSourceType::Video)
        {
            videoSources.push_back(static_cast<IVideoSource*>(entry.source));
        }
    }
    
    return videoSources;
}

bool SourceManager::StartAll()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    bool allSucceeded = true;
    
    for (const auto& [handle, entry] : m_sources)
    {
        if (!entry.source->IsRunning())
        {
            if (!entry.source->Start())
            {
                allSucceeded = false;
                // Continue starting others even if one fails
            }
        }
    }
    
    return allSucceeded;
}

void SourceManager::StopAll()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    for (const auto& [handle, entry] : m_sources)
    {
        if (entry.source->IsRunning())
        {
            entry.source->Stop();
        }
    }
}
```

#### 4.4 C++ Export Functions

**File:** `src/CaptureInterop/ScreenRecorder.h` (add to exports)

```cpp
extern "C"
{
    // Source management exports
    __declspec(dllexport) SourceHandle RegisterVideoSource(void* sourcePtr);
    __declspec(dllexport) SourceHandle RegisterAudioSource(void* sourcePtr);
    __declspec(dllexport) void UnregisterSource(SourceHandle handle);
    __declspec(dllexport) bool StartAllSources();
    __declspec(dllexport) void StopAllSources();
    __declspec(dllexport) int GetSourceCount();
}
```

#### 4.5 Acceptance Criteria

- ✅ Thread-safe source registration/unregistration
- ✅ Handles are unique and stable
- ✅ StartAll/StopAll work with multiple sources
- ✅ Sources properly released (no leaks)
- ✅ GetVideoSources/GetAudioSources filter correctly

---

### Task 5: Update ScreenRecorder to Use Source Abstraction

**Goal:** Migrate ScreenRecorder from legacy direct calls to using the new source classes.

#### 5.1 New Global State

**File:** `src/CaptureInterop/ScreenRecorder.cpp`

**Replace:**
```cpp
// Old globals
static wil::com_ptr<IGraphicsCaptureSession> g_session;
static wil::com_ptr<IDirect3D11CaptureFramePool> g_framePool;
static EventRegistrationToken g_frameArrivedEventToken;
static FrameArrivedHandler* g_frameHandler = nullptr;
static MP4SinkWriter g_sinkWriter;
static AudioCaptureHandler g_audioHandler;
```

**With:**
```cpp
// New source-based globals
static ScreenCaptureSource* g_videoSource = nullptr;
static DesktopAudioSource* g_desktopAudioSource = nullptr;
static MicrophoneAudioSource* g_microphoneSource = nullptr;
static MP4SinkWriter g_sinkWriter;
static D3DDeviceAndContext g_d3dDevice;
```

#### 5.2 Updated TryStartRecording

**New Implementation:**
```cpp
__declspec(dllexport) bool TryStartRecording(
    HMONITOR hMonitor,
    const wchar_t* outputPath,
    bool captureDesktopAudio,
    bool captureMicrophone)  // NEW PARAMETER
{
    HRESULT hr = S_OK;
    
    // Initialize D3D11 device (reused for video)
    g_d3dDevice = InitializeD3D(&hr);
    if (FAILED(hr))
    {
        return false;
    }
    
    // Create and initialize video source
    g_videoSource = new ScreenCaptureSource();
    g_videoSource->SetMonitor(hMonitor);
    g_videoSource->SetDevice(g_d3dDevice.device.get());
    
    if (!g_videoSource->Initialize())
    {
        g_videoSource->Release();
        g_videoSource = nullptr;
        return false;
    }
    
    // Get video resolution
    UINT32 width, height;
    g_videoSource->GetResolution(width, height);
    
    // Initialize MP4 sink writer
    if (!g_sinkWriter.Initialize(outputPath, g_d3dDevice.device.get(), width, height, &hr))
    {
        g_videoSource->Release();
        g_videoSource = nullptr;
        return false;
    }
    
    // Set up video callback
    g_videoSource->SetFrameCallback([](ID3D11Texture2D* texture, LONGLONG timestamp) {
        g_sinkWriter.WriteFrame(texture, timestamp);
    });
    
    // Create desktop audio source if requested
    bool desktopAudioEnabled = false;
    if (captureDesktopAudio)
    {
        g_desktopAudioSource = new DesktopAudioSource();
        
        if (g_desktopAudioSource->Initialize())
        {
            WAVEFORMATEX* audioFormat = g_desktopAudioSource->GetFormat();
            if (audioFormat && g_sinkWriter.InitializeAudioStream(audioFormat, &hr))
            {
                g_desktopAudioSource->SetAudioCallback([](const BYTE* data, UINT32 frames, LONGLONG ts) {
                    g_sinkWriter.WriteAudioSample(data, frames, ts);
                });
                
                if (g_desktopAudioSource->Start(&hr))
                {
                    desktopAudioEnabled = true;
                }
            }
        }
        
        if (!desktopAudioEnabled && g_desktopAudioSource)
        {
            g_desktopAudioSource->Release();
            g_desktopAudioSource = nullptr;
        }
    }
    
    // Create microphone source if requested
    bool microphoneEnabled = false;
    if (captureMicrophone)
    {
        g_microphoneSource = new MicrophoneAudioSource();
        
        if (g_microphoneSource->Initialize())
        {
            WAVEFORMATEX* micFormat = g_microphoneSource->GetFormat();
            
            // For Phase 2: both audio sources write to same track
            // Phase 3 will add mixing and multi-track support
            if (micFormat)
            {
                g_microphoneSource->SetAudioCallback([](const BYTE* data, UINT32 frames, LONGLONG ts) {
                    // TODO Phase 3: Route to mixer instead of direct write
                    // For now, just capture (no write to avoid conflicts)
                });
                
                if (g_microphoneSource->Start(&hr))
                {
                    microphoneEnabled = true;
                }
            }
        }
        
        if (!microphoneEnabled && g_microphoneSource)
        {
            g_microphoneSource->Release();
            g_microphoneSource = nullptr;
        }
    }
    
    // Start video capture
    if (!g_videoSource->Start())
    {
        // Cleanup on failure
        if (g_desktopAudioSource)
        {
            g_desktopAudioSource->Stop();
            g_desktopAudioSource->Release();
            g_desktopAudioSource = nullptr;
        }
        if (g_microphoneSource)
        {
            g_microphoneSource->Stop();
            g_microphoneSource->Release();
            g_microphoneSource = nullptr;
        }
        g_videoSource->Release();
        g_videoSource = nullptr;
        return false;
    }
    
    return true;
}
```

#### 5.3 Updated TryStopRecording

```cpp
__declspec(dllexport) void TryStopRecording()
{
    // Stop all audio sources first
    if (g_microphoneSource)
    {
        g_microphoneSource->Stop();
        g_microphoneSource->Release();
        g_microphoneSource = nullptr;
    }
    
    if (g_desktopAudioSource)
    {
        g_desktopAudioSource->Stop();
        g_desktopAudioSource->Release();
        g_desktopAudioSource = nullptr;
    }
    
    // Stop video source
    if (g_videoSource)
    {
        g_videoSource->Stop();
        g_videoSource->Release();
        g_videoSource = nullptr;
    }
    
    // Finalize MP4 file
    g_sinkWriter.Finalize();
    
    // Reset sink writer to fresh state
    g_sinkWriter = MP4SinkWriter();
}
```

#### 5.4 Updated TryToggleAudioCapture

```cpp
__declspec(dllexport) void TryToggleAudioCapture(bool enabled)
{
    if (g_desktopAudioSource)
    {
        g_desktopAudioSource->SetEnabled(enabled);
    }
    
    // Note: For Phase 2, we toggle desktop audio only
    // Phase 3 will add per-source control in UI
}
```

#### 5.5 Backward Compatibility

For backward compatibility, add overload:

```cpp
// Legacy signature (for existing callers)
__declspec(dllexport) bool TryStartRecording(
    HMONITOR hMonitor,
    const wchar_t* outputPath,
    bool captureAudio)
{
    // Call new version with microphone=false
    return TryStartRecording(hMonitor, outputPath, captureAudio, false);
}
```

#### 5.6 Acceptance Criteria

- ✅ Existing single-source recording still works
- ✅ Can record desktop + microphone simultaneously
- ✅ No performance regression
- ✅ All sources properly cleaned up
- ✅ Backward compatible with existing P/Invoke calls

---

### Task 6: C# Layer Integration

**Goal:** Expose new functionality to C# layer.

#### 6.1 Update IVideoCaptureHandler

**File:** `src/CaptureTool.Domains.Capture.Interfaces/IVideoCaptureHandler.cs`

**Add Methods:**
```csharp
public partial interface IVideoCaptureHandler
{
    // Existing members...
    
    /// <summary>
    /// Check if microphone capture is enabled.
    /// </summary>
    bool IsMicrophoneEnabled { get; }
    
    /// <summary>
    /// Enable or disable microphone capture.
    /// Must be called before starting recording.
    /// </summary>
    void SetIsMicrophoneEnabled(bool value);
}
```

#### 6.2 Update WindowsScreenRecorder

**File:** `src/CaptureTool.Domains.Capture.Implementations.Windows/WindowsScreenRecorder.cs`

**Add:**
```csharp
public partial class WindowsScreenRecorder : IScreenRecorder
{
    [LibraryImport("CaptureInterop.dll", EntryPoint = "TryStartRecording")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TryStartRecordingWithMicrophone(
        IntPtr hMonitor,
        [MarshalAs(UnmanagedType.LPWStr)] string outputPath,
        [MarshalAs(UnmanagedType.Bool)] bool captureDesktopAudio,
        [MarshalAs(UnmanagedType.Bool)] bool captureMicrophone
    );
    
    public bool StartRecording(
        IntPtr hMonitor,
        string outputPath,
        bool captureDesktopAudio,
        bool captureMicrophone)
    {
        return TryStartRecordingWithMicrophone(
            hMonitor,
            outputPath,
            captureDesktopAudio,
            captureMicrophone
        );
    }
}
```

#### 6.3 Dependency Injection

**File:** `src/CaptureTool.Domains.Capture.Implementations.Windows/DependencyInjection/ServiceCollectionExtensions.cs`

**Add:**
```csharp
public static IServiceCollection AddWindowsCaptureImplementations(this IServiceCollection services)
{
    // Existing registrations...
    
    services.AddSingleton<IAudioDeviceEnumerator, WindowsAudioDeviceEnumerator>();
    services.AddSingleton<IApplicationAudioEnumerator, WindowsApplicationAudioEnumerator>();
    
    return services;
}
```

#### 6.4 Acceptance Criteria

- ✅ C# interfaces compile
- ✅ P/Invoke marshalling works
- ✅ Dependency injection configured
- ✅ Can call from C# layer

---

## Implementation Sequence

### Week 1: Microphone and Device Enumeration

**Days 1-2: MicrophoneAudioSource**
- [ ] Create MicrophoneAudioSource.h
- [ ] Implement MicrophoneAudioSource.cpp
- [ ] Enhance AudioCaptureDevice for device selection
- [ ] Unit tests
- [ ] Verify microphone capture works

**Days 3-4: Audio Device Enumeration**
- [ ] Create AudioDeviceEnumerator.h/cpp
- [ ] Implement device enumeration (capture & render)
- [ ] Add C++ exports
- [ ] Create C# interfaces and P/Invoke wrappers
- [ ] Integration tests

**Day 5: Testing and Bug Fixes**
- [ ] Test device enumeration in various scenarios
- [ ] Test microphone with different devices
- [ ] Fix any issues found

### Week 2: Application Audio and Version Detection

**Days 1-2: Windows Version Detection**
- [ ] Create WindowsVersionHelper.h/cpp
- [ ] Implement version detection
- [ ] Test on Windows 10 and Windows 11
- [ ] Add unit tests

**Days 3-5: ApplicationAudioSource**
- [ ] Create ApplicationAudioSource.h
- [ ] Implement ApplicationAudioSource.cpp (Windows 11 path)
- [ ] Create AudioSessionEnumerator.h/cpp
- [ ] Implement graceful fallback for Windows 10
- [ ] Unit tests
- [ ] Integration tests on Windows 11

### Week 3: SourceManager and Integration

**Days 1-2: SourceManager**
- [ ] Create SourceManager.h
- [ ] Implement SourceManager.cpp
- [ ] Add C++ exports
- [ ] Unit tests for thread safety
- [ ] Stress tests (many sources)

**Days 3-4: Update ScreenRecorder**
- [ ] Refactor TryStartRecording to use sources
- [ ] Update TryStopRecording
- [ ] Maintain backward compatibility
- [ ] Integration tests

**Day 5: C# Layer Integration**
- [ ] Update C# interfaces
- [ ] Implement P/Invoke wrappers
- [ ] Configure dependency injection
- [ ] End-to-end tests

### Week 4: Testing and Documentation

**Days 1-2: Comprehensive Testing**
- [ ] Desktop + microphone simultaneously
- [ ] Device selection tests
- [ ] Application audio tests (Windows 11)
- [ ] Fallback tests (Windows 10)
- [ ] Performance tests
- [ ] Long-duration stability tests

**Days 3-4: Documentation**
- [ ] Update internal README
- [ ] Code comments
- [ ] Usage examples
- [ ] Migration guide from Phase 1

**Day 5: Final Verification and Polish**
- [ ] Full regression test suite
- [ ] Performance benchmarks
- [ ] Memory leak detection
- [ ] Code review
- [ ] Prepare for merge

---

## Testing Strategy

### Unit Tests

**MicrophoneAudioSource Tests:**
```cpp
TEST(MicrophoneAudioSource, InitializeWithDefaultDevice)
TEST(MicrophoneAudioSource, InitializeWithSpecificDevice)
TEST(MicrophoneAudioSource, StartStopLifecycle)
TEST(MicrophoneAudioSource, CallbackInvoked)
TEST(MicrophoneAudioSource, EnableDisableControl)
TEST(MicrophoneAudioSource, DeviceNotFound)
```

**ApplicationAudioSource Tests:**
```cpp
TEST(ApplicationAudioSource, IsSupportedOnWindows11)
TEST(ApplicationAudioSource, IsSupportedOnWindows10)
TEST(ApplicationAudioSource, InitializeWithValidProcess)
TEST(ApplicationAudioSource, InitializeWithInvalidProcess)
TEST(ApplicationAudioSource, ProcessTerminationHandling)
```

**SourceManager Tests:**
```cpp
TEST(SourceManager, RegisterUnregisterSource)
TEST(SourceManager, GetVideoSources)
TEST(SourceManager, GetAudioSources)
TEST(SourceManager, StartStopAll)
TEST(SourceManager, ThreadSafety)
TEST(SourceManager, HandleLifecycle)
```

**AudioDeviceEnumerator Tests:**
```cpp
TEST(AudioDeviceEnumerator, EnumerateCaptureDevices)
TEST(AudioDeviceEnumerator, EnumerateRenderDevices)
TEST(AudioDeviceEnumerator, GetDefaultCaptureDevice)
TEST(AudioDeviceEnumerator, NoDevicesScenario)
```

### Integration Tests

**Multi-Source Recording:**
- [ ] Desktop audio only
- [ ] Microphone only
- [ ] Desktop + microphone simultaneously
- [ ] Screen + desktop + microphone
- [ ] Application audio (Windows 11)

**Device Scenarios:**
- [ ] Default device selection
- [ ] Specific device selection
- [ ] Device not available (disconnected)
- [ ] Device change during recording
- [ ] Multiple devices of same type

**Process Lifecycle:**
- [ ] Application starts audio → capture
- [ ] Application stops audio → handle gracefully
- [ ] Application terminates → source stops

### Performance Tests

**Metrics to Measure:**
- [ ] CPU usage with 1 audio source (baseline from Phase 1)
- [ ] CPU usage with 2 audio sources (desktop + mic)
- [ ] CPU usage with 3 audio sources (desktop + mic + app)
- [ ] Memory usage with multiple sources
- [ ] Frame drop rate (should be 0)
- [ ] Audio/video sync accuracy (<10ms)
- [ ] Time to enumerate devices (<100ms)

**Test Scenarios:**
- [ ] 5-minute recording with 2 audio sources
- [ ] 30-minute recording (stability)
- [ ] Device enumeration 100 times (performance)
- [ ] Register/unregister sources 1000 times (stress test)

### Regression Tests

- [ ] All Phase 1 tests still pass
- [ ] Existing single-source recording works
- [ ] No new memory leaks
- [ ] No performance degradation vs Phase 1
- [ ] Backward compatibility maintained

---

## Risk Mitigation

### Risk 1: Audio Synchronization with Multiple Sources

**Likelihood:** High  
**Impact:** High

**Problem:** Multiple audio sources with independent clocks may drift.

**Mitigation:**
- All sources use same QPC-based reference clock
- Test synchronization extensively
- Accumulated timestamps prevent drift
- Monitor sync offset during long recordings

**Testing:**
- Record desktop + mic for 10 minutes
- Analyze audio tracks in editor
- Measure sync offset at start, middle, end
- Verify <10ms drift

### Risk 2: Windows Version Compatibility

**Likelihood:** Medium  
**Impact:** Medium

**Problem:** ApplicationAudioSource only works on Windows 11 22H2+.

**Mitigation:**
- IsSupported() check before attempting
- Graceful fallback for unsupported systems
- Clear error messages for users
- Document Windows version requirements

**Testing:**
- Test on Windows 10 21H2, 22H2
- Test on Windows 11 21H2, 22H2, 23H2
- Verify fallback behavior
- User-friendly error messages

### Risk 3: Device Enumeration Failures

**Likelihood:** Low  
**Impact:** Medium

**Problem:** Device enumeration may fail or return incomplete data.

**Mitigation:**
- Robust error handling in enumeration code
- Fallback to default device if enumeration fails
- Cache enumeration results (don't enumerate every time)
- Handle COM initialization failures

**Testing:**
- Test with no audio devices
- Test with disconnected device selected
- Test with many devices (10+)
- Test during device change events

### Risk 4: Memory Leaks with Multiple Sources

**Likelihood:** Medium  
**Impact:** High

**Problem:** Complex reference counting with multiple sources could leak.

**Mitigation:**
- Careful AddRef/Release in SourceManager
- Use smart pointers where possible
- Memory leak detection in tests
- Code review focused on lifetime management

**Testing:**
- Valgrind/ASAN on Linux
- Windows memory profiler
- Register/unregister sources 1000 times
- Check memory after each iteration

### Risk 5: Thread Deadlocks

**Likelihood:** Low  
**Impact:** High

**Problem:** Multiple sources with multiple threads could deadlock.

**Mitigation:**
- Minimize lock scope in SourceManager
- Never call Stop() while holding mutex
- Use lock hierarchies (consistent order)
- Thread sanitizer in debug builds

**Testing:**
- Stress test: start/stop rapidly
- Concurrent registration from multiple threads
- Thread sanitizer enabled
- Deadlock detection timeout in tests

---

## Success Criteria

### Functional Requirements

- ✅ Can record microphone audio
- ✅ Can record desktop + microphone simultaneously
- ✅ Can enumerate audio devices
- ✅ Can select specific microphone
- ✅ ApplicationAudioSource works on Windows 11 22H2+
- ✅ ApplicationAudioSource fails gracefully on Windows 10
- ✅ SourceManager coordinates multiple sources
- ✅ All sources can start/stop independently
- ✅ Backward compatibility maintained (existing code works)

### Performance Requirements

- ✅ CPU usage with 2 audio sources ≤ baseline + 5%
- ✅ CPU usage with 3 audio sources ≤ baseline + 10%
- ✅ Memory usage ≤ baseline + 10MB
- ✅ Zero frame drops
- ✅ Audio/video sync <10ms
- ✅ Device enumeration <100ms

### Code Quality Requirements

- ✅ All new code compiles without warnings
- ✅ Follows existing code style
- ✅ Proper error handling
- ✅ No memory leaks
- ✅ Thread-safe where needed
- ✅ Clean abstractions

### Testing Requirements

- ✅ All Phase 1 tests still pass
- ✅ New unit tests for all new classes
- ✅ Integration tests for multi-source scenarios
- ✅ Performance tests show acceptable overhead
- ✅ Regression tests pass

### Documentation Requirements

- ✅ Internal README updated
- ✅ Code comments on non-obvious logic
- ✅ Usage examples for new APIs
- ✅ Migration guide from Phase 1 patterns

---

## Post-Phase 2 State

### What We Have After Phase 2

1. **Three Audio Source Types:**
   - DesktopAudioSource (from Phase 1)
   - MicrophoneAudioSource (new)
   - ApplicationAudioSource (new, Windows 11+)

2. **Source Discovery:**
   - Audio device enumeration
   - Application audio session enumeration
   - Default device selection

3. **Source Management:**
   - SourceManager for coordination
   - Thread-safe registration/unregistration
   - Unified start/stop

4. **Updated ScreenRecorder:**
   - Uses source abstraction
   - Supports multiple audio sources
   - Backward compatible

5. **C# Integration:**
   - Device enumeration exposed
   - Multi-source recording support
   - P/Invoke wrappers complete

### What's Still Missing (Phase 3+)

- Audio mixing (sources writing to same track conflict)
- Multi-track recording (sources to separate tracks)
- Per-source volume control
- Audio routing configuration
- Real-time audio level monitoring
- UI for source management

### Current Limitations

**Phase 2 Limitation:** Multiple audio sources can be captured, but:
- They write to separate callbacks (no mixing yet)
- Cannot write to same MP4 track without conflicts
- Phase 3 will add AudioMixer to combine sources

**Workaround for Phase 2:**
- Desktop audio → Track 1 (via callback → MP4SinkWriter)
- Microphone → Captured but not written (waiting for Phase 3 mixer)
- Or: Write to temporary separate files, mix in post

---

## Appendix A: Windows Audio Session API Reference

### Key Interfaces

**IMMDeviceEnumerator:**
- Enumerate audio devices
- Get default device
- Register for device change notifications

**IAudioClient3 (Windows 11+):**
- Initialize audio capture for specific process
- Get mix format
- Start/stop capture

**IAudioSessionManager2:**
- Enumerate active audio sessions
- Get session for specific process
- Monitor session state changes

**IAudioSessionControl2:**
- Get process ID for session
- Get display name
- Get session state (active/inactive)

### Process Isolation Example

```cpp
// Get audio session manager
wil::com_ptr<IAudioSessionManager2> sessionManager;
hr = device->Activate(__uuidof(IAudioSessionManager2), 
                      CLSCTX_ALL, nullptr, 
                      sessionManager.put_void());

// Enumerate sessions
wil::com_ptr<IAudioSessionEnumerator> sessionEnum;
hr = sessionManager->GetSessionEnumerator(&sessionEnum);

int count;
sessionEnum->GetCount(&count);

for (int i = 0; i < count; i++)
{
    wil::com_ptr<IAudioSessionControl> sessionControl;
    sessionEnum->GetSession(i, &sessionControl);
    
    wil::com_ptr<IAudioSessionControl2> sessionControl2;
    sessionControl.query_to(&sessionControl2);
    
    DWORD processId;
    sessionControl2->GetProcessId(&processId);
    
    // If this is our target process, use this session
    if (processId == targetProcessId)
    {
        // Set up capture for this session
    }
}
```

---

## Appendix B: Device Enumeration Example

```cpp
bool AudioDeviceEnumerator::EnumerateCaptureDevices(std::vector<AudioDeviceInfo>& devices)
{
    // Initialize COM
    HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    if (FAILED(hr) && hr != RPC_E_CHANGED_MODE)
        return false;
    
    // Create device enumerator
    wil::com_ptr<IMMDeviceEnumerator> enumerator;
    hr = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL,
                          __uuidof(IMMDeviceEnumerator), enumerator.put_void());
    if (FAILED(hr))
        return false;
    
    // Get default capture device
    wil::com_ptr<IMMDevice> defaultDevice;
    enumerator->GetDefaultAudioEndpoint(eCapture, eConsole, &defaultDevice);
    
    LPWSTR defaultDeviceId = nullptr;
    if (defaultDevice)
    {
        defaultDevice->GetId(&defaultDeviceId);
    }
    
    // Enumerate all capture devices
    wil::com_ptr<IMMDeviceCollection> collection;
    hr = enumerator->EnumAudioEndpoints(eCapture, DEVICE_STATE_ACTIVE, &collection);
    if (FAILED(hr))
        return false;
    
    UINT count;
    collection->GetCount(&count);
    
    for (UINT i = 0; i < count; i++)
    {
        wil::com_ptr<IMMDevice> device;
        collection->Item(i, &device);
        
        AudioDeviceInfo info;
        
        // Get device ID
        LPWSTR deviceId;
        device->GetId(&deviceId);
        info.deviceId = deviceId;
        CoTaskMemFree(deviceId);
        
        // Get friendly name
        wil::com_ptr<IPropertyStore> props;
        device->OpenPropertyStore(STGM_READ, &props);
        
        PROPVARIANT varName;
        PropVariantInit(&varName);
        props->GetValue(PKEY_Device_FriendlyName, &varName);
        info.friendlyName = varName.pwszVal;
        PropVariantClear(&varName);
        
        // Check if default
        info.isDefault = (defaultDeviceId && 
                         info.deviceId == defaultDeviceId);
        info.isLoopback = false;
        
        devices.push_back(info);
    }
    
    if (defaultDeviceId)
    {
        CoTaskMemFree(defaultDeviceId);
    }
    
    return true;
}
```

---

## Appendix C: Testing Checklist

### Before Merge

- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] Performance tests show acceptable overhead
- [ ] Regression tests pass (Phase 1 functionality)
- [ ] Memory leak detection clean
- [ ] Thread sanitizer clean
- [ ] Code review complete
- [ ] Documentation updated
- [ ] Migration guide written

### Platform Testing

- [ ] Windows 10 21H2 - Basic functionality
- [ ] Windows 10 22H2 - Basic functionality
- [ ] Windows 11 21H2 - Basic functionality
- [ ] Windows 11 22H2 - Full functionality (app audio)
- [ ] Windows 11 23H2 - Full functionality

### Hardware Testing

- [ ] No microphone connected
- [ ] USB microphone
- [ ] Built-in microphone (laptop)
- [ ] Multiple microphones
- [ ] Microphone disconnected during recording
- [ ] No speakers connected
- [ ] Various audio devices

---

**Document Version:** 1.0  
**Last Updated:** 2025-12-18  
**Author:** GitHub Copilot (Phase 2 Planning Session)
