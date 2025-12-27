#include "pch.h"
#include "WindowsMFMP4SinkWriter.h"
#include "MediaFoundationLifecycleManager.h"
#include "StreamConfigurationBuilder.h"
#include "SampleBuilder.h"
#include "TextureProcessor.h"
#include "TextureProcessorFactory.h"
#include "MediaTimeConstants.h"

WindowsMFMP4SinkWriter::WindowsMFMP4SinkWriter()
    : m_mfLifecycle(std::make_unique<MediaFoundationLifecycleManager>())
    , m_configBuilder(std::make_unique<StreamConfigurationBuilder>())
    , m_sampleBuilder(std::make_unique<SampleBuilder>())
    , m_textureProcessorFactory(std::make_unique<TextureProcessorFactory>())
{
}

WindowsMFMP4SinkWriter::WindowsMFMP4SinkWriter(
    std::unique_ptr<IMediaFoundationLifecycleManager> lifecycleManager,
    std::unique_ptr<IStreamConfigurationBuilder> configBuilder,
    std::unique_ptr<ISampleBuilder> sampleBuilder)
    : m_mfLifecycle(std::move(lifecycleManager))
    , m_configBuilder(std::move(configBuilder))
    , m_sampleBuilder(std::move(sampleBuilder))
    , m_textureProcessorFactory(std::make_unique<TextureProcessorFactory>())
{
}

WindowsMFMP4SinkWriter::WindowsMFMP4SinkWriter(
    std::unique_ptr<IMediaFoundationLifecycleManager> lifecycleManager,
    std::unique_ptr<IStreamConfigurationBuilder> configBuilder,
    std::unique_ptr<ISampleBuilder> sampleBuilder,
    std::unique_ptr<ITextureProcessorFactory> textureProcessorFactory)
    : m_mfLifecycle(std::move(lifecycleManager))
    , m_configBuilder(std::move(configBuilder))
    , m_sampleBuilder(std::move(sampleBuilder))
    , m_textureProcessorFactory(std::move(textureProcessorFactory))
{
}

WindowsMFMP4SinkWriter::~WindowsMFMP4SinkWriter()
{
    Finalize();
}

bool WindowsMFMP4SinkWriter::Initialize(const wchar_t* outputPath, ID3D11Device* device, uint32_t width, uint32_t height, long* outHr)
{
    if (outHr) *outHr = S_OK;
    
    // Check if MF was successfully initialized
    if (!m_mfLifecycle->IsInitialized())
    {
        if (outHr) *outHr = m_mfLifecycle->GetInitializationResult();
        return false;
    }
    
    // Store video configuration
    m_videoConfig = IStreamConfigurationBuilder::VideoConfig::Default(width, height);
    
    // Create texture processor for video frame handling using factory
    wil::com_ptr<ID3D11DeviceContext> context;
    device->GetImmediateContext(context.put());
    m_textureProcessor = m_textureProcessorFactory->CreateTextureProcessor(device, context.get(), width, height);
    
    m_frameIndex = 0;

    // Create attributes to enable hardware acceleration and improve performance
    wil::com_ptr<IMFAttributes> attributes;
    HRESULT hr = MFCreateAttributes(attributes.put(), 3);
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }
    
    // Enable hardware transforms (GPU encoding) for better performance
    attributes->SetUINT32(MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, TRUE);
    
    // Enable format converters to allow automatic format conversion when needed
    attributes->SetUINT32(MF_READWRITE_DISABLE_CONVERTERS, FALSE);
    
    // Set low latency mode to reduce encoder queue buildup
    attributes->SetUINT32(MF_LOW_LATENCY, TRUE);

    wil::com_ptr<IMFSinkWriter> sinkWriter;
    hr = MFCreateSinkWriterFromURL(outputPath, nullptr, attributes.get(), sinkWriter.put());
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    // Create video output media type using builder
    auto outputTypeResult = m_configBuilder->CreateVideoOutputType(m_videoConfig);
    if (outputTypeResult.IsError())
    {
        if (outHr) *outHr = outputTypeResult.Error().hr;
        return false;
    }
    
    DWORD streamIndex = 0;
    hr = sinkWriter->AddStream(outputTypeResult.Value().get(), &streamIndex);
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    // Create video input media type using builder
    auto inputTypeResult = m_configBuilder->CreateVideoInputType(m_videoConfig);
    if (inputTypeResult.IsError())
    {
        if (outHr) *outHr = inputTypeResult.Error().hr;
        return false;
    }

    hr = sinkWriter->SetInputMediaType(streamIndex, inputTypeResult.Value().get(), nullptr);
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    m_sinkWriter = std::move(sinkWriter);
    m_videoStreamIndex = streamIndex;

    return true;
}

