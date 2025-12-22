#pragma once
#include "IMP4SinkWriter.h"
#include <wil/com.h>

// Forward declarations
struct IMFSinkWriter;

/// <summary>
/// Windows Media Foundation implementation of MP4 file writer.
/// Supports H.264 video and optional AAC audio streams with hardware acceleration.
/// </summary>
class WindowsMFMP4SinkWriter : public IMP4SinkWriter
{
public:
    WindowsMFMP4SinkWriter();
    ~WindowsMFMP4SinkWriter() override;

    // IMP4SinkWriter implementation
    bool Initialize(const wchar_t* outputPath, ID3D11Device* device, uint32_t width, uint32_t height, long* outHr = nullptr) override;
    bool InitializeAudioStream(WAVEFORMATEX* audioFormat, long* outHr = nullptr) override;
    long WriteFrame(ID3D11Texture2D* texture, int64_t relativeTicks) override;
    long WriteAudioSample(const uint8_t* pData, uint32_t numFrames, int64_t timestamp) override;
    void Finalize() override;

    // Reference counting for COM-style usage
    unsigned long AddRef();
    unsigned long Release();

private:
    volatile long m_ref = 1;
    wil::com_ptr<IMFSinkWriter> m_sinkWriter;
    unsigned long m_videoStreamIndex = 0;
    unsigned long m_audioStreamIndex = 0;
    bool m_hasAudioStream = false;
    bool m_hasBegunWriting = false;
    uint64_t m_frameIndex = 0;
    uint32_t m_width = 0;
    uint32_t m_height = 0;
    ID3D11Device* m_device = nullptr;
    ID3D11DeviceContext* m_context = nullptr;
    int64_t m_prevVideoTimestamp = 0;
    WAVEFORMATEX m_audioFormat = {};

    // Cached staging texture to prevent memory leak from repeated allocations
    wil::com_ptr<ID3D11Texture2D> m_stagingTexture;
};
