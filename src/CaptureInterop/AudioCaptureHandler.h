#pragma once
#include "AudioCaptureDevice.h"
#include <thread>
#include <atomic>
#include <mutex>
#include <vector>

// Forward declaration
class MP4SinkWriter;

/// <summary>
/// Manages audio capture in a dedicated thread with synchronized timestamps.
/// Captures audio samples from WASAPI and writes them to an MP4 sink writer.
/// Uses accumulated timestamps to ensure proper audio playback speed.
/// </summary>
class AudioCaptureHandler
{
public:
    AudioCaptureHandler();
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
    /// Synchronizes start time with video capture if MP4SinkWriter is set.
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
    /// Set the MP4 sink writer to receive captured audio samples.
    /// Must be called before Start() to enable audio/video synchronization.
    /// </summary>
    /// <param name="sinkWriter">Pointer to the MP4SinkWriter instance.</param>
    void SetSinkWriter(MP4SinkWriter* sinkWriter) { m_sinkWriter = sinkWriter; }

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
    /// Uses accumulated timestamps to prevent audio speedup from overlapping samples.
    /// </summary>
    void CaptureThreadProc();
    
    AudioCaptureDevice m_device;
    MP4SinkWriter* m_sinkWriter = nullptr;
    
    std::thread m_captureThread;
    std::atomic<bool> m_isRunning{false};
    std::atomic<bool> m_isEnabled{true};        // Controls whether audio is written to output
    
    LONGLONG m_startQpc = 0;                    // QPC value at recording start (for synchronization)
    LARGE_INTEGER m_qpcFrequency{};             // QPC frequency for time calculations
    LONGLONG m_nextAudioTimestamp = 0;          // Accumulated timestamp (prevents overlapping samples)
};
