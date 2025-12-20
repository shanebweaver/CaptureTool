#pragma once
#include "AudioCaptureDevice.h"
#include "MediaClock.h"

// Forward declaration
class MP4SinkWriter;

/// <summary>
/// Manages audio capture in a dedicated thread with synchronized timestamps.
/// Captures audio samples from WASAPI and writes them to an MP4 sink writer.
/// Uses accumulated timestamps to ensure proper audio playback speed.
/// 
/// MEDIACLOCK OWNERSHIP:
/// - This class owns the master MediaClock for the entire capture session
/// - The clock is created in Start() after the audio format is known
/// - The clock is advanced on every audio buffer read (audio is the law)
/// - The clock is destroyed in Stop() to reset state
/// - Other media sources should use GetMediaClock() for read-only access
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

    /// <summary>
    /// Get read-only access to the master MediaClock.
    /// 
    /// This clock provides the authoritative timeline for all media sources.
    /// Only valid while audio capture is running (between Start() and Stop()).
    /// Other media sources should use this for synchronization.
    /// </summary>
    /// <returns>Pointer to MediaClock, or nullptr if not running</returns>
    const MediaClock* GetMediaClock() const { return m_mediaClock.get(); }

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
    std::atomic<bool> m_wasDisabled{false};     // Tracks if audio was previously disabled for resync
    std::atomic<int> m_samplesToSkip{0};        // Number of samples to skip after re-enabling
    
    std::vector<BYTE> m_silentBuffer;           // Reusable buffer for silent audio samples
    
    LONGLONG m_startQpc = 0;                    // QPC value at recording start (for synchronization)
    LARGE_INTEGER m_qpcFrequency{};             // QPC frequency for time calculations
    LONGLONG m_nextAudioTimestamp = 0;          // Accumulated timestamp (prevents overlapping samples)
    
    std::unique_ptr<MediaClock> m_mediaClock;   // Master clock driven by audio samples
};
