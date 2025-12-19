#include "pch.h"
#include "AACEncoder.h"
#include <chrono>

#pragma comment(lib, "mfplat.lib")
#pragma comment(lib, "mfuuid.lib")

AACEncoder::AACEncoder()
    : m_refCount(1)
    , m_isInitialized(false)
    , m_isHardwareAccelerated(false)
    , m_pEncoder(nullptr)
    , m_pInputType(nullptr)
    , m_pOutputType(nullptr)
    , m_samplesPerFrame(1024)  // Standard AAC frame size
    , m_encodedSamples(0)
    , m_droppedSamples(0)
    , m_totalEncodingTime(0.0)
{
}

AACEncoder::~AACEncoder()
{
    Flush(nullptr);
}

ULONG AACEncoder::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

ULONG AACEncoder::Release()
{
    ULONG count = InterlockedDecrement(&m_refCount);
    if (count == 0)
    {
        delete this;
    }
    return count;
}

HRESULT AACEncoder::Configure(const AudioEncoderConfig& config)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (m_isInitialized)
    {
        return E_FAIL;
    }
    
    m_config = config;
    
    HRESULT hr = CreateEncoder();
    if (FAILED(hr))
    {
        return hr;
    }
    
    hr = ConfigureEncoder();
    if (FAILED(hr))
    {
        Flush(nullptr);
        return hr;
    }
    
    hr = m_pEncoder->ProcessMessage(MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, 0);
    if (FAILED(hr))
    {
        Flush(nullptr);
        return hr;
    }
    
    hr = m_pEncoder->ProcessMessage(MFT_MESSAGE_NOTIFY_START_OF_STREAM, 0);
    if (FAILED(hr))
    {
        Flush(nullptr);
        return hr;
    }
    
    m_isInitialized = true;
    return S_OK;
}

HRESULT AACEncoder::GetConfiguration(AudioEncoderConfig* pConfig) const
{
    if (!pConfig)
    {
        return E_POINTER;
    }
    
    std::lock_guard<std::mutex> lock(m_mutex);
    *pConfig = m_config;
    return S_OK;
}

HRESULT AACEncoder::CreateEncoder()
{
    // AAC encoder CLSID
    CLSID clsidEncoder = CLSID_AACMFTEncoder;
    
    HRESULT hr = CoCreateInstance(clsidEncoder, nullptr, CLSCTX_INPROC_SERVER,
        IID_IMFTransform, (void**)&m_pEncoder);
    
    if (FAILED(hr))
    {
        return hr;
    }
    
    // Check if hardware accelerated (most AAC encoders are software)
    m_isHardwareAccelerated = false;
    
    return S_OK;
}

HRESULT AACEncoder::ConfigureEncoder()
{
    HRESULT hr = CreateInputMediaType();
    if (FAILED(hr))
    {
        return hr;
    }
    
    hr = CreateOutputMediaType();
    if (FAILED(hr))
    {
        return hr;
    }
    
    // Set input type
    hr = m_pEncoder->SetInputType(0, m_pInputType, 0);
    if (FAILED(hr))
    {
        return hr;
    }
    
    // Set output type
    hr = m_pEncoder->SetOutputType(0, m_pOutputType, 0);
    if (FAILED(hr))
    {
        return hr;
    }
    
    // Configure quality via ICodecAPI if available
    ICodecAPI* pCodecAPI = nullptr;
    hr = m_pEncoder->QueryInterface(IID_PPV_ARGS(&pCodecAPI));
    if (SUCCEEDED(hr))
    {
        UINT32 quality = GetQualityValue();
        VARIANT var;
        var.vt = VT_UI4;
        var.ulVal = quality;
        
        pCodecAPI->SetValue(&CODECAPI_AVEncCommonQuality, &var);
        pCodecAPI->Release();
    }
    
    return S_OK;
}

HRESULT AACEncoder::CreateInputMediaType()
{
    HRESULT hr = MFCreateMediaType(&m_pInputType);
    if (FAILED(hr))
    {
        return hr;
    }
    
    hr = m_pInputType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
    if (FAILED(hr))
    {
        return hr;
    }
    
    hr = m_pInputType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_PCM);
    if (FAILED(hr))
    {
        return hr;
    }
    
    hr = m_pInputType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, m_config.sampleRate);
    if (FAILED(hr))
    {
        return hr;
    }
    
    hr = m_pInputType->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, m_config.channels);
    if (FAILED(hr))
    {
        return hr;
    }
    
    hr = m_pInputType->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, m_config.bitsPerSample);
    if (FAILED(hr))
    {
        return hr;
    }
    
    UINT32 blockAlign = m_config.channels * (m_config.bitsPerSample / 8);
    hr = m_pInputType->SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, blockAlign);
    if (FAILED(hr))
    {
        return hr;
    }
    
    UINT32 avgBytesPerSecond = m_config.sampleRate * blockAlign;
    hr = m_pInputType->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, avgBytesPerSecond);
    if (FAILED(hr))
    {
        return hr;
    }
    
    return S_OK;
}

