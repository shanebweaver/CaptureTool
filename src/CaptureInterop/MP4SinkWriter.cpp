#include "pch.h"
#include "MP4SinkWriter.h"

MP4SinkWriter::MP4SinkWriter() = default;

MP4SinkWriter::~MP4SinkWriter()
{
    Finalize();
}

bool MP4SinkWriter::Initialize(const wchar_t* outputPath, ID3D11Device* device, UINT32 width, UINT32 height, bool enableAudio, WAVEFORMATEX* audioFormat, HRESULT* outHr)
{
    if (outHr) *outHr = S_OK;
    m_device = device;
    m_device->AddRef();

    m_device->GetImmediateContext(&m_context);
    m_context->AddRef();

    m_width = width;
    m_height = height;
    m_frameIndex = 0;
    m_hasAudio = enableAudio && audioFormat != nullptr;

    HRESULT hr = MFStartup(MF_VERSION);
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    wil::com_ptr<IMFSinkWriter> sinkWriter;
    hr = MFCreateSinkWriterFromURL(outputPath, nullptr, nullptr, sinkWriter.put());
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    // Output type: H.264 video
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

    DWORD videoStreamIndex = 0;
    hr = sinkWriter->AddStream(mediaTypeOut.get(), &videoStreamIndex);
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    // Input type: RGB32 video
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

    hr = sinkWriter->SetInputMediaType(videoStreamIndex, mediaTypeIn.get(), nullptr);
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    m_videoStreamIndex = videoStreamIndex;

    // Add audio stream if enabled
    if (m_hasAudio)
    {
        // Output type: AAC audio
        wil::com_ptr<IMFMediaType> audioTypeOut;
        hr = MFCreateMediaType(audioTypeOut.put());
        if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

        constexpr UINT32 AUDIO_BITRATE_BPS = 24000; // 192 kbps for AAC
        audioTypeOut->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
        audioTypeOut->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_AAC);
        audioTypeOut->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, audioFormat->nSamplesPerSec);
        audioTypeOut->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, audioFormat->nChannels);
        audioTypeOut->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
        audioTypeOut->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, AUDIO_BITRATE_BPS);
        audioTypeOut->SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, 1);

        DWORD audioStreamIndex = 0;
        hr = sinkWriter->AddStream(audioTypeOut.get(), &audioStreamIndex);
        if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

        // Input type: PCM audio (16-bit)
        // Note: We convert float audio to 16-bit PCM in AudioCaptureManager if needed
        wil::com_ptr<IMFMediaType> audioTypeIn;
        hr = MFCreateMediaType(audioTypeIn.put());
        if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

        audioTypeIn->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
        audioTypeIn->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_PCM);
        audioTypeIn->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, audioFormat->nSamplesPerSec);
        audioTypeIn->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, audioFormat->nChannels);
        audioTypeIn->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 16); // Always 16-bit PCM
        audioTypeIn->SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, audioFormat->nChannels * 2); // 2 bytes per sample
        audioTypeIn->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, audioFormat->nSamplesPerSec * audioFormat->nChannels * 2);

        hr = sinkWriter->SetInputMediaType(audioStreamIndex, audioTypeIn.get(), nullptr);
        if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

        m_audioStreamIndex = audioStreamIndex;
    }

    hr = sinkWriter->BeginWriting();
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    m_sinkWriter = std::move(sinkWriter);

    return true;
}

HRESULT MP4SinkWriter::WriteFrame(ID3D11Texture2D* texture, LONGLONG relativeTicks)
{
    if (!texture || !m_sinkWriter) return E_FAIL;

    std::lock_guard<std::mutex> lock(m_writeMutex);

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

HRESULT MP4SinkWriter::WriteAudioSample(BYTE* data, UINT32 dataSize, LONGLONG relativeTicks)
{
    if (!m_sinkWriter || !m_hasAudio || !data || dataSize == 0) return E_FAIL;

    // Use try_lock to avoid blocking the audio capture thread
    // If we can't get the lock immediately, skip this sample to prevent UI freezes
    std::unique_lock<std::mutex> lock(m_writeMutex, std::try_to_lock);
    if (!lock.owns_lock())
    {
        // Mutex is busy (video write in progress), skip this audio sample
        // Missing a few audio samples is better than freezing the UI
        return S_OK; // Return success to avoid error propagation
    }

    HRESULT hr;
    wil::com_ptr<IMFMediaBuffer> buffer;
    hr = MFCreateMemoryBuffer(dataSize, buffer.put());
    if (FAILED(hr)) return hr;

    BYTE* pData = nullptr;
    DWORD maxLen = 0, curLen = 0;
    hr = buffer->Lock(&pData, &maxLen, &curLen);
    if (FAILED(hr)) return hr;

    memcpy(pData, data, dataSize);
    buffer->SetCurrentLength(dataSize);
    buffer->Unlock();

    wil::com_ptr<IMFSample> sample;
    hr = MFCreateSample(sample.put());
    if (FAILED(hr)) return hr;

    sample->AddBuffer(buffer.get());
    sample->SetSampleTime(relativeTicks);

    // Calculate duration based on sample size and format
    // This will be properly calculated by Media Foundation based on the format
    if (m_prevAudioTimestamp > 0)
    {
        LONGLONG duration = relativeTicks - m_prevAudioTimestamp;
        if (duration > 0)
        {
            sample->SetSampleDuration(duration);
        }
    }
    m_prevAudioTimestamp = relativeTicks;

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
