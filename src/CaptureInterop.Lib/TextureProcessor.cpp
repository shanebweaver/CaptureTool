#include "pch.h"
#include "TextureProcessor.h"

TextureProcessor::TextureProcessor(ID3D11Device* device, ID3D11DeviceContext* context, uint32_t width, uint32_t height)
    : m_device(device)
    , m_context(context)
    , m_width(width)
    , m_height(height)
{
}

Result<void> TextureProcessor::EnsureStagingTexture()
{
    if (m_stagingTexture)
    {
        return Result<void>::Ok();
    }

    // Create staging texture for CPU access
    D3D11_TEXTURE2D_DESC stagingDesc{};
    stagingDesc.Width = m_width;
    stagingDesc.Height = m_height;
    stagingDesc.MipLevels = 1;
    stagingDesc.ArraySize = 1;
    stagingDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    stagingDesc.SampleDesc.Count = 1;
    stagingDesc.SampleDesc.Quality = 0;
    stagingDesc.Usage = D3D11_USAGE_STAGING;
    stagingDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    stagingDesc.BindFlags = 0;
    stagingDesc.MiscFlags = 0;

    HRESULT hr = m_device->CreateTexture2D(&stagingDesc, nullptr, m_stagingTexture.put());
    if (FAILED(hr))
    {
        return Result<void>::Error(
            ErrorInfo::FromHResult(hr, "TextureProcessor: Failed to create staging texture"));
    }

    return Result<void>::Ok();
}

Result<void> TextureProcessor::CopyTextureToBuffer(ID3D11Texture2D* texture, std::vector<uint8_t>& outBuffer)
{
    if (!texture)
    {
        return Result<void>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Texture is null", "TextureProcessor::CopyTextureToBuffer"));
    }

    // Ensure staging texture exists
    auto stagingResult = EnsureStagingTexture();
    if (stagingResult.IsError())
    {
        return stagingResult;
    }

    // Copy texture to staging
    m_context->CopyResource(m_stagingTexture.get(), texture);

    // Map the staging texture for CPU read access
    D3D11_MAPPED_SUBRESOURCE mapped{};
    HRESULT hr = m_context->Map(m_stagingTexture.get(), 0, D3D11_MAP_READ, 0, &mapped);
    if (FAILED(hr))
    {
        return Result<void>::Error(
            ErrorInfo::FromHResult(hr, "TextureProcessor: Failed to map staging texture"));
    }

    // Ensure buffer is correctly sized
    const uint32_t canonicalStride = m_width * BYTES_PER_PIXEL_RGB32;
    const uint32_t bufferSize = canonicalStride * m_height;
    outBuffer.resize(bufferSize);

    // Copy data row by row to handle non-canonical stride
    for (uint32_t row = 0; row < m_height; ++row)
    {
        uint8_t* destRow = outBuffer.data() + row * canonicalStride;
        uint8_t* srcRow = static_cast<uint8_t*>(mapped.pData) + row * mapped.RowPitch;
        memcpy(destRow, srcRow, canonicalStride);
    }

    // Unmap the staging texture
    m_context->Unmap(m_stagingTexture.get(), 0);

    return Result<void>::Ok();
}
