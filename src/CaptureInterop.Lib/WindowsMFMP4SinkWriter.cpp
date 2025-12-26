#include "pch.h"
#include "WindowsMFMP4SinkWriter.h"
#include "MediaTimeConstants.h"

WindowsMFMP4SinkWriter::WindowsMFMP4SinkWriter() = default;

WindowsMFMP4SinkWriter::~WindowsMFMP4SinkWriter()
{
    Finalize();
}

bool WindowsMFMP4SinkWriter::Initialize(const wchar_t* outputPath, ID3D11Device* device, uint32_t width, uint32_t height, long* outHr)
{
    if (outHr) *outHr = S_OK;
    
    // Smart pointer handles AddRef automatically
    m_device = device;
    
    // Get context through com_ptr
    m_device->GetImmediateContext(m_context.put());
    
    m_width = width;
    m_height = height;
    m_frameIndex = 0;

    HRESULT hr = MFStartup(MF_VERSION);
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    // Create attributes to enable hardware acceleration and improve performance
    wil::com_ptr<IMFAttributes> attributes;
    hr = MFCreateAttributes(attributes.put(), 3);
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

    // Output type: H.264
    wil::com_ptr<IMFMediaType> mediaTypeOut;
    hr = MFCreateMediaType(mediaTypeOut.put());
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    mediaTypeOut->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
    mediaTypeOut->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_H264);
    mediaTypeOut->SetUINT32(MF_MT_AVG_BITRATE, 5000000);
    mediaTypeOut->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
    MFSetAttributeSize(mediaTypeOut.get(), MF_MT_FRAME_SIZE, width, height);
    MFSetAttributeRatio(mediaTypeOut.get(), MF_MT_FRAME_RATE, 30, 1);
    MFSetAttributeRatio(mediaTypeOut.get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1);

    DWORD streamIndex = 0;
    hr = sinkWriter->AddStream(mediaTypeOut.get(), &streamIndex);
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    // Input type: RGB32
    wil::com_ptr<IMFMediaType> mediaTypeIn;
    hr = MFCreateMediaType(mediaTypeIn.put());
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }
    
    mediaTypeIn->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
    mediaTypeIn->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_RGB32);
    MFSetAttributeSize(mediaTypeIn.get(), MF_MT_FRAME_SIZE, width, height);
    MFSetAttributeRatio(mediaTypeIn.get(), MF_MT_FRAME_RATE, 30, 1);
    MFSetAttributeRatio(mediaTypeIn.get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1);

    LONG defaultStride = static_cast<LONG>(width * 4);
    mediaTypeIn->SetUINT32(MF_MT_DEFAULT_STRIDE, static_cast<UINT32>(defaultStride));

    hr = sinkWriter->SetInputMediaType(streamIndex, mediaTypeIn.get(), nullptr);
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

    // Detect audio format type before storing
    bool isFloatFormat = false;
    
    if (audioFormat->wFormatTag == WAVE_FORMAT_IEEE_FLOAT)
    {
        isFloatFormat = true;
    }
    else if (audioFormat->wFormatTag == WAVE_FORMAT_EXTENSIBLE)
    {
        WAVEFORMATEXTENSIBLE* pFormatEx = reinterpret_cast<WAVEFORMATEXTENSIBLE*>(audioFormat);
        if (IsEqualGUID(pFormatEx->SubFormat, KSDATAFORMAT_SUBTYPE_IEEE_FLOAT))
        {
            isFloatFormat = true;
        }
    }
    
    memcpy(&m_audioFormat, audioFormat, sizeof(WAVEFORMATEX));

    // Configure output audio stream: AAC at 160 kbps
    const UINT32 AAC_BITRATE = 20000;
    wil::com_ptr<IMFMediaType> mediaTypeOut;
    HRESULT hr = MFCreateMediaType(mediaTypeOut.put());
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    mediaTypeOut->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
    mediaTypeOut->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_AAC);
    mediaTypeOut->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, audioFormat->nSamplesPerSec);
    mediaTypeOut->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, audioFormat->nChannels);
    mediaTypeOut->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, AAC_BITRATE);
    mediaTypeOut->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 16);

    DWORD audioStreamIndex = 0;
    hr = m_sinkWriter->AddStream(mediaTypeOut.get(), &audioStreamIndex);
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    // Configure input audio format
    wil::com_ptr<IMFMediaType> mediaTypeIn;
    hr = MFCreateMediaType(mediaTypeIn.put());
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    mediaTypeIn->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
    
    if (isFloatFormat)
    {
        mediaTypeIn->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_Float);
    }
    else
    {
        mediaTypeIn->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_PCM);
    }
    
    mediaTypeIn->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, audioFormat->nSamplesPerSec);
    mediaTypeIn->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, audioFormat->nChannels);
    mediaTypeIn->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, audioFormat->wBitsPerSample);
    mediaTypeIn->SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, audioFormat->nBlockAlign);
    mediaTypeIn->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, audioFormat->nAvgBytesPerSec);

    hr = m_sinkWriter->SetInputMediaType(audioStreamIndex, mediaTypeIn.get(), nullptr);
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

    D3D11_TEXTURE2D_DESC desc{};
    texture->GetDesc(&desc);

    // Create staging texture only once and reuse it to prevent memory leak
    if (!m_stagingTexture)
    {
        D3D11_TEXTURE2D_DESC stagingDesc = desc;
        stagingDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
        stagingDesc.Usage = D3D11_USAGE_STAGING;
        stagingDesc.BindFlags = 0;
        stagingDesc.MiscFlags = 0;

        HRESULT hr = m_device->CreateTexture2D(&stagingDesc, nullptr, m_stagingTexture.put());
        if (FAILED(hr)) return hr;
    }

    m_context->CopyResource(m_stagingTexture.get(), texture);

    D3D11_MAPPED_SUBRESOURCE mapped{};
    HRESULT hr = m_context->Map(m_stagingTexture.get(), 0, D3D11_MAP_READ, 0, &mapped);
    if (FAILED(hr)) 
    {
        return hr;
    }

    const UINT32 bytesPerPixel = 4;
    const UINT32 canonicalStride = m_width * bytesPerPixel;
    const UINT32 bufferSize = canonicalStride * m_height;

    wil::com_ptr<IMFMediaBuffer> buffer;
    hr = MFCreateMemoryBuffer(bufferSize, buffer.put());
    if (FAILED(hr)) 
    { 
        m_context->Unmap(m_stagingTexture.get(), 0); 
        return hr; 
    }

    BYTE* pData = nullptr;
    DWORD maxLen = 0, curLen = 0;
    hr = buffer->Lock(&pData, &maxLen, &curLen);
    if (FAILED(hr)) 
    { 
        m_context->Unmap(m_stagingTexture.get(), 0); 
        return hr; 
    }

    for (UINT row = 0; row < m_height; ++row)
    {
        BYTE* destRow = pData + row * canonicalStride;
        BYTE* srcRow = (BYTE*)mapped.pData + row * mapped.RowPitch;
        memcpy(destRow, srcRow, canonicalStride);
    }

    buffer->SetCurrentLength(bufferSize);
    buffer->Unlock();
    
    m_context->Unmap(m_stagingTexture.get(), 0);

    wil::com_ptr<IMFSample> sample;
    hr = MFCreateSample(sample.put());
    if (FAILED(hr)) return hr;

    sample->AddBuffer(buffer.get());
    sample->SetSampleTime(relativeTicks);

    if (m_prevVideoTimestamp == 0)
        m_prevVideoTimestamp = relativeTicks;

    const LONGLONG frameDuration = MediaTimeConstants::TicksPerSecond() / 30;

    LONGLONG duration = relativeTicks - m_prevVideoTimestamp;
    if (duration <= 0) duration = frameDuration;
    sample->SetSampleDuration(duration);
    m_prevVideoTimestamp = relativeTicks;

    m_frameIndex++;
    return m_sinkWriter->WriteSample(m_videoStreamIndex, sample.get());
}

