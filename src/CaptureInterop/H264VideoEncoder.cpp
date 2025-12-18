#include "pch.h"
#include "H264VideoEncoder.h"
#include <codecapi.h>
#include <chrono>
#include <numeric>

using namespace CaptureInterop;

H264VideoEncoder::H264VideoEncoder()
    : m_refCount(1)
    , m_initialized(false)
    , m_running(false)
    , m_hardwareEncoderAvailable(false)
    , m_usingHardwareEncoder(false)
    , m_pEncoder(nullptr)
    , m_pInputType(nullptr)
    , m_pOutputType(nullptr)
    , m_pD3DDevice(nullptr)
    , m_pD3DContext(nullptr)
    , m_pTextureConverter(nullptr)
    , m_encodedFrameCount(0)
    , m_droppedFrameCount(0)
    , m_totalEncodingTimeMs(0.0)
{
    m_recentEncodingTimes.reserve(MAX_RECENT_SAMPLES);
}

H264VideoEncoder::~H264VideoEncoder()
{
    Stop();
    
    if (m_pTextureConverter)
    {
        delete m_pTextureConverter;
        m_pTextureConverter = nullptr;
    }
    
    if (m_pEncoder)
    {
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
    
    if (m_pD3DContext)
    {
        m_pD3DContext->Release();
        m_pD3DContext = nullptr;
    }
    
    if (m_pD3DDevice)
    {
        m_pD3DDevice->Release();
        m_pD3DDevice = nullptr;
    }
}

uint32_t H264VideoEncoder::AddRef()
{
    return ++m_refCount;
}

uint32_t H264VideoEncoder::Release()
{
    uint32_t count = --m_refCount;
    if (count == 0)
    {
        delete this;
    }
    return count;
}

HRESULT H264VideoEncoder::Initialize()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (m_initialized)
    {
        return S_OK;
    }
    
    // Initialize Media Foundation
    HRESULT hr = MFStartup(MF_VERSION);
    if (FAILED(hr))
    {
        return hr;
    }
    
    // Detect hardware encoder capability
    hr = DetectHardwareEncoder();
    if (SUCCEEDED(hr))
    {
        m_capabilities.supportsHardwareAcceleration = m_hardwareEncoderAvailable;
    }
    
    // Set default capabilities
    m_capabilities.supportsH264 = true;
    m_capabilities.supportsH265 = false; // Future
    m_capabilities.supportsVP9 = false;  // Future
    m_capabilities.supportsAV1 = false;  // Future
    m_capabilities.supportsLossless = false; // H264 doesn't support true lossless
    m_capabilities.maxWidth = 7680;  // 8K
    m_capabilities.maxHeight = 4320;
    
    m_initialized = true;
    return S_OK;
}

HRESULT H264VideoEncoder::Start()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (!m_initialized)
    {
        return E_NOT_VALID_STATE;
    }
    
    if (m_running)
    {
        return S_OK;
    }
    
    // Create encoder based on configuration
    HRESULT hr = S_OK;
    
    if (m_config.hardwareAcceleration && m_hardwareEncoderAvailable)
    {
        hr = CreateHardwareEncoder();
        if (SUCCEEDED(hr))
        {
            m_usingHardwareEncoder = true;
        }
        else
        {
            // Fall back to software
            hr = CreateSoftwareEncoder();
            m_usingHardwareEncoder = false;
        }
    }
    else
    {
        hr = CreateSoftwareEncoder();
        m_usingHardwareEncoder = false;
    }
    
    if (FAILED(hr))
    {
        return hr;
    }
    
    // Configure the encoder
    hr = ConfigureEncoder();
    if (FAILED(hr))
    {
        return hr;
    }
    
    // Start streaming
    hr = m_pEncoder->ProcessMessage(MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, 0);
    if (FAILED(hr))
    {
        return hr;
    }
    
    hr = m_pEncoder->ProcessMessage(MFT_MESSAGE_NOTIFY_START_OF_STREAM, 0);
    if (FAILED(hr))
    {
        return hr;
    }
    
    m_running = true;
    return S_OK;
}

HRESULT H264VideoEncoder::Stop()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (!m_running)
    {
        return S_OK;
    }
    
    if (m_pEncoder)
    {
        m_pEncoder->ProcessMessage(MFT_MESSAGE_NOTIFY_END_OF_STREAM, 0);
        m_pEncoder->ProcessMessage(MFT_MESSAGE_COMMAND_DRAIN, 0);
    }
    
    m_running = false;
    return S_OK;
}

bool H264VideoEncoder::IsRunning() const
{
    return m_running;
}