bool WindowsMFMP4SinkWriter::InitializeAudioStream(WAVEFORMATEX* audioFormat, long* outHr)
{
    if (!m_sinkWriter || !audioFormat)
    {
        if (outHr) *outHr = E_INVALIDARG;
        return false;
    }

    if (m_hasAudioStream)
    {
        if (outHr) *outHr = E_NOT_VALID_STATE;
        return false;
    }
    
    // Create audio configuration from wave format
    m_audioConfig = IStreamConfigurationBuilder::AudioConfig::FromWaveFormat(*audioFormat);

    // Create audio output media type using builder
    auto outputTypeResult = m_configBuilder->CreateAudioOutputType(m_audioConfig);
    if (outputTypeResult.IsError())
    {
        if (outHr) *outHr = outputTypeResult.Error().hr;
        return false;
    }

    DWORD audioStreamIndex = 0;
    HRESULT hr = m_sinkWriter->AddStream(outputTypeResult.Value().get(), &audioStreamIndex);
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    // Create audio input media type using builder
    auto inputTypeResult = m_configBuilder->CreateAudioInputType(m_audioConfig);
    if (inputTypeResult.IsError())
    {
        if (outHr) *outHr = inputTypeResult.Error().hr;
        return false;
    }

    hr = m_sinkWriter->SetInputMediaType(audioStreamIndex, inputTypeResult.Value().get(), nullptr);
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    m_audioStreamIndex = audioStreamIndex;
    m_hasAudioStream = true;

    hr = m_sinkWriter->BeginWriting();
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }
    m_hasBegunWriting = true;

    if (outHr) *outHr = S_OK;
    return true;
}

long WindowsMFMP4SinkWriter::WriteFrame(ID3D11Texture2D* texture, int64_t relativeTicks)
{
    if (!texture || !m_sinkWriter) return E_FAIL;

    // For video-only recording, begin writing on first frame
    if (!m_hasBegunWriting && !m_hasAudioStream)
    {
        HRESULT hr = m_sinkWriter->BeginWriting();
        if (FAILED(hr)) return hr;
        m_hasBegunWriting = true;
    }

    // Use texture processor to copy texture to buffer
    std::vector<uint8_t> frameBuffer;
    auto copyResult = m_textureProcessor->CopyTextureToBuffer(texture, frameBuffer);
    if (copyResult.IsError())
    {
        return copyResult.Error().hr;
    }

    // Calculate frame duration
    if (m_prevVideoTimestamp == 0)
        m_prevVideoTimestamp = relativeTicks;

    // Calculate frame duration using integer division
    // For standard frame rates (30, 60, etc.) that evenly divide TicksPerSecond(),
    // this provides exact values. For non-standard rates, this is an approximation.
    const LONGLONG frameDuration = MediaTimeConstants::TicksPerSecond() / m_videoConfig.frameRate;
    LONGLONG duration = relativeTicks - m_prevVideoTimestamp;
    if (duration <= 0) duration = frameDuration;
    m_prevVideoTimestamp = relativeTicks;

    // Use sample builder to create video sample
    auto sampleResult = m_sampleBuilder->CreateVideoSample(
        std::span<const uint8_t>(frameBuffer.data(), frameBuffer.size()),
        relativeTicks,
        duration);
    
    if (sampleResult.IsError())
    {
        return sampleResult.Error().hr;
    }

    m_frameIndex++;
    return m_sinkWriter->WriteSample(m_videoStreamIndex, sampleResult.Value().get());
}

long WindowsMFMP4SinkWriter::WriteAudioSample(std::span<const uint8_t> data, int64_t timestamp)
{
    if (!m_sinkWriter || !m_hasAudioStream || data.empty())
    {
        return E_FAIL;
    }

    // Validate audio format to prevent division by zero
    uint32_t blockAlign = (m_audioConfig.channels * m_audioConfig.bitsPerSample) / 8;
    if (blockAlign == 0 || m_audioConfig.sampleRate == 0)
    {
        return E_FAIL;
    }

    // Calculate number of frames from buffer size
    UINT32 numFrames = static_cast<UINT32>(data.size()) / blockAlign;
    LONGLONG duration = MediaTimeConstants::TicksFromAudioFrames(numFrames, m_audioConfig.sampleRate);
    
    // Use sample builder to create audio sample
    auto sampleResult = m_sampleBuilder->CreateAudioSample(data, timestamp, duration);
    if (sampleResult.IsError())
    {
        return sampleResult.Error().hr;
    }

    return m_sinkWriter->WriteSample(m_audioStreamIndex, sampleResult.Value().get());
}

void WindowsMFMP4SinkWriter::Finalize()
{
    if (m_sinkWriter)
    {
        if (m_hasBegunWriting)
        {
            // Flush encoder by sending stream ticks
            HRESULT hr = m_sinkWriter->SendStreamTick(m_videoStreamIndex, m_prevVideoTimestamp);
            
            if (m_hasAudioStream)
            {
                m_sinkWriter->SendStreamTick(m_audioStreamIndex, m_prevVideoTimestamp);
            }
        }
        
        HRESULT hr = m_sinkWriter->Finalize();
        m_sinkWriter.reset();
    }

    // TextureProcessor and other components clean up automatically via RAII
    m_textureProcessor.reset();
    
    // MediaFoundationLifecycleManager handles MFShutdown in its destructor
}

unsigned long WindowsMFMP4SinkWriter::AddRef()
{
    return InterlockedIncrement(&m_ref);
}

unsigned long WindowsMFMP4SinkWriter::Release()
{
    ULONG ref = InterlockedDecrement(&m_ref);
    if (ref == 0)
    {
        delete this;
    }
    return ref;
}
