#pragma once
#include "IMP4SinkWriter.h"
#include "MediaFoundationLifecycleManager.h"
#include "StreamConfigurationBuilder.h"
#include "TextureProcessor.h"
#include "SampleBuilder.h"
#include <span>
#include <wil/com.h>
#include <memory>

// Forward declarations
struct IMFSinkWriter;

/// <summary>
/// Windows Media Foundation implementation of MP4 file writer.
/// Supports H.264 video and optional AAC audio streams with hardware acceleration.
/// 
/// Refactored to follow SOLID principles with single-responsibility components:
/// - MediaFoundationLifecycleManager: MF initialization/shutdown
/// - StreamConfigurationBuilder: Media type configuration
/// - TextureProcessor: D3D11 texture handling
/// - SampleBuilder: IMFSample creation
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
    long WriteAudioSample(std::span<const uint8_t> data, int64_t timestamp) override;
    void Finalize() override;

    // Reference counting for COM-style usage
    unsigned long AddRef();
    unsigned long Release();

private:
    volatile long m_ref = 1;
    
    // Core components (single-responsibility)
    MediaFoundationLifecycleManager m_mfLifecycle;
    StreamConfigurationBuilder m_configBuilder;
    std::unique_ptr<TextureProcessor> m_textureProcessor;
    SampleBuilder m_sampleBuilder;
    
    // Sink writer state
    wil::com_ptr<IMFSinkWriter> m_sinkWriter;
    unsigned long m_videoStreamIndex = 0;
    unsigned long m_audioStreamIndex = 0;
    bool m_hasAudioStream = false;
    bool m_hasBegunWriting = false;
    uint64_t m_frameIndex = 0;
    int64_t m_prevVideoTimestamp = 0;
    
    // Configuration
    StreamConfigurationBuilder::VideoConfig m_videoConfig;
    StreamConfigurationBuilder::AudioConfig m_audioConfig;
};
