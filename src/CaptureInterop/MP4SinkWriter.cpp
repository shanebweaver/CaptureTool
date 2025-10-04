#include "pch.h"
#include "MP4SinkWriter.h"

MP4SinkWriter::MP4SinkWriter() = default;

MP4SinkWriter::~MP4SinkWriter()
{
    Finalize();
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

    hr = sinkWriter->BeginWriting();
    if (FAILED(hr)) { if (outHr) *outHr = hr; return false; }

    m_sinkWriter = std::move(sinkWriter);
    m_streamIndex = streamIndex;

    return true;
}

HRESULT MP4SinkWriter::WriteFrame(ID3D11Texture2D* texture, LONGLONG relativeTicks)
{
    if (!texture || !m_sinkWriter) return E_FAIL;

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

    if (m_prevTimestamp == 0)
        m_prevTimestamp = relativeTicks;

    const LONGLONG TICKS_PER_SECOND = 10000000LL;
    const LONGLONG frameDuration = TICKS_PER_SECOND / 30; // 30 FPS

    LONGLONG duration = relativeTicks - m_prevTimestamp;
    if (duration <= 0) duration = frameDuration; // fallback ~30 fps
    sample->SetSampleDuration(duration);
    m_prevTimestamp = relativeTicks;

    m_frameIndex++;
    return m_sinkWriter->WriteSample(m_streamIndex, sample.get());
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
