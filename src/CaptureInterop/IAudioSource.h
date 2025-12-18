#pragma once
#include "IMediaSource.h"
#include <functional>
#include <mmreg.h>

/// <summary>
/// Callback signature for audio sample delivery.
/// Called on capture thread when new audio data is available.
/// </summary>
/// <param name="data">Pointer to raw audio sample data.</param>
/// <param name="numFrames">Number of audio frames (one sample per channel).</param>
/// <param name="timestamp">Timestamp in 100-nanosecond units (accumulated, not relative).</param>
using AudioSampleCallback = std::function<void(const BYTE* data, UINT32 numFrames, LONGLONG timestamp)>;

/// <summary>
/// Interface for audio capture sources.
/// Extends IMediaSource with audio-specific functionality.
/// </summary>
class IAudioSource : public IMediaSource
{
public:
    /// <summary>
    /// Get the audio format of this source.
    /// </summary>
    /// <returns>Pointer to WAVEFORMATEX structure, or nullptr if not initialized.</returns>
    virtual WAVEFORMATEX* GetFormat() const = 0;
    
    /// <summary>
    /// Set the callback to receive captured audio samples.
    /// Must be set before Start() to receive audio data.
    /// </summary>
    /// <param name="callback">Function to call for each audio sample batch.</param>
    virtual void SetAudioCallback(AudioSampleCallback callback) = 0;
    
    /// <summary>
    /// Enable or disable audio output.
    /// When disabled, silent audio is sent instead of captured audio.
    /// </summary>
    /// <param name="enabled">True to enable audio output, false to mute.</param>
    virtual void SetEnabled(bool enabled) = 0;
    
    /// <summary>
    /// Check if audio output is enabled.
    /// </summary>
    /// <returns>True if enabled, false if muted.</returns>
    virtual bool IsEnabled() const = 0;
    
    /// <summary>
    /// Override to return Audio type.
    /// </summary>
    MediaSourceType GetSourceType() const override { return MediaSourceType::Audio; }
};
