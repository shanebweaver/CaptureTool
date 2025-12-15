#include "pch.h"
#include "MP4SinkWriter.h"

MP4SinkWriter::MP4SinkWriter() = default;

MP4SinkWriter::~MP4SinkWriter()
{
    Finalize();
}

void MP4SinkWriter::SetRecordingStartTime(LONGLONG qpcStart)
{
    m_recordingStartQpc = qpcStart;
}

bool MP4SinkWriter::Initialize(const wchar_t* outputPath, ID3D11Device* device, UINT32 width, UINT32 height, HRESULT* outHr)
{
    if (outHr) *outHr = S_OK;
    m_device = device;
    m_device->AddRef();

    m_device->GetImmediateContext(&m_context);
    m_context->AddRef();

    m_width = width;
    m_height = height;
    m_frameIndex = 0;

    HRESULT hr = MFStartup(MF_VERSION);
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    wil::com_ptr<IMFSinkWriter> sinkWriter;
    hr = MFCreateSinkWriterFromURL(outputPath, nullptr, nullptr, sinkWriter.put());
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

    // Don't call BeginWriting yet - caller may want to add audio stream first
    // If no audio stream is added, WriteFrame will call BeginWriting on first frame

    return true;
}

bool MP4SinkWriter::InitializeAudioStream(WAVEFORMATEX* audioFormat, HRESULT* outHr)
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

    // Store audio format for later use
    // Note: We only copy the base WAVEFORMATEX structure, not extended format data.
    // This is intentional - m_audioFormat is WAVEFORMATEX (not WAVEFORMATEXTENSIBLE),
    // and we only need the basic format info (sample rate, channels, bits per sample).
    // Media Foundation handles any extended format conversion automatically.
    memcpy(&m_audioFormat, audioFormat, sizeof(WAVEFORMATEX));

    // Output type: AAC at 160 kbps
    const UINT32 AAC_BITRATE = 20000; // bytes per second (160000 bits per second / 8)
    wil::com_ptr<IMFMediaType> mediaTypeOut;
    HRESULT hr = MFCreateMediaType(mediaTypeOut.put());
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    mediaTypeOut->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
    mediaTypeOut->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_AAC);
    mediaTypeOut->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, audioFormat->nSamplesPerSec);
    mediaTypeOut->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, audioFormat->nChannels);
    mediaTypeOut->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, AAC_BITRATE);
    // AAC output is always 16-bit; Media Foundation handles conversion from input format
    mediaTypeOut->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 16);

    DWORD audioStreamIndex = 0;
    hr = m_sinkWriter->AddStream(mediaTypeOut.get(), &audioStreamIndex);
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    // Input type: PCM or Float (from WASAPI)
    wil::com_ptr<IMFMediaType> mediaTypeIn;
    hr = MFCreateMediaType(mediaTypeIn.put());
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    mediaTypeIn->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
    
    // Check if audio format is float or PCM
    // WASAPI often returns float format, which needs to be specified correctly
    bool isFloatFormat = false;
    
    if (audioFormat->wFormatTag == WAVE_FORMAT_IEEE_FLOAT)
    {
        // Direct float format
        isFloatFormat = true;
    }
    else if (audioFormat->wFormatTag == WAVE_FORMAT_EXTENSIBLE)
    {
        // Extended format - check the SubFormat GUID
        WAVEFORMATEXTENSIBLE* pFormatEx = reinterpret_cast<WAVEFORMATEXTENSIBLE*>(audioFormat);
        if (IsEqualGUID(pFormatEx->SubFormat, KSDATAFORMAT_SUBTYPE_IEEE_FLOAT))
        {
            isFloatFormat = true;
        }
    }
    
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

    // Now that all streams are added, begin writing
    hr = m_sinkWriter->BeginWriting();
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }
    m_hasBegunWriting = true;

    if (outHr) *outHr = S_OK;
    return true;
}

