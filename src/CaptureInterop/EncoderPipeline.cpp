#include "pch.h"
#include "EncoderPipeline.h"
#include "H264VideoEncoder.h"
#include "AACEncoder.h"
#include "MP4Muxer.h"
#include <sstream>

EncoderPipeline::EncoderPipeline()
    : m_refCount(1)
    , m_isInitialized(false)
    , m_isRunning(false)
    , m_videoEncoder(nullptr)
    , m_muxer(nullptr)
    , m_videoTrackIndex(0)
    , m_startTime(0)
{
}

EncoderPipeline::~EncoderPipeline()
{
    Cleanup();
}

ULONG EncoderPipeline::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

ULONG EncoderPipeline::Release()
{
    ULONG count = InterlockedDecrement(&m_refCount);
    if (count == 0)
    {
        delete this;
    }
    return count;
}

HRESULT EncoderPipeline::Initialize(const EncoderPipelineConfig& config)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (m_isInitialized)
    {
        return E_UNEXPECTED;
    }
    
    // Validate configuration
    if (config.videoWidth == 0 || config.videoHeight == 0 || config.videoFPS == 0)
    {
        return E_INVALIDARG;
    }
    
    if (config.audioTrackCount == 0 || config.audioTrackCount > 6)
    {
        return E_INVALIDARG;
    }
    
    if (config.outputPath.empty() || config.d3dDevice == nullptr)
    {
        return E_INVALIDARG;
    }
    
    // Store configuration
    m_config = config;
    
    // Initialize muxer first (needs to be ready for encoders)
    HRESULT hr = InitializeMuxer();
    if (FAILED(hr))
    {
        Cleanup();
        return hr;
    }
    
    // Initialize video encoder
    hr = InitializeVideoEncoder();
    if (FAILED(hr))
    {
        Cleanup();
        return hr;
    }
    
    // Initialize audio encoders (one per track)
    hr = InitializeAudioEncoders();
    if (FAILED(hr))
    {
        Cleanup();
        return hr;
    }
    
    // Initialize statistics
    m_stats = PipelineStatistics();
    m_stats.audioSamplesProcessed.resize(config.audioTrackCount, 0);
    m_stats.audioSamplesEncoded.resize(config.audioTrackCount, 0);
    m_stats.audioSamplesDropped.resize(config.audioTrackCount, 0);
    m_stats.audioByteSize.resize(config.audioTrackCount, 0);
    m_stats.averageAudioEncodingTimeMs.resize(config.audioTrackCount, 0.0);
    
    m_audioEncodingTimes.resize(config.audioTrackCount);
    
    m_isInitialized = true;
    return S_OK;
}

HRESULT EncoderPipeline::InitializeVideoEncoder()
{
    // Create video encoder
    m_videoEncoder = new (std::nothrow) CaptureInterop::H264VideoEncoder();
    if (!m_videoEncoder)
    {
        return E_OUTOFMEMORY;
    }
    
    m_videoEncoder->AddRef();
    
    // Create video encoder configuration
    CaptureInterop::VideoEncoderConfig videoConfig;
    videoConfig.codec = CaptureInterop::VideoCodec::H264;
    videoConfig.width = m_config.videoWidth;
    videoConfig.height = m_config.videoHeight;
    videoConfig.frameRateNum = m_config.videoFPS;
    videoConfig.frameRateDen = 1;
    videoConfig.preset = m_config.videoPreset;
    videoConfig.hardwareAcceleration = m_config.useHardwareEncoding;
    videoConfig.bitrate = 0;  // Auto
    
    // Initialize encoder
    HRESULT hr = m_videoEncoder->Configure(videoConfig);
    if (FAILED(hr))
    {
        m_videoEncoder->Release();
        m_videoEncoder = nullptr;
        return hr;
    }
    
    // Create video media type for muxer
    wil::com_ptr<IMFMediaType> videoMediaType;
    hr = MFCreateMediaType(videoMediaType.put());
    if (FAILED(hr))
    {
        m_videoEncoder->Release();
        m_videoEncoder = nullptr;
        return hr;
    }
    
    videoMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
    videoMediaType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_H264);
    MFSetAttributeSize(videoMediaType.get(), MF_MT_FRAME_SIZE, m_config.videoWidth, m_config.videoHeight);
    MFSetAttributeRatio(videoMediaType.get(), MF_MT_FRAME_RATE, m_config.videoFPS, 1);
    videoMediaType->SetUINT32(MF_MT_AVG_BITRATE, videoConfig.bitrate);
    videoMediaType->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
    
    // Create TrackInfo for video track
    CaptureInterop::TrackInfo videoTrack;
    videoTrack.type = CaptureInterop::TrackType::Video;
    videoTrack.name = L"Video";
    videoTrack.pMediaType = videoMediaType.get();
    
    // Add track to muxer
    uint32_t videoTrackIdx = 0;
    hr = m_muxer->AddTrack(videoTrack, &videoTrackIdx);
    m_videoTrackIndex = videoTrackIdx;
    
    if (FAILED(hr))
    {
        m_videoEncoder->Release();
        m_videoEncoder = nullptr;
        return hr;
    }
    
    return S_OK;
}

