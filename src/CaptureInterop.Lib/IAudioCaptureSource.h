# pragma once
#include "IMediaClockAdvancer.h"
#include <functional>
#include <span>

/// <summary>
/// Event arguments for audio sample ready event.
/// Contains the audio sample data and timing information.
/// </summary>
struct AudioSampleReadyEventArgs
{
    std::span<const uint8_t> data;  // Audio sample data (size = numFrames * nBlockAlign)
    LONGLONG timestamp;             // Timestamp for this sample (100ns ticks)
    WAVEFORMATEX* pFormat;          // Audio format for this sample
};

/// <summary>
/// Callback function type for audio sample ready events.
/// </summary>
using AudioSampleReadyCallback = std::function<void(const AudioSampleReadyEventArgs&)>;

/// <summary>
/// Interface for audio input sources that can be captured and written to an output stream.
/// Implementations provide different audio sources (system audio, microphone, etc.)
/// Extends IMediaClockAdvancer to drive the media clock timeline based on audio samples.
/// 
/// Implements Rust Principles:
/// - Principle #7 (Const Correctness): Read-only methods are const (GetFormat, IsEnabled, IsRunning)
/// - Principle #8 (Thread Safety by Design): Implementations use threading primitives to ensure
///   safe concurrent access from capture thread and control thread
/// 
/// Design notes:
/// - Audio capture sources are the authoritative time source for A/V synchronization
/// - Implementations drive the media clock by calling IMediaClockWriter::AdvanceByAudioSamples()
/// - SetAudioSampleReadyCallback() is invoked on the capture thread, not the calling thread
/// 
/// See docs/RUST_PRINCIPLES.md for more details on these principles.
/// </summary>
class IAudioCaptureSource : public IMediaClockAdvancer
{
public:
    virtual ~IAudioCaptureSource() = default;

    /// <summary>
    /// Initialize the audio input source.
    /// </summary>
    /// <param name="outHr">Optional pointer to receive the HRESULT error code.</param>
    /// <returns>True if initialization succeeded, false otherwise.</returns>
    virtual bool Initialize(HRESULT* outHr = nullptr) = 0;

    /// <summary>
    /// Start capturing audio from the input source.
    /// </summary>
    /// <param name="outHr">Optional pointer to receive the HRESULT error code.</param>
    /// <returns>True if capture started successfully, false otherwise.</returns>
    virtual bool Start(HRESULT* outHr = nullptr) = 0;

    /// <summary>
    /// Stop capturing audio from the input source.
    /// </summary>
    virtual void Stop() = 0;

    /// <summary>
    /// Get the audio format of the input source.
    /// </summary>
    /// <returns>Pointer to WAVEFORMATEX structure, or nullptr if not initialized.</returns>
    virtual WAVEFORMATEX* GetFormat() const = 0;

    /// <summary>
    /// Set the callback to be invoked when an audio sample is ready.
    /// The callback is invoked on the audio capture thread.
    /// </summary>
    /// <param name="callback">Callback function to receive audio samples.</param>
    virtual void SetAudioSampleReadyCallback(AudioSampleReadyCallback callback) = 0;

    /// <summary>
    /// Enable or disable audio writing to the output.
    /// When disabled, audio samples may still be captured but not written.
    /// </summary>
    /// <param name="enabled">True to enable audio writing, false to mute.</param>
    virtual void SetEnabled(bool enabled) = 0;

    /// <summary>
    /// Check if audio writing is enabled.
    /// </summary>
    /// <returns>True if enabled, false if muted.</returns>
    virtual bool IsEnabled() const = 0;

    /// <summary>
    /// Check if the audio input source is currently running.
    /// </summary>
    /// <returns>True if capture is active, false otherwise.</returns>
    virtual bool IsRunning() const = 0;
};
