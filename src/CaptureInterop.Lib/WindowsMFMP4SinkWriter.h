#pragma once
#include "IMP4SinkWriter.h"
#include "IMediaFoundationLifecycleManager.h"
#include "IStreamConfigurationBuilder.h"
#include "ITextureProcessor.h"
#include "ITextureProcessorFactory.h"
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
/// - ITextureProcessor: D3D11 texture handling (created during Initialize)
/// - ISampleBuilder: IMFSample creation
/// 
/// Note: TextureProcessor is created during Initialize() rather than via constructor
/// injection because it requires runtime parameters (D3D11 device, context, dimensions)
/// that are not available at construction time.
/// </summary>
class WindowsMFMP4SinkWriter : public IMP4SinkWriter
{
public:
    WindowsMFMP4SinkWriter();
    
    /// <summary>
    /// Constructor with dependency injection for testability.
    /// Note: TextureProcessor is not injected here as it requires runtime parameters
    /// (D3D11 device, context, dimensions) provided during Initialize().
    /// To inject TextureProcessor creation logic, use the overload that accepts
    /// ITextureProcessorFactory.
    /// </summary>
    /// <param name="lifecycleManager">Media Foundation lifecycle manager. Must not be null.</param>
    /// <param name="configBuilder">Stream configuration builder. Must not be null.</param>
    /// <param name="sampleBuilder">Sample builder. Must not be null.</param>
    /// <exception cref="std::invalid_argument">Thrown if any parameter is null.</exception>
    WindowsMFMP4SinkWriter(
        std::unique_ptr<IMediaFoundationLifecycleManager> lifecycleManager,
        std::unique_ptr<IStreamConfigurationBuilder> configBuilder,
        std::unique_ptr<ISampleBuilder> sampleBuilder);
    
    /// <summary>
    /// Constructor with full dependency injection including TextureProcessor factory.
    /// This allows complete control over TextureProcessor creation for advanced testing scenarios.
    /// </summary>
    /// <param name="lifecycleManager">Media Foundation lifecycle manager. Must not be null.</param>
    /// <param name="configBuilder">Stream configuration builder. Must not be null.</param>
    /// <param name="sampleBuilder">Sample builder. Must not be null.</param>
    /// <param name="textureProcessorFactory">Texture processor factory. Must not be null.</param>
    /// <exception cref="std::invalid_argument">Thrown if any parameter is null.</exception>
    WindowsMFMP4SinkWriter(
        std::unique_ptr<IMediaFoundationLifecycleManager> lifecycleManager,
        std::unique_ptr<IStreamConfigurationBuilder> configBuilder,
        std::unique_ptr<ISampleBuilder> sampleBuilder,
        std::unique_ptr<ITextureProcessorFactory> textureProcessorFactory);
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
    std::unique_ptr<ITextureProcessorFactory> m_textureProcessorFactory;  // Optional factory for TextureProcessor creation
    
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
