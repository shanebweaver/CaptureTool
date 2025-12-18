#include "pch.h"
#include "TextureConverter.h"
#include <chrono>

using namespace CaptureInterop;

TextureConverter::TextureConverter()
    : m_pDevice(nullptr)
    , m_pContext(nullptr)
    , m_pVideoDevice(nullptr)
    , m_pVideoContext(nullptr)
    , m_pVideoProcessor(nullptr)
    , m_pVideoProcessorEnum(nullptr)
    , m_pStagingTexture(nullptr)
    , m_pNV12Texture(nullptr)
    , m_pInputView(nullptr)
    , m_pOutputView(nullptr)
    , m_width(0)
    , m_height(0)
    , m_initialized(false)
    , m_conversionCount(0)
    , m_totalConversionTimeMs(0.0)
{
}

TextureConverter::~TextureConverter()
{
    if (m_pOutputView)
    {
        m_pOutputView->Release();
        m_pOutputView = nullptr;
    }

    if (m_pInputView)
    {
        m_pInputView->Release();
        m_pInputView = nullptr;
    }

    if (m_pNV12Texture)
    {
        m_pNV12Texture->Release();
        m_pNV12Texture = nullptr;
    }

    if (m_pStagingTexture)
    {
        m_pStagingTexture->Release();
        m_pStagingTexture = nullptr;
    }

    if (m_pVideoProcessor)
    {
        m_pVideoProcessor->Release();
        m_pVideoProcessor = nullptr;
    }

    if (m_pVideoProcessorEnum)
    {
        m_pVideoProcessorEnum->Release();
        m_pVideoProcessorEnum = nullptr;
    }

    if (m_pVideoContext)
    {
        m_pVideoContext->Release();
        m_pVideoContext = nullptr;
    }

    if (m_pVideoDevice)
    {
        m_pVideoDevice->Release();
        m_pVideoDevice = nullptr;
    }

    if (m_pContext)
    {
        m_pContext->Release();
        m_pContext = nullptr;
    }

    if (m_pDevice)
    {
        m_pDevice->Release();
        m_pDevice = nullptr;
    }
}

HRESULT TextureConverter::Initialize(ID3D11Device* pDevice, UINT32 width, UINT32 height)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    if (m_initialized)
    {
        return S_OK;
    }

    if (!pDevice || width == 0 || height == 0)
    {
        return E_INVALIDARG;
    }

    m_pDevice = pDevice;
    m_pDevice->AddRef();

    m_pDevice->GetImmediateContext(&m_pContext);
    if (!m_pContext)
    {
        return E_FAIL;
    }

    m_width = width;
    m_height = height;

    // Get video device for hardware video processing
    HRESULT hr = m_pDevice->QueryInterface(__uuidof(ID3D11VideoDevice), (void**)&m_pVideoDevice);
    if (FAILED(hr))
    {
        return hr;
    }

    hr = m_pContext->QueryInterface(__uuidof(ID3D11VideoContext), (void**)&m_pVideoContext);
    if (FAILED(hr))
    {
        return hr;
    }

    // Create video processor for format conversion
    hr = CreateVideoProcessor();
    if (FAILED(hr))
    {
        return hr;
    }

    // Create staging and NV12 textures
    hr = CreateStagingTextures();
    if (FAILED(hr))
    {
        return hr;
    }

    hr = CreateNV12Texture();
    if (FAILED(hr))
    {
        return hr;
    }

    m_initialized = true;
    return S_OK;
}

