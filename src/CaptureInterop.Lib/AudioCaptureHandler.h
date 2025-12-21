#pragma once
#include "AudioCaptureDevice.h"

// Forward declarations
class MP4SinkWriter;
class IMediaClockWriter;
class IMediaClockReader;

/// <summary>
/// Manages audio capture in a dedicated thread with synchronized timestamps.
/// Captures audio samples from WASAPI and writes them to an MP4 sink writer.
/// Uses the media clock reader to get synchronized timestamps.
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
    
    AudioCaptureDevice m_device;
    MP4SinkWriter* m_sinkWriter = nullptr;
    IMediaClockWriter* m_clockWriter = nullptr;
    IMediaClockReader* m_clockReader = nullptr;
    
    std::thread m_captureThread;
    std::atomic<bool> m_isRunning{false};
    std::atomic<bool> m_isEnabled{true};        // Controls whether audio is written to output
    std::atomic<bool> m_wasDisabled{false};     // Tracks if audio was previously disabled for resync
    std::atomic<int> m_samplesToSkip{0};        // Number of samples to skip after re-enabling
    
    std::vector<BYTE> m_silentBuffer;           // Reusable buffer for silent audio samples
    
    LARGE_INTEGER m_qpcFrequency{};             // QPC frequency for time calculations
};