HRESULT H264VideoEncoder::Configure(const VideoEncoderConfig& config)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (m_running)
    {
        return E_NOT_VALID_STATE;
    }
    
    // Validate configuration
    if (config.codec != VideoCodec::H264)
    {
        return E_INVALIDARG;
    }
    
    if (config.width == 0 || config.height == 0)
    {
        return E_INVALIDARG;
    }
    
    if (config.width > m_capabilities.maxWidth || config.height > m_capabilities.maxHeight)
    {
        return E_INVALIDARG;
    }
    
    m_config = config;
    
    // Calculate bitrate if not specified
    if (m_config.bitrate == 0)
    {
        m_config.bitrate = CalculateVideoBitrate(m_config.preset, m_config.width, m_config.height, 
                                                  m_config.frameRateNum / m_config.frameRateDen);
    }
    
    return S_OK;
}

HRESULT H264VideoEncoder::GetConfiguration(VideoEncoderConfig* pConfig) const
{
    if (!pConfig)
    {
        return E_POINTER;
    }
    
    std::lock_guard<std::mutex> lock(m_mutex);
    *pConfig = m_config;
    return S_OK;
}

HRESULT H264VideoEncoder::GetCapabilities(VideoEncoderCapabilities* pCapabilities) const
{
    if (!pCapabilities)
    {
        return E_POINTER;
    }
    
    std::lock_guard<std::mutex> lock(m_mutex);
    *pCapabilities = m_capabilities;
    return S_OK;
}

bool H264VideoEncoder::SupportsCodec(VideoCodec codec) const
{
    return codec == VideoCodec::H264;
}

HRESULT H264VideoEncoder::EncodeFrame(ID3D11Texture2D* pTexture, int64_t timestamp, IMFSample** ppSample)
{
    if (!pTexture || !ppSample)
    {
        return E_POINTER;
    }
    
    if (!m_running)
    {
        return E_NOT_VALID_STATE;
    }
    
    auto startTime = std::chrono::high_resolution_clock::now();
    
    // Convert texture to MF sample
    IMFSample* pInputSample = nullptr;
    HRESULT hr = ConvertTextureToSample(pTexture, &pInputSample);
    if (FAILED(hr))
    {
        m_droppedFrameCount++;
        return hr;
    }
    
    // Set timestamp
    pInputSample->SetSampleTime(timestamp);
    pInputSample->SetSampleDuration(10000000LL * m_config.frameRateDen / m_config.frameRateNum);
    
    // Process input
    hr = m_pEncoder->ProcessInput(0, pInputSample, 0);
    pInputSample->Release();
    
    if (FAILED(hr))
    {
        m_droppedFrameCount++;
        return hr;
    }
    
    // Get encoded output
    hr = ProcessEncoderOutput(ppSample);
    
    auto endTime = std::chrono::high_resolution_clock::now();
    double encodingTimeMs = std::chrono::duration<double, std::milli>(endTime - startTime).count();
    UpdateEncodingStats(encodingTimeMs);
    
    if (SUCCEEDED(hr))
    {
        m_encodedFrameCount++;
    }
    else
    {
        m_droppedFrameCount++;
    }
    
    return hr;
}

HRESULT H264VideoEncoder::Flush(IMFSample** ppSample)
{
    if (!ppSample)
    {
        return E_POINTER;
    }
    
    if (!m_running)
    {
        return E_NOT_VALID_STATE;
    }
    
    HRESULT hr = m_pEncoder->ProcessMessage(MFT_MESSAGE_COMMAND_DRAIN, 0);
    if (FAILED(hr))
    {
        return hr;
    }
    
    return ProcessEncoderOutput(ppSample);
}

uint64_t H264VideoEncoder::GetEncodedFrameCount() const
{
    return m_encodedFrameCount;
}

uint64_t H264VideoEncoder::GetDroppedFrameCount() const
{
    return m_droppedFrameCount;
}

double H264VideoEncoder::GetAverageEncodingTimeMs() const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (m_recentEncodingTimes.empty())
    {
        return 0.0;
    }
    
    double sum = std::accumulate(m_recentEncodingTimes.begin(), m_recentEncodingTimes.end(), 0.0);
    return sum / m_recentEncodingTimes.size();
}

// Private helper methods

HRESULT H264VideoEncoder::DetectHardwareEncoder()
{
    // Try to enumerate hardware H.264 encoders
    UINT32 count = 0;
    IMFActivate** ppActivate = nullptr;
    
    MFT_REGISTER_TYPE_INFO outputType = { MFMediaType_Video, MFVideoFormat_H264 };
    
    HRESULT hr = MFTEnumEx(
        MFT_CATEGORY_VIDEO_ENCODER,
        MFT_ENUM_FLAG_HARDWARE | MFT_ENUM_FLAG_SORTANDFILTER,
        nullptr,
        &outputType,
        &ppActivate,
        &count);
    
    if (SUCCEEDED(hr) && count > 0)
    {
        m_hardwareEncoderAvailable = true;
        
        // Cleanup
        for (UINT32 i = 0; i < count; i++)
        {
            ppActivate[i]->Release();
        }
        CoTaskMemFree(ppActivate);
    }
    else
    {
        m_hardwareEncoderAvailable = false;
    }
    
    return S_OK;
}