HRESULT AACEncoder::CreateOutputMediaType()
{
    HRESULT hr = MFCreateMediaType(&m_pOutputType);
    if (FAILED(hr))
    {
        return hr;
    }
    
    hr = m_pOutputType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
    if (FAILED(hr))
    {
        return hr;
    }
    
    hr = m_pOutputType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_AAC);
    if (FAILED(hr))
    {
        return hr;
    }
    
    hr = m_pOutputType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, m_config.sampleRate);
    if (FAILED(hr))
    {
        return hr;
    }
    
    hr = m_pOutputType->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, m_config.channels);
    if (FAILED(hr))
    {
        return hr;
    }
    
    UINT32 bitrate = CalculateBitrate();
    hr = m_pOutputType->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, bitrate / 8);
    if (FAILED(hr))
    {
        return hr;
    }
    
    // AAC Low Complexity profile
    hr = m_pOutputType->SetUINT32(MF_MT_AAC_AUDIO_PROFILE_LEVEL_INDICATION, 0x29);
    if (FAILED(hr))
    {
        return hr;
    }
    
    return S_OK;
}

UINT32 AACEncoder::CalculateBitrate() const
{
    // Base bitrate per channel
    UINT32 bitratePerChannel = 0;
    
    switch (m_config.quality)
    {
    case AudioQuality::Low:
        bitratePerChannel = 64000;  // 64 kbps per channel
        break;
    case AudioQuality::Medium:
        bitratePerChannel = 96000;  // 96 kbps per channel
        break;
    case AudioQuality::High:
        bitratePerChannel = 128000; // 128 kbps per channel
        break;
    case AudioQuality::VeryHigh:
        bitratePerChannel = 192000; // 192 kbps per channel
        break;
    default:
        bitratePerChannel = 128000;
    }
    
    return bitratePerChannel * m_config.channels;
}

UINT32 AACEncoder::GetQualityValue() const
{
    switch (m_config.quality)
    {
    case AudioQuality::Low:
        return 30;
    case AudioQuality::Medium:
        return 60;
    case AudioQuality::High:
        return 80;
    case AudioQuality::VeryHigh:
        return 100;
    default:
        return 60;
    }
}

HRESULT AACEncoder::EncodeAudio(const uint8_t* pData, uint32_t dataSize, int64_t timestamp, IMFSample** ppSample)
{
    if (!m_isInitialized || !pData || !ppSample)
    {
        return E_INVALIDARG;
    }
    
    std::lock_guard<std::mutex> lock(m_mutex);
    
    auto startTime = std::chrono::high_resolution_clock::now();
    
    // Add data to input buffer
    size_t oldSize = m_inputBuffer.size();
    m_inputBuffer.resize(oldSize + dataSize);
    memcpy(m_inputBuffer.data() + oldSize, pData, dataSize);
    
    // Calculate frame size in bytes
    UINT32 bytesPerSample = m_config.channels * (m_config.bitsPerSample / 8);
    UINT32 frameSizeBytes = m_samplesPerFrame * bytesPerSample;
    
    *ppSample = nullptr;
    
    // Process complete frames
    while (m_inputBuffer.size() >= frameSizeBytes)
    {
        HRESULT hr = ProcessInput(m_inputBuffer.data(), frameSizeBytes, timestamp);
        if (FAILED(hr))
        {
            m_droppedSamples++;
            auto endTime = std::chrono::high_resolution_clock::now();
            m_totalEncodingTime += std::chrono::duration<double, std::milli>(endTime - startTime).count();
            return hr;
        }
        
        // Try to get output
        hr = ProcessOutput(ppSample);
        if (SUCCEEDED(hr) && *ppSample)
        {
            m_encodedSamples++;
            
            // Remove processed data from buffer
            m_inputBuffer.erase(m_inputBuffer.begin(), m_inputBuffer.begin() + frameSizeBytes);
            
            auto endTime = std::chrono::high_resolution_clock::now();
            m_totalEncodingTime += std::chrono::duration<double, std::milli>(endTime - startTime).count();
            
            return S_OK;
        }
        
        // Remove processed data even if no output yet
        m_inputBuffer.erase(m_inputBuffer.begin(), m_inputBuffer.begin() + frameSizeBytes);
    }
    
    auto endTime = std::chrono::high_resolution_clock::now();
    m_totalEncodingTime += std::chrono::duration<double, std::milli>(endTime - startTime).count();
    
    // No complete frame available yet
    return S_FALSE;
}

