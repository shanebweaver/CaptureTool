#pragma once
#include "IMP4SinkWriter.h"
#include "IMediaFoundationLifecycleManager.h"
#include "IStreamConfigurationBuilder.h"
#include "ITextureProcessor.h"
#include "ISampleBuilder.h"
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
/// - IMediaFoundationLifecycleManager: MF initialization/shutdown
/// - IStreamConfigurationBuilder: Media type configuration
/// - ITextureProcessor: D3D11 texture handling
/// - ISampleBuilder: IMFSample creation
/// </summary>
class WindowsMFMP4SinkWriter : public IMP4SinkWriter
{
public:
    WindowsMFMP4SinkWriter();
    WindowsMFMP4SinkWriter(
        std::unique_ptr<IMediaFoundationLifecycleManager> lifecycleManager,
        std::unique_ptr<IStreamConfigurationBuilder> configBuilder,
        std::unique_ptr<ISampleBuilder> sampleBuilder);
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
    std::unique_ptr<IMediaFoundationLifecycleManager> m_mfLifecycle;
    std::unique_ptr<IStreamConfigurationBuilder> m_configBuilder;
    std::unique_ptr<ITextureProcessor> m_textureProcessor;
    std::unique_ptr<ISampleBuilder> m_sampleBuilder;
    
    // Sink writer state
    wil::com_ptr<IMFSinkWriter> m_sinkWriter;
    unsigned long m_videoStreamIndex = 0;
    unsigned long m_audioStreamIndex = 0;
    bool m_hasAudioStream = false;
    bool m_hasBegunWriting = false;
    uint64_t m_frameIndex = 0;
    int64_t m_prevVideoTimestamp = 0;
    
    // Configuration
    IStreamConfigurationBuilder::VideoConfig m_videoConfig;
    IStreamConfigurationBuilder::AudioConfig m_audioConfig;
};
