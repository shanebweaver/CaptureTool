#pragma once
#include "AudioCaptureDevice.h"
#include <functional>
#include <thread>
#include <atomic>
#include <vector>
#include <mmreg.h>
#include <Windows.h>
#include <mutex>

// Forward declarations
class IMediaClockWriter;
class IMediaClockReader;

/// <summary>
/// Event arguments for audio sample ready event.
/// Contains the audio sample data and timing information.
/// </summary>
struct AudioSampleReadyEventArgs;

/// <summary>
/// Callback function type for audio sample ready events.
/// </summary>
using AudioSampleReadyCallback = std::function<void(const AudioSampleReadyEventArgs&)>;

/// <summary>
/// Manages audio capture in a dedicated thread with synchronized timestamps.
/// Captures audio samples from WASAPI and fires an event when samples are ready.
/// Uses the media clock reader to get synchronized timestamps.
/// 
/// Implements Rust Principles:
/// - Principle #5 (RAII Everything): Destructor calls Stop() to ensure thread is joined
///   and audio device is released. AudioCaptureDevice cleanup is automatic.
/// - Principle #6 (No Globals): All dependencies (clock reader/writer) are passed via
///   constructor or setters, not accessed as singletons.
/// - Principle #8 (Thread Safety by Design): Uses std::atomic for flags, std::mutex for
///   buffer protection. Designed for concurrent access from control and capture threads.
/// 
/// Threading model:
/// - Audio capture runs in dedicated thread at ABOVE_NORMAL priority
/// - Control methods (Start, Stop, SetEnabled) are thread-safe via atomics
/// - Callback is invoked on the capture thread
/// - Silent buffer access is protected by mutex
/// 
/// Design notes:
/// - Advances media clock as audio samples are captured (authoritative time source)
/// - Generates silent frames when system audio is silent to maintain timing
/// - SetEnabled() allows runtime muting without stopping capture thread
/// 
/// See docs/RUST_PRINCIPLES.md for more details.
/// </summary>
class AudioCaptureHandler
{
public:
    AudioCaptureHandler(IMediaClockReader* clockReader);
    ~AudioCaptureHandler();

    /// <summary>
    /// Initialize the audio capture device.
    /// </summary>
    /// <param name="loopback">True for system audio loopback, false for microphone.</param>
    /// <param name="outHr">Optional pointer to receive the HRESULT error code.</param>
    /// <returns>True if initialization succeeded, false otherwise.</returns>
    bool Initialize(bool loopback, HRESULT* outHr = nullptr);
    
    /// <summary>
    /// Start the audio capture thread.
    /// </summary>
    /// <param name="outHr">Optional pointer to receive the HRESULT error code.</param>
    /// <returns>True if capture started successfully, false otherwise.</returns>
    bool Start(HRESULT* outHr = nullptr);
    
    /// <summary>
    /// Stop the audio capture thread and wait for it to complete.
    /// Safe to call multiple times.
    /// </summary>
    void Stop();
    
    /// <summary>
    /// Get the audio format of the capture device.
    /// </summary>
    /// <returns>Pointer to WAVEFORMATEX structure, or nullptr if not initialized.</returns>
    WAVEFORMATEX* GetFormat() const;

    /// <summary>
    /// Set the callback to be invoked when an audio sample is ready.
    /// The callback is invoked on the audio capture thread.
    /// </summary>
    /// <param name="callback">Callback function to receive audio samples.</param>
    void SetAudioSampleReadyCallback(AudioSampleReadyCallback callback) { m_audioSampleReadyCallback = callback; }

    /// <summary>
    /// Set the media clock writer to advance as audio samples are captured.
    /// The handler advances the clock with each audio sample to maintain
    /// accurate timeline synchronization for A/V sync.
    /// Must be called before Start() to enable clock advancement.
    /// </summary>
    /// <param name="clockWriter">Pointer to the IMediaClockWriter instance.</param>
    void SetClockWriter(IMediaClockWriter* clockWriter) { m_clockWriter = clockWriter; }

    /// <summary>
    /// Enable or disable audio capture writing.
    /// When disabled, audio samples are still captured but not written to the output.
    /// </summary>
    /// <param name="enabled">True to enable audio writing, false to mute.</param>
    void SetEnabled(bool enabled) { m_isEnabled = enabled; }

    /// <summary>
    /// Check if audio capture writing is enabled.
    /// </summary>
    /// <returns>True if enabled, false if muted.</returns>
    bool IsEnabled() const { return m_isEnabled; }

    /// <summary>
    /// Check if audio capture is currently running.
    /// </summary>
    /// <returns>True if capture thread is active, false otherwise.</returns>
    bool IsRunning() const { return m_isRunning; }

private:
    /// <summary>
    /// Audio capture thread procedure.
    /// Runs at ABOVE_NORMAL priority to ensure responsive capture without starving UI.
    /// Uses the media clock reader to get synchronized timestamps.
    /// </summary>
    void CaptureThreadProc();
    
    /// <summary>
    /// Thread-safe helper to get silent audio buffer of required size. 
    /// Returns pointer to zeroed buffer that's valid until next call.
    /// </summary>
    BYTE* GetSilentBuffer(UINT32 requiredSize);
    
    AudioCaptureDevice m_device;
    AudioSampleReadyCallback m_audioSampleReadyCallback;
    IMediaClockWriter* m_clockWriter = nullptr;
    IMediaClockReader* m_clockReader = nullptr;
    
    std::thread m_captureThread;
    std::atomic<bool> m_isRunning{false};
    std::atomic<bool> m_isEnabled{true};        // Controls whether audio is written to output
    std::atomic<bool> m_wasDisabled{false};     // Tracks if audio was previously disabled for resync
    std::atomic<int> m_samplesToSkip{0};        // Number of samples to skip after re-enabling
    
    std::vector<BYTE> m_silentBuffer;           // Reusable buffer for silent audio samples
    std::mutex m_silentBufferMutex;             // Protects m_silentBuffer access
    
    UINT32 m_sampleRate = 0;                    // Cached sample rate from audio format
    LARGE_INTEGER m_qpcFrequency{};             // QPC frequency for time calculations
};
