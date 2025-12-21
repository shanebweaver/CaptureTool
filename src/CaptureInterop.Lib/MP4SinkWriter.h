#pragma once

/// <summary>
/// MP4 file writer supporting H.264 video and optional AAC audio streams.
/// Handles video encoding and audio encoding via Media Foundation.
/// Supports synchronized audio/video recording with common time base.
/// </summary>
class MP4SinkWriter
{
public:
    MP4SinkWriter();
    ~MP4SinkWriter();

    /// <summary>
    /// Initialize the MP4 sink writer for video recording.
    /// </summary>
    /// <param name="outputPath">Path to the output MP4 file.</param>
    /// <param name="device">D3D11 device for video texture access.</param>
    /// <param name="width">Video frame width in pixels.</param>
    /// <param name="height">Video frame height in pixels.</param>
    /// <param name="outHr">Optional pointer to receive the HRESULT error code.</param>
    /// <returns>True if initialization succeeded, false otherwise.</returns>
    bool Initialize(const wchar_t* outputPath, ID3D11Device* device, UINT32 width, UINT32 height, HRESULT* outHr = nullptr);

    /// <summary>
    /// Initialize the audio stream for the MP4 file.
    /// Must be called after Initialize() but before any WriteFrame() or WriteAudioSample() calls.
    /// Automatically starts writing once audio stream is configured.
    /// </summary>
    /// <param name="audioFormat">Audio format from WASAPI (PCM or Float).</param>
    /// <param name="outHr">Optional pointer to receive the HRESULT error code.</param>
    /// <returns>True if audio stream was added successfully, false otherwise.</returns>
    bool InitializeAudioStream(WAVEFORMATEX* audioFormat, HRESULT* outHr = nullptr);

    /// <summary>
    /// Write a video frame to the MP4 file.
    /// </summary>
    /// <param name="texture">D3D11 texture containing the video frame.</param>
    /// <param name="relativeTicks">Timestamp in 100-nanosecond units (from media clock).</param>
    /// <returns>S_OK on success, or error HRESULT.</returns>
    HRESULT WriteFrame(ID3D11Texture2D* texture, LONGLONG relativeTicks);

    /// <summary>
    /// Write an audio sample to the MP4 file.
    /// Audio is automatically encoded to AAC format by Media Foundation.
    /// </summary>
    /// <param name="pData">Pointer to raw audio data (PCM or Float from WASAPI).</param>
    /// <param name="numFrames">Number of audio frames (one sample per channel).</param>
    /// <param name="timestamp">Timestamp in 100-nanosecond units (from media clock).</param>
    /// <returns>S_OK on success, or error HRESULT.</returns>
    HRESULT WriteAudioSample(const BYTE* pData, UINT32 numFrames, LONGLONG timestamp);

    /// <summary>
    /// Finalize and close the MP4 file.
    /// Must be called when recording is complete to properly close the file.
    /// </summary>
    void Finalize();

    ULONG STDMETHODCALLTYPE AddRef();
    ULONG STDMETHODCALLTYPE Release();

private:
    volatile long m_ref = 1;
    wil::com_ptr<IMFSinkWriter> m_sinkWriter;
    DWORD m_videoStreamIndex = 0;
    DWORD m_audioStreamIndex = 0;
    bool m_hasAudioStream = false;
    bool m_hasBegunWriting = false;
    UINT64 m_frameIndex = 0;
    UINT32 m_width = 0;
    UINT32 m_height = 0;
    ID3D11Device* m_device = nullptr;
    ID3D11DeviceContext* m_context = nullptr;
    LONGLONG m_prevVideoTimestamp = 0;
    WAVEFORMATEX m_audioFormat = {};            // Cached audio format info
};