HRESULT EncoderPipeline::InitializeAudioEncoders()
{
    // Pre-allocate encoder vector
    m_audioEncoders.resize(m_config.audioTrackCount, nullptr);
    
    for (UINT32 i = 0; i < m_config.audioTrackCount; i++)
    {
        // Create AAC encoder
        AACEncoder* encoder = new (std::nothrow) AACEncoder();
        if (!encoder)
        {
            return E_OUTOFMEMORY;
        }
        
        encoder->AddRef();
        m_audioEncoders[i] = encoder;
        
        // Create audio encoder configuration
        CaptureInterop::AudioEncoderConfig audioConfig;
        audioConfig.codec = CaptureInterop::AudioCodec::AAC;
        audioConfig.sampleRate = m_config.audioSampleRate;
        audioConfig.channels = m_config.audioChannels;
        audioConfig.bitsPerSample = m_config.audioBitsPerSample;
        audioConfig.quality = m_config.audioQuality;
        audioConfig.bitrate = 0;  // Auto
        
        // Initialize encoder
        HRESULT hr = encoder->Configure(audioConfig);
        if (FAILED(hr))
        {
            return hr;
        }
        
        // Track mapping: by default, AudioMixer track i → encoder i
        m_trackToEncoderMap[i] = i;
    }
    
    return S_OK;
}

HRESULT EncoderPipeline::InitializeMuxer()
{
    // Create MP4 muxer
    m_muxer = new (std::nothrow) CaptureInterop::MP4Muxer();
    if (!m_muxer)
    {
        return E_OUTOFMEMORY;
    }
    
    m_muxer->AddRef();
    
    // Create muxer configuration
    CaptureInterop::MuxerConfig muxerConfig;
    muxerConfig.format = CaptureInterop::ContainerFormat::MP4;
    muxerConfig.outputPath = m_config.outputPath;
    muxerConfig.fastStart = true;
    muxerConfig.fragmentedMP4 = false;
    
    // Initialize muxer (only takes one parameter)
    HRESULT hr = m_muxer->Initialize(muxerConfig);
    if (FAILED(hr))
    {
        m_muxer->Release();
        m_muxer = nullptr;
        return hr;
    }
    
    // Set D3D device for hardware encoding support
    hr = m_muxer->SetD3DDevice(m_config.d3dDevice);
    if (FAILED(hr))
    {
        m_muxer->Release();
        m_muxer = nullptr;
        return hr;
    }
    
    return S_OK;
}