HRESULT TextureConverter::CreateVideoProcessor()
{
    // Describe the video processor content
    D3D11_VIDEO_PROCESSOR_CONTENT_DESC contentDesc = {};
    contentDesc.InputFrameFormat = D3D11_VIDEO_FRAME_FORMAT_PROGRESSIVE;
    contentDesc.InputWidth = m_width;
    contentDesc.InputHeight = m_height;
    contentDesc.OutputWidth = m_width;
    contentDesc.OutputHeight = m_height;
    contentDesc.Usage = D3D11_VIDEO_USAGE_PLAYBACK_NORMAL;

    // Create video processor enumerator
    HRESULT hr = m_pVideoDevice->CreateVideoProcessorEnumerator(
        &contentDesc,
        &m_pVideoProcessorEnum);
    if (FAILED(hr))
    {
        return hr;
    }

    // Create video processor
    hr = m_pVideoDevice->CreateVideoProcessor(
        m_pVideoProcessorEnum,
        0,  // Use first (default) processor
        &m_pVideoProcessor);
    if (FAILED(hr))
    {
        return hr;
    }

    return S_OK;
}

HRESULT TextureConverter::CreateStagingTextures()
{
    // Create staging texture for CPU readback (if needed for fallback)
    D3D11_TEXTURE2D_DESC stagingDesc = {};
    stagingDesc.Width = m_width;
    stagingDesc.Height = m_height;
    stagingDesc.MipLevels = 1;
    stagingDesc.ArraySize = 1;
    stagingDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    stagingDesc.SampleDesc.Count = 1;
    stagingDesc.Usage = D3D11_USAGE_STAGING;
    stagingDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;

    HRESULT hr = m_pDevice->CreateTexture2D(&stagingDesc, nullptr, &m_pStagingTexture);
    if (FAILED(hr))
    {
        return hr;
    }

    return S_OK;
}

HRESULT TextureConverter::CreateNV12Texture()
{
    // Create NV12 texture for video processor output
    D3D11_TEXTURE2D_DESC nv12Desc = {};
    nv12Desc.Width = m_width;
    nv12Desc.Height = m_height;
    nv12Desc.MipLevels = 1;
    nv12Desc.ArraySize = 1;
    nv12Desc.Format = DXGI_FORMAT_NV12;
    nv12Desc.SampleDesc.Count = 1;
    nv12Desc.Usage = D3D11_USAGE_DEFAULT;
    nv12Desc.BindFlags = D3D11_BIND_RENDER_TARGET;

    HRESULT hr = m_pDevice->CreateTexture2D(&nv12Desc, nullptr, &m_pNV12Texture);
    if (FAILED(hr))
    {
        return hr;
    }

    // Create input view for BGRA texture
    D3D11_VIDEO_PROCESSOR_INPUT_VIEW_DESC inputViewDesc = {};
    inputViewDesc.FourCC = 0;
    inputViewDesc.ViewDimension = D3D11_VPIV_DIMENSION_TEXTURE2D;
    inputViewDesc.Texture2D.MipSlice = 0;
    inputViewDesc.Texture2D.ArraySlice = 0;

    // Note: Input view will be created per frame with source texture

    // Create output view for NV12 texture
    D3D11_VIDEO_PROCESSOR_OUTPUT_VIEW_DESC outputViewDesc = {};
    outputViewDesc.ViewDimension = D3D11_VPOV_DIMENSION_TEXTURE2D;
    outputViewDesc.Texture2D.MipSlice = 0;

    hr = m_pVideoDevice->CreateVideoProcessorOutputView(
        m_pNV12Texture,
        m_pVideoProcessorEnum,
        &outputViewDesc,
        &m_pOutputView);
    if (FAILED(hr))
    {
        return hr;
    }

    return S_OK;
}

