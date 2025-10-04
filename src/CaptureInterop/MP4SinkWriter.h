#pragma once

class MP4SinkWriter
{
public:
    MP4SinkWriter();
    ~MP4SinkWriter();

    // Initialize the sink writer
    bool Initialize(const wchar_t* outputPath, ID3D11Device* device, UINT32 width, UINT32 height, HRESULT* outHr = nullptr);

    // Write a frame to the MP4 file
    HRESULT WriteFrame(ID3D11Texture2D* texture, LONGLONG relativeTicks);

    // Finalize MP4 file
    void Finalize();

    ULONG STDMETHODCALLTYPE AddRef();
    ULONG STDMETHODCALLTYPE Release();

private:
    volatile long m_ref = 1;
    wil::com_ptr<IMFSinkWriter> m_sinkWriter;
    DWORD m_streamIndex = 0;
    UINT64 m_frameIndex = 0;
    UINT32 m_width = 0;
    UINT32 m_height = 0;
    ID3D11Device* m_device = nullptr;
    ID3D11DeviceContext* m_context = nullptr;
    LONGLONG m_prevTimestamp = 0;
};