HRESULT EncoderPipeline::InitializeAudioTrack(UINT32 trackIndex, const WAVEFORMATEX* format, const wchar_t* trackName)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (!m_isInitialized || trackIndex >= m_config.audioTrackCount)
    {
        return E_INVALIDARG;
    }
    
    // Create audio media type
    wil::com_ptr<IMFMediaType> audioMediaType;
    HRESULT hr = MFCreateMediaType(audioMediaType.put());
    if (FAILED(hr))
    {
        return hr;
    }
    
    audioMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
    audioMediaType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_AAC);
    audioMediaType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, format->nSamplesPerSec);
    audioMediaType->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, format->nChannels);
    audioMediaType->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
    audioMediaType->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, 20000); // 160 kbps AAC
    
    // Create TrackInfo for audio track
    CaptureInterop::TrackInfo audioTrack;
    audioTrack.type = CaptureInterop::TrackType::Audio;
    audioTrack.name = trackName ? trackName : L"Audio";
    audioTrack.pMediaType = audioMediaType.get();
    
    // Add audio track to muxer
    uint32_t muxerTrackIndex = 0;
    hr = m_muxer->AddTrack(audioTrack, &muxerTrackIndex);
    if (FAILED(hr))
    {
        return hr;
    }
    
    // Note: Track indices should align between AudioMixer, encoder array, and muxer
    // This is handled by the m_trackToEncoderMap
    
    return S_OK;
}

HRESULT EncoderPipeline::Start()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (!m_isInitialized || m_isRunning)
    {
        return E_UNEXPECTED;
    }
    
    // Start muxer
    HRESULT hr = m_muxer->Start();
    if (FAILED(hr))
    {
        return hr;
    }
    
    m_startTime = GetTickCount64();
    m_isRunning = true;
    m_stats.isRunning = true;
    
    return S_OK;
}

HRESULT EncoderPipeline::Stop()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (!m_isRunning)
    {
        return S_FALSE;  // Already stopped
    }
    
    m_isRunning = false;
    m_stats.isRunning = false;
    m_stats.totalDurationMs = GetTickCount64() - m_startTime;
    
    // Stop muxer
    HRESULT hr = m_muxer->Stop();
    
    return hr;
}

HRESULT EncoderPipeline::ProcessVideoFrame(ID3D11Texture2D* texture, UINT64 timestamp)
{
    if (!m_isRunning || !texture || !m_videoEncoder)
    {
        return E_UNEXPECTED;
    }
    
    // Update statistics
    m_stats.videoFramesProcessed++;
    
    LARGE_INTEGER start, end, frequency;
    QueryPerformanceCounter(&start);
    QueryPerformanceFrequency(&frequency);
    
    // Encode video frame
    IMFSample* encodedSample = nullptr;
    HRESULT hr = m_videoEncoder->EncodeFrame(texture, timestamp, &encodedSample);
    
    QueryPerformanceCounter(&end);
    double elapsedMs = (double)(end.QuadPart - start.QuadPart) * 1000.0 / (double)frequency.QuadPart;
    
    if (SUCCEEDED(hr) && encodedSample)
    {
        m_stats.videoFramesEncoded++;
        m_videoEncodingTimes.push_back(elapsedMs);
        
        // Calculate average encoding time (keep last 100 samples)
        if (m_videoEncodingTimes.size() > 100)
        {
            m_videoEncodingTimes.erase(m_videoEncodingTimes.begin());
        }
        
        double sum = 0.0;
        for (double time : m_videoEncodingTimes)
        {
            sum += time;
        }
        m_stats.averageVideoEncodingTimeMs = sum / m_videoEncodingTimes.size();
        
        // Get encoded data size for statistics
        IMFMediaBuffer* buffer = nullptr;
        if (SUCCEEDED(encodedSample->ConvertToContiguousBuffer(&buffer)))
        {
            DWORD dataLen = 0;
            buffer->GetCurrentLength(&dataLen);
            m_stats.videoBytesize += dataLen;
            buffer->Release();
        }
        
        // Write to muxer using WriteSample
        hr = m_muxer->WriteSample(m_videoTrackIndex, encodedSample);
        
        encodedSample->Release();
    }
    else
    {
        m_stats.videoFramesDropped++;
    }
    
    return hr;
}