HRESULT H264VideoEncoder::CreateSoftwareEncoder()
{
    return CoCreateInstance(
        CLSID_CMSH264EncoderMFT,
        nullptr,
        CLSCTX_INPROC_SERVER,
        IID_PPV_ARGS(&m_pEncoder));
}

HRESULT H264VideoEncoder::CreateHardwareEncoder()
{
    // Enumerate hardware encoders
    UINT32 count = 0;
    IMFActivate** ppActivate = nullptr;
    
    MFT_REGISTER_TYPE_INFO outputType = { MFMediaType_Video, MFVideoFormat_H264 };
    
    HRESULT hr = MFTEnumEx(
        MFT_CATEGORY_VIDEO_ENCODER,
        MFT_ENUM_FLAG_HARDWARE | MFT_ENUM_FLAG_SORTANDFILTER,
        nullptr,
        &outputType,
        &ppActivate,
        &count);
    
    if (FAILED(hr) || count == 0)
    {
        return E_FAIL;
    }
    
    // Activate the first hardware encoder
    hr = ppActivate[0]->ActivateObject(IID_PPV_ARGS(&m_pEncoder));
    
    // Cleanup
    for (UINT32 i = 0; i < count; i++)
    {
        ppActivate[i]->Release();
    }
    CoTaskMemFree(ppActivate);
    
    return hr;
}

HRESULT H264VideoEncoder::ConfigureEncoder()
{
    if (!m_pEncoder)
    {
        return E_POINTER;
    }
    
    // Create input media type
    HRESULT hr = CreateInputMediaType(&m_pInputType);
    if (FAILED(hr))
    {
        return hr;
    }
    
    hr = m_pEncoder->SetInputType(0, m_pInputType, 0);
    if (FAILED(hr))
    {
        return hr;
    }
    
    // Create output media type
    hr = CreateOutputMediaType(&m_pOutputType);
    if (FAILED(hr))
    {
        return hr;
    }
    
    hr = m_pEncoder->SetOutputType(0, m_pOutputType, 0);
    if (FAILED(hr))
    {
        return hr;
    }
    
    // Set encoder properties based on preset
    if (m_pEncoder)
    {
        ICodecAPI* pCodecAPI = nullptr;
        hr = m_pEncoder->QueryInterface(IID_PPV_ARGS(&pCodecAPI));
        if (SUCCEEDED(hr))
        {
            // Set bitrate mode and target bitrate
            VARIANT var;
            VariantInit(&var);
            var.vt = VT_UI4;
            var.ulVal = eAVEncCommonRateControlMode_CBR; // Constant bitrate
            pCodecAPI->SetValue(&CODECAPI_AVEncCommonRateControlMode, &var);
            
            var.ulVal = m_config.bitrate;
            pCodecAPI->SetValue(&CODECAPI_AVEncCommonMeanBitRate, &var);
            
            // Set quality based on preset
            switch (m_config.preset)
            {
            case EncoderPreset::Fast:
                var.ulVal = 30; // Lower quality for speed
                break;
            case EncoderPreset::Balanced:
                var.ulVal = 60; // Balanced
                break;
            case EncoderPreset::Quality:
                var.ulVal = 90; // High quality
                break;
            case EncoderPreset::Lossless:
                var.ulVal = 100; // Maximum quality
                break;
            }
            pCodecAPI->SetValue(&CODECAPI_AVEncCommonQuality, &var);
            
            VariantClear(&var);
            pCodecAPI->Release();
        }
    }
    
    return S_OK;
}

HRESULT H264VideoEncoder::CreateInputMediaType(IMFMediaType** ppMediaType)
{
    if (!ppMediaType)
    {
        return E_POINTER;
    }
    
    HRESULT hr = MFCreateMediaType(ppMediaType);
    if (FAILED(hr))
    {
        return hr;
    }
    
    IMFMediaType* pType = *ppMediaType;
    
    hr = pType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
    if (FAILED(hr)) return hr;
    
    hr = pType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_NV12);
    if (FAILED(hr)) return hr;
    
    hr = MFSetAttributeSize(pType, MF_MT_FRAME_SIZE, m_config.width, m_config.height);
    if (FAILED(hr)) return hr;
    
    hr = MFSetAttributeRatio(pType, MF_MT_FRAME_RATE, m_config.frameRateNum, m_config.frameRateDen);
    if (FAILED(hr)) return hr;
    
    hr = MFSetAttributeRatio(pType, MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
    if (FAILED(hr)) return hr;
    
    hr = pType->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
    if (FAILED(hr)) return hr;
    
    return S_OK;
}