HRESULT MP4SinkWriter::WriteFrame(ID3D11Texture2D* texture, LONGLONG relativeTicks)
{
    if (!texture || !m_sinkWriter) return E_FAIL;

    // If we don't have audio and haven't started writing yet, begin now
    if (!m_hasBegunWriting && !m_hasAudioStream)
    {
        HRESULT hr = m_sinkWriter->BeginWriting();
        if (FAILED(hr)) return hr;
        m_hasBegunWriting = true;
    }

    // Copy to staging for CPU read
    D3D11_TEXTURE2D_DESC desc{};
    texture->GetDesc(&desc);

    D3D11_TEXTURE2D_DESC stagingDesc = desc;
    stagingDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    stagingDesc.Usage = D3D11_USAGE_STAGING;
    stagingDesc.BindFlags = 0;
    stagingDesc.MiscFlags = 0;

    wil::com_ptr<ID3D11Texture2D> staging;
    HRESULT hr = m_device->CreateTexture2D(&stagingDesc, nullptr, staging.put());
    if (FAILED(hr)) return hr;

    m_context->CopyResource(staging.get(), texture);

    D3D11_MAPPED_SUBRESOURCE mapped{};
    hr = m_context->Map(staging.get(), 0, D3D11_MAP_READ, 0, &mapped);
    if (FAILED(hr)) return hr;

    const UINT32 bytesPerPixel = 4;
    const UINT32 canonicalStride = m_width * bytesPerPixel;
    const UINT32 bufferSize = canonicalStride * m_height;

    wil::com_ptr<IMFMediaBuffer> buffer;
    hr = MFCreateMemoryBuffer(mapped.RowPitch * m_height, buffer.put());
    if (FAILED(hr)) { m_context->Unmap(staging.get(), 0); return hr; }

    BYTE* pData = nullptr;
    DWORD maxLen = 0, curLen = 0;
    hr = buffer->Lock(&pData, &maxLen, &curLen);
    if (FAILED(hr)) { m_context->Unmap(staging.get(), 0); return hr; }

    for (UINT row = 0; row < m_height; ++row)
    {
        BYTE* destRow = pData + row * canonicalStride;
        BYTE* srcRow = (BYTE*)mapped.pData + row * mapped.RowPitch;
        memcpy(destRow, srcRow, canonicalStride);
    }

    buffer->SetCurrentLength(bufferSize);
    buffer->Unlock();
    m_context->Unmap(staging.get(), 0);

    wil::com_ptr<IMFSample> sample;
    hr = MFCreateSample(sample.put());
    if (FAILED(hr)) return hr;

    sample->AddBuffer(buffer.get());
    sample->SetSampleTime(relativeTicks);

    if (m_prevVideoTimestamp == 0)
        m_prevVideoTimestamp = relativeTicks;

    const LONGLONG TICKS_PER_SECOND = 10000000LL;
    const LONGLONG frameDuration = TICKS_PER_SECOND / 30; // 30 FPS

    LONGLONG duration = relativeTicks - m_prevVideoTimestamp;
    if (duration <= 0) duration = frameDuration; // fallback ~30 fps
    sample->SetSampleDuration(duration);
    m_prevVideoTimestamp = relativeTicks;

    m_frameIndex++;
    return m_sinkWriter->WriteSample(m_videoStreamIndex, sample.get());
}

HRESULT MP4SinkWriter::WriteAudioSample(const BYTE* pData, UINT32 numFrames, LONGLONG timestamp)
{
    if (!m_sinkWriter || !m_hasAudioStream || !pData || numFrames == 0)
    {
        return E_FAIL;
    }

    // Calculate buffer size
    UINT32 bufferSize = numFrames * m_audioFormat.nBlockAlign;

    // Create media buffer
    wil::com_ptr<IMFMediaBuffer> buffer;
    HRESULT hr = MFCreateMemoryBuffer(bufferSize, buffer.put());
    if (FAILED(hr)) return hr;

    // Copy audio data to buffer
    BYTE* pBufferData = nullptr;
    DWORD maxLen = 0, curLen = 0;
    hr = buffer->Lock(&pBufferData, &maxLen, &curLen);
    if (FAILED(hr)) return hr;

    memcpy(pBufferData, pData, bufferSize);
    buffer->SetCurrentLength(bufferSize);
    buffer->Unlock();

    // Create sample
    wil::com_ptr<IMFSample> sample;
    hr = MFCreateSample(sample.put());
    if (FAILED(hr)) return hr;

    sample->AddBuffer(buffer.get());
    sample->SetSampleTime(timestamp);

    // Calculate duration based on number of frames and sample rate
    const LONGLONG TICKS_PER_SECOND = 10000000LL;
    LONGLONG duration = (numFrames * TICKS_PER_SECOND) / m_audioFormat.nSamplesPerSec;
    
    if (m_prevAudioTimestamp > 0)
    {
        LONGLONG calculatedDuration = timestamp - m_prevAudioTimestamp;
        // Use calculated duration if it's positive and less than 1 second
        // (reject durations >= 1 second as they indicate timestamp discontinuity)
        if (calculatedDuration > 0 && calculatedDuration < TICKS_PER_SECOND)
        {
            duration = calculatedDuration;
        }
    }
    
    sample->SetSampleDuration(duration);
    m_prevAudioTimestamp = timestamp;

    return m_sinkWriter->WriteSample(m_audioStreamIndex, sample.get());
}

void MP4SinkWriter::Finalize()
{
    if (m_sinkWriter)
    {
        m_sinkWriter->Finalize();
        m_sinkWriter.reset();
    }

    if (m_context)
    {
        m_context->Release();
        m_context = nullptr;
    }

    if (m_device)
    {
        m_device->Release();
        m_device = nullptr;
    }

    MFShutdown();
}

ULONG STDMETHODCALLTYPE MP4SinkWriter::AddRef()
{
    return InterlockedIncrement(&m_ref);
}

ULONG STDMETHODCALLTYPE MP4SinkWriter::Release()
{
    ULONG ref = InterlockedDecrement(&m_ref);
    if (ref == 0)
    {
        delete this;
    }
    return ref;
}