HRESULT EncoderPipeline::ProcessAudioSamples(const BYTE* data, UINT32 length, UINT64 timestamp, UINT32 trackIndex)
{
    if (!m_isRunning || !data || length == 0 || trackIndex >= m_config.audioTrackCount)
    {
        return E_INVALIDARG;
    }
    
    // Get the corresponding encoder
    auto it = m_trackToEncoderMap.find(trackIndex);
    if (it == m_trackToEncoderMap.end())
    {
        return E_INVALIDARG;
    }
    
    UINT32 encoderIndex = it->second;
    if (encoderIndex >= m_audioEncoders.size() || !m_audioEncoders[encoderIndex])
    {
        return E_UNEXPECTED;
    }
    
    AACEncoder* encoder = m_audioEncoders[encoderIndex];
    
    // Update statistics
    m_stats.audioSamplesProcessed[trackIndex]++;
    
    LARGE_INTEGER start, end, frequency;
    QueryPerformanceCounter(&start);
    QueryPerformanceFrequency(&frequency);
    
    // Encode audio samples
    IMFSample* encodedSample = nullptr;
    HRESULT hr = encoder->EncodeAudio((const uint8_t*)data, length, timestamp, &encodedSample);
    
    QueryPerformanceCounter(&end);
    double elapsedMs = (double)(end.QuadPart - start.QuadPart) * 1000.0 / (double)frequency.QuadPart;
    
    if (SUCCEEDED(hr) && encodedSample)
    {
        m_stats.audioSamplesEncoded[trackIndex]++;
        m_audioEncodingTimes[trackIndex].push_back(elapsedMs);
        
        // Calculate average encoding time (keep last 100 samples)
        if (m_audioEncodingTimes[trackIndex].size() > 100)
        {
            m_audioEncodingTimes[trackIndex].erase(m_audioEncodingTimes[trackIndex].begin());
        }
        
        double sum = 0.0;
        for (double time : m_audioEncodingTimes[trackIndex])
        {
            sum += time;
        }
        m_stats.averageAudioEncodingTimeMs[trackIndex] = sum / m_audioEncodingTimes[trackIndex].size();
        
        // Get encoded data size for statistics
        IMFMediaBuffer* buffer = nullptr;
        if (SUCCEEDED(encodedSample->ConvertToContiguousBuffer(&buffer)))
        {
            DWORD dataLen = 0;
            buffer->GetCurrentLength(&dataLen);
            m_stats.audioByteSize[trackIndex] += dataLen;
            buffer->Release();
        }
        
        // Write to muxer using WriteSample
        hr = m_muxer->WriteSample(trackIndex, encodedSample);
        
        encodedSample->Release();
    }
    else
    {
        m_stats.audioSamplesDropped[trackIndex]++;
    }
    
    return hr;
}

void EncoderPipeline::GetStatistics(PipelineStatistics* stats) const
{
    if (stats)
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        *stats = m_stats;
    }
}

void EncoderPipeline::ResetStatistics()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    m_stats = PipelineStatistics();
    m_stats.audioSamplesProcessed.resize(m_config.audioTrackCount, 0);
    m_stats.audioSamplesEncoded.resize(m_config.audioTrackCount, 0);
    m_stats.audioSamplesDropped.resize(m_config.audioTrackCount, 0);
    m_stats.audioByteSize.resize(m_config.audioTrackCount, 0);
    m_stats.averageAudioEncodingTimeMs.resize(m_config.audioTrackCount, 0.0);
    
    m_videoEncodingTimes.clear();
    m_audioEncodingTimes.clear();
    m_audioEncodingTimes.resize(m_config.audioTrackCount);
}

void EncoderPipeline::Cleanup()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (m_isRunning)
    {
        Stop();
    }
    
    // Release video encoder
    if (m_videoEncoder)
    {
        m_videoEncoder->Release();
        m_videoEncoder = nullptr;
    }
    
    // Release audio encoders
    for (auto encoder : m_audioEncoders)
    {
        if (encoder)
        {
            encoder->Release();
        }
    }
    m_audioEncoders.clear();
    m_trackToEncoderMap.clear();
    
    // Release muxer
    if (m_muxer)
    {
        m_muxer->Release();
        m_muxer = nullptr;
    }
    
    m_isInitialized = false;
}
