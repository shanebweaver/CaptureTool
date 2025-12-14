#pragma once

class MP4SinkWriter
{
public:
    MP4SinkWriter();
    ~MP4SinkWriter();

    // Initialize the sink writer
    bool Initialize(const wchar_t* outputPath, ID3D11Device* device, UINT32 width, UINT32 height, bool enableAudio = false, WAVEFORMATEX* audioFormat = nullptr, HRESULT* outHr = nullptr);

    // Write a frame to the MP4 file
    HRESULT WriteFrame(ID3D11Texture2D* texture, LONGLONG relativeTicks);

    // Write audio sample to the MP4 file
    HRESULT WriteAudioSample(BYTE* data, UINT32 dataSize, LONGLONG relativeTicks);

    // Finalize MP4 file
    void Finalize();

    ULONG STDMETHODCALLTYPE AddRef();
    ULONG STDMETHODCALLTYPE Release();

private:
    volatile long m_ref = 1;
    wil::com_ptr<IMFSinkWriter> m_sinkWriter;
    DWORD m_videoStreamIndex = 0;
    DWORD m_audioStreamIndex = 0;
    UINT64 m_frameIndex = 0;
    UINT32 m_width = 0;
    UINT32 m_height = 0;
    ID3D11Device* m_device = nullptr;
    ID3D11DeviceContext* m_context = nullptr;
    LONGLONG m_prevVideoTimestamp = 0;
    LONGLONG m_prevAudioTimestamp = 0;
    bool m_hasAudio = false;
    std::mutex m_writeMutex;
};
