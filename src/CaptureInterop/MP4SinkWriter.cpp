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
    MFCreateMediaType(mediaTypeOut.put());
    mediaTypeOut->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
    mediaTypeOut->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_H264);
    mediaTypeOut->SetUINT32(MF_MT_AVG_BITRATE, 5000000);
    mediaTypeOut->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
    MFSetAttributeSize(mediaTypeOut.get(), MF_MT_FRAME_SIZE, width, height);
    MFSetAttributeRatio(mediaTypeOut.get(), MF_MT_FRAME_RATE, 30, 1);
    MFSetAttributeRatio(mediaTypeOut.get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1);

    DWORD streamIndex;
    sinkWriter->AddStream(mediaTypeOut.get(), &streamIndex);

    // Input type: RGB32
    wil::com_ptr<IMFMediaType> mediaTypeIn;
    MFCreateMediaType(mediaTypeIn.put());
    mediaTypeIn->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
    mediaTypeIn->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_RGB32);
    MFSetAttributeSize(mediaTypeIn.get(), MF_MT_FRAME_SIZE, width, height);
    MFSetAttributeRatio(mediaTypeIn.get(), MF_MT_FRAME_RATE, 30, 1);
    MFSetAttributeRatio(mediaTypeIn.get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1);

    sinkWriter->SetInputMediaType(streamIndex, mediaTypeIn.get(), nullptr);
    sinkWriter->BeginWriting();

    m_sinkWriter = std::move(sinkWriter);
    m_streamIndex = streamIndex;

    return true;
}

HRESULT MP4SinkWriter::WriteFrame(ID3D11Texture2D* texture)
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

    wil::com_ptr<IMFMediaBuffer> buffer;
    hr = MFCreateMemoryBuffer(mapped.RowPitch * m_height, buffer.put());
    if (FAILED(hr)) { m_context->Unmap(staging.get(), 0); return hr; }

    BYTE* pData = nullptr;
    DWORD maxLen = 0, curLen = 0;
    buffer->Lock(&pData, &maxLen, &curLen);

    for (UINT row = 0; row < m_height; ++row)
    {
        memcpy(pData + row * mapped.RowPitch,
            (BYTE*)mapped.pData + row * mapped.RowPitch,
            mapped.RowPitch);
    }

    buffer->SetCurrentLength(mapped.RowPitch * m_height);
    buffer->Unlock();
    m_context->Unmap(staging.get(), 0);

    wil::com_ptr<IMFSample> sample;
    MFCreateSample(sample.put());
    sample->AddBuffer(buffer.get());

    LONGLONG rtStart = m_frameIndex * 333333; // 30fps
    sample->SetSampleTime(rtStart);
    sample->SetSampleDuration(333333);
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