HRESULT AACEncoder::ProcessInput(const BYTE* pData, DWORD dataSize, LONGLONG timestamp)
{
    IMFMediaBuffer* pBuffer = nullptr;
    HRESULT hr = MFCreateMemoryBuffer(dataSize, &pBuffer);
    if (FAILED(hr))
    {
        return hr;
    }
    
    BYTE* pDest = nullptr;
    hr = pBuffer->Lock(&pDest, nullptr, nullptr);
    if (SUCCEEDED(hr))
    {
        memcpy(pDest, pData, dataSize);
        pBuffer->Unlock();
        pBuffer->SetCurrentLength(dataSize);
    }
    
    if (FAILED(hr))
    {
        pBuffer->Release();
        return hr;
    }
    
    IMFSample* pSample = nullptr;
    hr = MFCreateSample(&pSample);
    if (FAILED(hr))
    {
        pBuffer->Release();
        return hr;
    }
    
    pSample->AddBuffer(pBuffer);
    pSample->SetSampleTime(timestamp);
    
    // Calculate duration based on sample count
    UINT32 bytesPerSample = m_config.channels * (m_config.bitsPerSample / 8);
    UINT32 sampleCount = dataSize / bytesPerSample;
    LONGLONG duration = (sampleCount * 10000000LL) / m_config.sampleRate;
    pSample->SetSampleDuration(duration);
    
    hr = m_pEncoder->ProcessInput(0, pSample, 0);
    
    pSample->Release();
    pBuffer->Release();
    
    return hr;
}

HRESULT AACEncoder::ProcessOutput(IMFSample** ppSample)
{
    MFT_OUTPUT_DATA_BUFFER outputBuffer = { 0 };
    outputBuffer.dwStreamID = 0;
    outputBuffer.pSample = nullptr;
    outputBuffer.dwStatus = 0;
    outputBuffer.pEvents = nullptr;
    
    DWORD status = 0;
    HRESULT hr = m_pEncoder->ProcessOutput(0, 1, &outputBuffer, &status);
    
    if (hr == MF_E_TRANSFORM_NEED_MORE_INPUT)
    {
        return S_FALSE;
    }
    
    if (FAILED(hr))
    {
        if (outputBuffer.pSample)
        {
            outputBuffer.pSample->Release();
        }
        if (outputBuffer.pEvents)
        {
            outputBuffer.pEvents->Release();
        }
        return hr;
    }
    
    *ppSample = outputBuffer.pSample;
    
    if (outputBuffer.pEvents)
    {
        outputBuffer.pEvents->Release();
    }
    
    return S_OK;
}

HRESULT AACEncoder::Flush(IMFSample** ppSample)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (ppSample)
    {
        *ppSample = nullptr;
    }
    
    if (m_pEncoder)
    {
        m_pEncoder->ProcessMessage(MFT_MESSAGE_NOTIFY_END_OF_STREAM, 0);
        m_pEncoder->ProcessMessage(MFT_MESSAGE_COMMAND_DRAIN, 0);
        
        // Try to get any remaining output
        if (ppSample)
        {
            ProcessOutput(ppSample);
        }
        
        m_pEncoder->Release();
        m_pEncoder = nullptr;
    }
    
    if (m_pInputType)
    {
        m_pInputType->Release();
        m_pInputType = nullptr;
    }
    
    if (m_pOutputType)
    {
        m_pOutputType->Release();
        m_pOutputType = nullptr;
    }
    
    m_inputBuffer.clear();
    m_isInitialized = false;
    
    return S_OK;
}

HRESULT AACEncoder::GetCapabilities(AudioEncoderCapabilities* pCapabilities) const
{
    if (!pCapabilities)
    {
        return E_POINTER;
    }
    
    pCapabilities->supportsAAC = true;
    pCapabilities->supportsFLAC = false;
    pCapabilities->supportsOpus = false;
    pCapabilities->supportsPCM = false;
    pCapabilities->maxChannels = 8;  // AAC supports up to 7.1
    pCapabilities->maxSampleRate = 96000;
    
    return S_OK;
}

bool AACEncoder::SupportsCodec(AudioCodec codec) const
{
    return codec == AudioCodec::AAC;
}

uint64_t AACEncoder::GetEncodedSampleCount() const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    return m_encodedSamples;
}

double AACEncoder::GetAverageEncodingTimeMs() const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    if (m_encodedSamples == 0)
    {
        return 0.0;
    }
    return m_totalEncodingTime / m_encodedSamples;
}