HRESULT H264VideoEncoder::CreateOutputMediaType(IMFMediaType** ppMediaType)
{
    if (!ppMediaType)
    {
        return E_POINTER;
    }
    
    HRESULT hr = MFCreateMediaType(ppMediaType);
    if (FAILED(hr))
    {
        return hr;
    }
    
    IMFMediaType* pType = *ppMediaType;
    
    hr = pType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
    if (FAILED(hr)) return hr;
    
    hr = pType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_H264);
    if (FAILED(hr)) return hr;
    
    hr = MFSetAttributeSize(pType, MF_MT_FRAME_SIZE, m_config.width, m_config.height);
    if (FAILED(hr)) return hr;
    
    hr = MFSetAttributeRatio(pType, MF_MT_FRAME_RATE, m_config.frameRateNum, m_config.frameRateDen);
    if (FAILED(hr)) return hr;
    
    hr = pType->SetUINT32(MF_MT_AVG_BITRATE, m_config.bitrate);
    if (FAILED(hr)) return hr;
    
    hr = pType->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
    if (FAILED(hr)) return hr;
    
    // Set H.264 profile
    hr = pType->SetUINT32(MF_MT_MPEG2_PROFILE, eAVEncH264VProfile_High);
    if (FAILED(hr)) return hr;
    
    return S_OK;
}

HRESULT H264VideoEncoder::ConvertTextureToSample(ID3D11Texture2D* pTexture, IMFSample** ppSample)
{
    if (!pTexture || !ppSample)
    {
        return E_INVALIDARG;
    }
    
    // Initialize texture converter on first use
    if (!m_pTextureConverter)
    {
        m_pTextureConverter = new TextureConverter();
        HRESULT hr = m_pTextureConverter->Initialize(m_config.d3dDevice, m_config.width, m_config.height);
        if (FAILED(hr))
        {
            delete m_pTextureConverter;
            m_pTextureConverter = nullptr;
            return hr;
        }
    }
    
    // Convert texture to Media Foundation sample
    // Timestamp will be set by the encoder based on frame timing
    int64_t timestamp = 0; // Placeholder - actual timestamp should come from capture
    return m_pTextureConverter->ConvertTextureToSample(pTexture, timestamp, ppSample);
}

HRESULT H264VideoEncoder::ProcessEncoderOutput(IMFSample** ppSample)
{
    if (!ppSample)
    {
        return E_POINTER;
    }
    
    MFT_OUTPUT_DATA_BUFFER outputBuffer = { 0 };
    outputBuffer.dwStreamID = 0;
    outputBuffer.pSample = nullptr;
    outputBuffer.dwStatus = 0;
    outputBuffer.pEvents = nullptr;
    
    DWORD status = 0;
    HRESULT hr = m_pEncoder->ProcessOutput(0, 1, &outputBuffer, &status);
    
    if (hr == MF_E_TRANSFORM_NEED_MORE_INPUT)
    {
        return S_FALSE; // Need more input before output is available
    }
    
    if (FAILED(hr))
    {
        return hr;
    }
    
    *ppSample = outputBuffer.pSample;
    
    if (outputBuffer.pEvents)
    {
        outputBuffer.pEvents->Release();
    }
    
    return S_OK;
}

void H264VideoEncoder::UpdateEncodingStats(double encodingTimeMs)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    m_totalEncodingTimeMs += encodingTimeMs;
    
    m_recentEncodingTimes.push_back(encodingTimeMs);
    if (m_recentEncodingTimes.size() > MAX_RECENT_SAMPLES)
    {
        m_recentEncodingTimes.erase(m_recentEncodingTimes.begin());
    }
}

uint32_t EncoderPresets::CalculateVideoBitrate(EncoderPreset preset, uint32_t width, uint32_t height, uint32_t fps)
{
    // Calculate pixels per second
    uint64_t pixelsPerSecond = static_cast<uint64_t>(width) * height * fps;
    
    // Bits per pixel based on preset
    double bitsPerPixel = 0.0;
    
    switch (preset)
    {
    case EncoderPreset::Fast:
        bitsPerPixel = 0.08;  // ~5 Mbps for 1080p30
        break;
    case EncoderPreset::Balanced:
        bitsPerPixel = 0.12;  // ~8 Mbps for 1080p30
        break;
    case EncoderPreset::Quality:
        bitsPerPixel = 0.20;  // ~13 Mbps for 1080p30
        break;
    case EncoderPreset::Lossless:
        bitsPerPixel = 0.30;  // ~20 Mbps for 1080p30
        break;
    default:
        bitsPerPixel = 0.12;
        break;
    }
    
    return static_cast<uint32_t>(pixelsPerSecond * bitsPerPixel);
}
