#include "pch.h"
#include "TextureProcessor.h"

TextureProcessor::TextureProcessor(
    ID3D11Device* device,
    ID3D11DeviceContext* context,
    uint32_t width,
    uint32_t height,
    uint32_t sourceLeft,
    uint32_t sourceTop)
    : m_device(device)
    , m_context(context)
    , m_width(width)
    , m_height(height)
    , m_sourceLeft(sourceLeft)
    , m_sourceTop(sourceTop)
{
}

Result<void> TextureProcessor::EnsureStagingTexture()
{
    if (m_stagingTexture)
    {
        return Result<void>::Ok();
    }

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
    const uint32_t bufferSize = GetRequiredBufferSize();
    outBuffer.resize(bufferSize);
    return CopyTextureToMemory(texture, std::span<uint8_t>(outBuffer.data(), outBuffer.size()));
}

Result<void> TextureProcessor::CopyTextureToMemory(ID3D11Texture2D* texture, std::span<uint8_t> outBuffer)
{
    if (!texture)
    {
        return Result<void>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Texture is null", "TextureProcessor::CopyTextureToBuffer"));
    }

    // Validate texture format is compatible with RGB32
    D3D11_TEXTURE2D_DESC desc{};
    texture->GetDesc(&desc);
    if (desc.Format != DXGI_FORMAT_B8G8R8A8_UNORM)
    {
        return Result<void>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Texture format must be DXGI_FORMAT_B8G8R8A8_UNORM", "TextureProcessor::CopyTextureToBuffer"));
    }

    if (m_sourceLeft + m_width > desc.Width || m_sourceTop + m_height > desc.Height)
    {
        return Result<void>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Requested source rectangle is outside texture bounds", "TextureProcessor::CopyTextureToMemory"));
    }

    auto stagingResult = EnsureStagingTexture();
    if (stagingResult.IsError())
    {
        return stagingResult;
    }

    D3D11_BOX sourceBox{};
    sourceBox.left = m_sourceLeft;
    sourceBox.top = m_sourceTop;
    sourceBox.front = 0;
    sourceBox.right = m_sourceLeft + m_width;
    sourceBox.bottom = m_sourceTop + m_height;
    sourceBox.back = 1;

    m_context->CopySubresourceRegion(m_stagingTexture.get(), 0, 0, 0, 0, texture, 0, &sourceBox);

    D3D11_MAPPED_SUBRESOURCE mapped{};
    HRESULT hr = m_context->Map(m_stagingTexture.get(), 0, D3D11_MAP_READ, 0, &mapped);
    if (FAILED(hr))
    {
        return Result<void>::Error(
            ErrorInfo::FromHResult(hr, "TextureProcessor: Failed to map staging texture"));
    }

    const uint32_t canonicalStride = m_width * BYTES_PER_PIXEL_RGB32;
    const uint32_t bufferSize = canonicalStride * m_height;
    if (outBuffer.size() < bufferSize)
    {
        m_context->Unmap(m_stagingTexture.get(), 0);
        return Result<void>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Output buffer is too small", "TextureProcessor::CopyTextureToMemory"));
    }

    for (uint32_t row = 0; row < m_height; ++row)
    {
        uint8_t* destRow = outBuffer.data() + row * canonicalStride;
        uint8_t* srcRow = static_cast<uint8_t*>(mapped.pData) + row * mapped.RowPitch;
        memcpy(destRow, srcRow, canonicalStride);
    }

    m_context->Unmap(m_stagingTexture.get(), 0);

    return Result<void>::Ok();
}