HRESULT TextureConverter::ConvertBGRAToNV12(ID3D11Texture2D* pSource, ID3D11Texture2D* pDest)
{
    if (!pSource || !pDest)
    {
        return E_INVALIDARG;
    }

    // Create input view for source texture
    ID3D11VideoProcessorInputView* pTempInputView = nullptr;

    D3D11_VIDEO_PROCESSOR_INPUT_VIEW_DESC inputViewDesc = {};
    inputViewDesc.FourCC = 0;
    inputViewDesc.ViewDimension = D3D11_VPIV_DIMENSION_TEXTURE2D;
    inputViewDesc.Texture2D.MipSlice = 0;
    inputViewDesc.Texture2D.ArraySlice = 0;

    HRESULT hr = m_pVideoDevice->CreateVideoProcessorInputView(
        pSource,
        m_pVideoProcessorEnum,
        &inputViewDesc,
        &pTempInputView);
    if (FAILED(hr))
    {
        return hr;
    }

    // Set up video processor stream
    D3D11_VIDEO_PROCESSOR_STREAM stream = {};
    stream.Enable = TRUE;
    stream.OutputIndex = 0;
    stream.InputFrameOrField = 0;
    stream.PastFrames = 0;
    stream.FutureFrames = 0;
    stream.ppPastSurfaces = nullptr;
    stream.ppFutureSurfaces = nullptr;
    stream.pInputSurface = pTempInputView;
    stream.ppPastSurfacesRight = nullptr;
    stream.ppFutureSurfacesRight = nullptr;

    // Process the frame (BGRA → NV12 conversion)
    hr = m_pVideoContext->VideoProcessorBlt(
        m_pVideoProcessor,
        m_pOutputView,
        0,  // Output frame
        1,  // Stream count
        &stream);

    pTempInputView->Release();

    return hr;
}

HRESULT TextureConverter::CreateMFSampleFromTexture(ID3D11Texture2D* pTexture, int64_t timestamp, IMFSample** ppSample)
{
    if (!pTexture || !ppSample)
    {
        return E_INVALIDARG;
    }

    *ppSample = nullptr;

    // Get texture description
    D3D11_TEXTURE2D_DESC desc;
    pTexture->GetDesc(&desc);

    // Calculate buffer size for NV12 format
    // NV12 is Y plane (width * height) + UV plane (width * height / 2)
    UINT32 bufferSize = desc.Width * desc.Height * 3 / 2;

    // Create Media Foundation sample
    IMFSample* pSample = nullptr;
    HRESULT hr = MFCreateSample(&pSample);
    if (FAILED(hr))
    {
        return hr;
    }

    // Create Media Foundation media buffer
    IMFMediaBuffer* pBuffer = nullptr;
    hr = MFCreateMemoryBuffer(bufferSize, &pBuffer);
    if (FAILED(hr))
    {
        pSample->Release();
        return hr;
    }

    // Copy texture data to buffer
    hr = CopyTextureToMFSample(pTexture, pSample);
    if (FAILED(hr))
    {
        pBuffer->Release();
        pSample->Release();
        return hr;
    }

    // Set sample timestamp and duration
    hr = pSample->SetSampleTime(timestamp);
    if (FAILED(hr))
    {
        pBuffer->Release();
        pSample->Release();
        return hr;
    }

    // Set duration (assuming 30fps, ~333,333 100-ns units)
    hr = pSample->SetSampleDuration(333333);
    if (FAILED(hr))
    {
        pBuffer->Release();
        pSample->Release();
        return hr;
    }

    // Add buffer to sample
    hr = pSample->AddBuffer(pBuffer);
    pBuffer->Release();
    if (FAILED(hr))
    {
        pSample->Release();
        return hr;
    }

    *ppSample = pSample;
    return S_OK;
}

