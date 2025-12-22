#pragma once
#include <cstdint>
#include <mmreg.h>

// Forward declarations
struct ID3D11Device;
struct ID3D11Texture2D;

/// <summary>
/// Interface for MP4 file writing with H.264 video and optional AAC audio.
/// Provides abstraction for sink writer implementations to enable dependency injection and testing.
/// </summary>
class IMP4SinkWriter
{
public:
    virtual ~IMP4SinkWriter() = default;

    /// <summary>
    /// Initialize the MP4 sink writer for video recording.
    /// </summary>
    /// <param name="outputPath">Path to the output MP4 file.</param>
    /// <param name="device">D3D11 device for video texture access.</param>
    /// <param name="width">Video frame width in pixels.</param>
    /// <param name="height">Video frame height in pixels.</param>
    /// <param name="outHr">Optional pointer to receive the HRESULT error code.</param>
    /// <returns>True if initialization succeeded, false otherwise.</returns>
    virtual bool Initialize(const wchar_t* outputPath, ID3D11Device* device, uint32_t width, uint32_t height, long* outHr = nullptr) = 0;

    /// <summary>
    /// Initialize the audio stream for the MP4 file.
    /// Must be called after Initialize() but before any WriteFrame() or WriteAudioSample() calls.
    /// Automatically starts writing once audio stream is configured.
    /// </summary>
    /// <param name="audioFormat">Audio format from WASAPI (PCM or Float).</param>
    /// <param name="outHr">Optional pointer to receive the HRESULT error code.</param>
    /// <returns>True if audio stream was added successfully, false otherwise.</returns>
    virtual bool InitializeAudioStream(WAVEFORMATEX* audioFormat, long* outHr = nullptr) = 0;

    /// <summary>
    /// Write a video frame to the MP4 file.
    /// </summary>
    /// <param name="texture">D3D11 texture containing the video frame.</param>
    /// <param name="relativeTicks">Timestamp in 100-nanosecond units (from media clock).</param>
    /// <returns>S_OK on success, or error HRESULT.</returns>
    virtual long WriteFrame(ID3D11Texture2D* texture, int64_t relativeTicks) = 0;

    /// <summary>
    /// Write an audio sample to the MP4 file.
    /// Audio is automatically encoded to AAC format by Media Foundation.
    /// </summary>
    /// <param name="pData">Pointer to raw audio data (PCM or Float from WASAPI).</param>
    /// <param name="numFrames">Number of audio frames (one sample per channel).</param>
    /// <param name="timestamp">Timestamp in 100-nanosecond units (from media clock).</param>
    /// <returns>S_OK on success, or error HRESULT.</returns>
    virtual long WriteAudioSample(const uint8_t* pData, uint32_t numFrames, int64_t timestamp) = 0;

    /// <summary>
    /// Finalize and close the MP4 file.
    /// Must be called when recording is complete to properly close the file.
    /// </summary>
    virtual void Finalize() = 0;
};