long WindowsMFMP4SinkWriter::WriteAudioSample(std::span<const uint8_t> data, int64_t timestamp)
{
    if (!m_sinkWriter || !m_hasAudioStream || data.empty())
    {
        return E_FAIL;
    }

    UINT32 bufferSize = static_cast<UINT32>(data.size());

    wil::com_ptr<IMFMediaBuffer> buffer;
    HRESULT hr = MFCreateMemoryBuffer(bufferSize, buffer.put());
    if (FAILED(hr)) return hr;

    BYTE* pBufferData = nullptr;
    DWORD maxLen = 0, curLen = 0;
    hr = buffer->Lock(&pBufferData, &maxLen, &curLen);
    if (FAILED(hr)) return hr;

    memcpy(pBufferData, data.data(), bufferSize);
    buffer->SetCurrentLength(bufferSize);
    buffer->Unlock();

    wil::com_ptr<IMFSample> sample;
    hr = MFCreateSample(sample.put());
    if (FAILED(hr)) return hr;

    sample->AddBuffer(buffer.get());
    sample->SetSampleTime(timestamp);

    // Calculate number of frames from buffer size
    UINT32 numFrames = bufferSize / m_audioFormat.nBlockAlign;
    LONGLONG duration = MediaTimeConstants::TicksFromAudioFrames(numFrames, m_audioFormat.nSamplesPerSec);
    
    sample->SetSampleDuration(duration);

    return m_sinkWriter->WriteSample(m_audioStreamIndex, sample.get());
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

    // Release cached staging texture
    m_stagingTexture.reset();

    // Smart pointers clean up automatically
    m_context.reset();
    m_device.reset();

    MFShutdown();
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