HRESULT TextureConverter::CopyTextureToMFSample(ID3D11Texture2D* pTexture, IMFSample* pSample)
{
    if (!pTexture || !pSample)
    {
        return E_INVALIDARG;
    }

    // Copy texture to staging texture for CPU access
    m_pContext->CopyResource(m_pStagingTexture, pTexture);

    // Map the staging texture
    D3D11_MAPPED_SUBRESOURCE mapped;
    HRESULT hr = m_pContext->Map(m_pStagingTexture, 0, D3D11_MAP_READ, 0, &mapped);
    if (FAILED(hr))
    {
        return hr;
    }

    // Get buffer from sample
    IMFMediaBuffer* pBuffer = nullptr;
    hr = pSample->GetBufferByIndex(0, &pBuffer);
    if (FAILED(hr))
    {
        m_pContext->Unmap(m_pStagingTexture, 0);
        return hr;
    }

    // Lock buffer
    BYTE* pData = nullptr;
    DWORD maxLength = 0;
    DWORD currentLength = 0;
    hr = pBuffer->Lock(&pData, &maxLength, &currentLength);
    if (FAILED(hr))
    {
        pBuffer->Release();
        m_pContext->Unmap(m_pStagingTexture, 0);
        return hr;
    }

    // Copy Y plane (luminance)
    BYTE* pSrc = (BYTE*)mapped.pData;
    BYTE* pDest = pData;
    for (UINT32 y = 0; y < m_height; y++)
    {
        memcpy(pDest, pSrc, m_width);
        pSrc += mapped.RowPitch;
        pDest += m_width;
    }

    // Copy UV plane (chrominance)
    // NV12 has interleaved U and V in half resolution
    for (UINT32 y = 0; y < m_height / 2; y++)
    {
        memcpy(pDest, pSrc, m_width);
        pSrc += mapped.RowPitch;
        pDest += m_width;
    }

    // Set buffer length
    hr = pBuffer->SetCurrentLength(m_width * m_height * 3 / 2);

    pBuffer->Unlock();
    pBuffer->Release();
    m_pContext->Unmap(m_pStagingTexture, 0);

    return hr;
}

HRESULT TextureConverter::ConvertTextureToSample(ID3D11Texture2D* pTexture, int64_t timestamp, IMFSample** ppSample)
{
    if (!m_initialized)
    {
        return E_NOT_VALID_STATE;
    }

    if (!pTexture || !ppSample)
    {
        return E_INVALIDARG;
    }

    auto start = std::chrono::high_resolution_clock::now();

    // Step 1: Convert BGRA to NV12 using video processor
    HRESULT hr = ConvertBGRAToNV12(pTexture, m_pNV12Texture);
    if (FAILED(hr))
    {
        return hr;
    }

    // Step 2: Create Media Foundation sample from NV12 texture
    hr = CreateMFSampleFromTexture(m_pNV12Texture, timestamp, ppSample);
    if (FAILED(hr))
    {
        return hr;
    }

    // Update statistics
    auto end = std::chrono::high_resolution_clock::now();
    double conversionTimeMs = std::chrono::duration<double, std::milli>(end - start).count();

    std::lock_guard<std::mutex> lock(m_mutex);
    m_conversionCount++;
    m_totalConversionTimeMs += conversionTimeMs;

    return S_OK;
}

HRESULT TextureConverter::UpdateResolution(UINT32 width, UINT32 height)
{
    if (width == m_width && height == m_height)
    {
        return S_OK;
    }

    // Release old resources
    if (m_pOutputView)
    {
        m_pOutputView->Release();
        m_pOutputView = nullptr;
    }

    if (m_pNV12Texture)
    {
        m_pNV12Texture->Release();
        m_pNV12Texture = nullptr;
    }

    if (m_pStagingTexture)
    {
        m_pStagingTexture->Release();
        m_pStagingTexture = nullptr;
    }

    if (m_pVideoProcessor)
    {
        m_pVideoProcessor->Release();
        m_pVideoProcessor = nullptr;
    }

    if (m_pVideoProcessorEnum)
    {
        m_pVideoProcessorEnum->Release();
        m_pVideoProcessorEnum = nullptr;
    }

    // Update dimensions
    m_width = width;
    m_height = height;

    // Recreate resources
    HRESULT hr = CreateVideoProcessor();
    if (FAILED(hr))
    {
        return hr;
    }

    hr = CreateStagingTextures();
    if (FAILED(hr))
    {
        return hr;
    }

    hr = CreateNV12Texture();
    if (FAILED(hr))
    {
        return hr;
    }

    return S_OK;
}

double TextureConverter::GetAverageConversionTimeMs() const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    if (m_conversionCount == 0)
    {
        return 0.0;
    }
    return m_totalConversionTimeMs / m_conversionCount;
}

uint64_t TextureConverter::GetConversionCount() const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    return m_conversionCount;
}
