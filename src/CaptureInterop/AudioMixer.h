#pragma once
#include "IAudioSource.h"
#include <vector>
#include <mutex>
#include <mmreg.h>
#include <mfapi.h>
#include <mftransform.h>

/// <summary>
/// Audio source with associated mixing configuration.
/// </summary>
struct AudioSourceEntry
{
    IAudioSource* source;          // Weak reference to audio source (not owned)
    float volume;                  // Volume multiplier (0.0 - 2.0, default 1.0)
    bool muted;                    // If true, source is muted (overrides volume)
    WAVEFORMATEX format;          // Cached format of this source
    uint64_t sourceId;            // Unique identifier for this source
};

/// <summary>
/// Multi-source audio mixer with real-time mixing, volume control, and sample rate conversion.
/// Combines multiple audio sources into a single output stream with format normalization.
/// Thread-safe for registration/configuration, but mixing is single-threaded.
/// </summary>
class AudioMixer
{
public:
    AudioMixer();
    ~AudioMixer();

    /// <summary>
    /// Initialize the mixer with target output format.
    /// All input sources will be converted to this format before mixing.
    /// </summary>
    /// <param name="sampleRate">Target sample rate (e.g., 48000)</param>
    /// <param name="channels">Target channel count (1=mono, 2=stereo)</param>
    /// <param name="bitsPerSample">Target bits per sample (16 or 32)</param>
    /// <returns>True if initialization succeeded</returns>
    bool Initialize(UINT32 sampleRate, UINT16 channels, UINT16 bitsPerSample);

    /// <summary>
    /// Register an audio source with the mixer.
    /// The source must remain valid until UnregisterSource is called.
    /// </summary>
    /// <param name="source">Audio source to register (weak reference)</param>
    /// <param name="volume">Initial volume (0.0-2.0, default 1.0)</param>
    /// <returns>Source ID for future operations, or 0 on failure</returns>
    uint64_t RegisterSource(IAudioSource* source, float volume = 1.0f);

    /// <summary>
    /// Unregister an audio source from the mixer.
    /// </summary>
    /// <param name="sourceId">Source ID returned from RegisterSource</param>
    void UnregisterSource(uint64_t sourceId);

    /// <summary>
    /// Set volume for a registered source.
    /// </summary>
    /// <param name="sourceId">Source ID</param>
    /// <param name="volume">Volume multiplier (0.0-2.0, clamped)</param>
    void SetSourceVolume(uint64_t sourceId, float volume);

    /// <summary>
    /// Get volume for a registered source.
    /// </summary>
    /// <param name="sourceId">Source ID</param>
    /// <returns>Current volume, or 0.0 if source not found</returns>
    float GetSourceVolume(uint64_t sourceId) const;

    /// <summary>
    /// Mute or unmute a registered source.
    /// </summary>
    /// <param name="sourceId">Source ID</param>
    /// <param name="muted">True to mute, false to unmute</param>
    void SetSourceMuted(uint64_t sourceId, bool muted);

    /// <summary>
    /// Check if a registered source is muted.
    /// </summary>
    /// <param name="sourceId">Source ID</param>
    /// <returns>True if muted, false if not muted or source not found</returns>
    bool IsSourceMuted(uint64_t sourceId) const;

    /// <summary>
    /// Get the number of registered sources.
    /// </summary>
    /// <returns>Number of registered sources</returns>
    size_t GetSourceCount() const;

    /// <summary>
    /// Get the target output format.
    /// </summary>
    /// <returns>Pointer to output format, or nullptr if not initialized</returns>
    const WAVEFORMATEX* GetOutputFormat() const;

    /// <summary>
    /// Mix audio from all registered sources for a given timestamp range.
    /// This method should be called periodically to process audio data.
    /// Output buffer must be pre-allocated by caller.
    /// </summary>
    /// <param name="outputBuffer">Pre-allocated output buffer</param>
    /// <param name="outputFrames">Number of frames to generate</param>
    /// <param name="timestamp">Current timestamp in 100-nanosecond units</param>
    /// <returns>Number of frames actually mixed</returns>
    UINT32 MixAudio(BYTE* outputBuffer, UINT32 outputFrames, LONGLONG timestamp);

    /// <summary>
    /// Get audio from a specific source (without mixing with other sources).
    /// Used for separate track recording where each source goes to its own track.
    /// Output buffer must be pre-allocated by caller.
    /// </summary>
    /// <param name="sourceId">Source ID to get audio from</param>
    /// <param name="outputBuffer">Pre-allocated output buffer</param>
    /// <param name="outputFrames">Number of frames to generate</param>
    /// <param name="timestamp">Current timestamp in 100-nanosecond units</param>
    /// <returns>Number of frames actually processed</returns>
    UINT32 GetSourceAudio(uint64_t sourceId, BYTE* outputBuffer, UINT32 outputFrames, LONGLONG timestamp);

    /// <summary>
    /// Get list of all registered source IDs.
    /// </summary>
    /// <returns>Vector of source IDs</returns>
    std::vector<uint64_t> GetSourceIds() const;

    /// <summary>
    /// Get the mixer output format information.
    /// </summary>
    UINT32 GetOutputSampleRate() const { return m_outputFormat.nSamplesPerSec; }
    UINT16 GetOutputChannels() const { return m_outputFormat.nChannels; }
    UINT16 GetOutputBitsPerSample() const { return m_outputFormat.wBitsPerSample; }

private:
    // Helper methods
    AudioSourceEntry* FindSource(uint64_t sourceId);
    const AudioSourceEntry* FindSource(uint64_t sourceId) const;
    bool CreateResampler(const WAVEFORMATEX* inputFormat, const WAVEFORMATEX* outputFormat, IMFTransform** ppResampler);
    UINT32 ConvertAndMixSource(AudioSourceEntry& entry, BYTE* mixBuffer, UINT32 mixFrames, LONGLONG timestamp);
    void ApplyVolume(BYTE* buffer, UINT32 numFrames, float volume);
    void MixBuffers(BYTE* dest, const BYTE* src, UINT32 numFrames);

    // State
    bool m_initialized;
    WAVEFORMATEX m_outputFormat;
    std::vector<AudioSourceEntry> m_sources;
    uint64_t m_nextSourceId;
    mutable std::mutex m_mutex;

    // Mixing buffers
    std::vector<BYTE> m_tempBuffer;       // Temporary buffer for format conversion
    std::vector<BYTE> m_mixBuffer;        // Buffer for mixing audio
    std::vector<float> m_floatBuffer;     // Float buffer for high-quality mixing

    // Sample rate converters (one per source with different format)
    std::map<uint64_t, wil::com_ptr<IMFTransform>> m_resamplers;
};
