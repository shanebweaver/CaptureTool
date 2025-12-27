#pragma once
#include "Result.h"
#include <wil/com.h>
#include <cstdint>
#include <vector>

// Forward declarations
struct ID3D11Device;
struct ID3D11DeviceContext;
struct ID3D11Texture2D;

/// <summary>
/// Handles D3D11 texture processing for video frames.
/// Manages staging texture and handles stride normalization.
/// Expects RGB32 (DXGI_FORMAT_B8G8R8A8_UNORM) format textures.
/// </summary>
class TextureProcessor
{
public:
    TextureProcessor(ID3D11Device* device, ID3D11DeviceContext* context, uint32_t width, uint32_t height);
    ~TextureProcessor() = default;

    TextureProcessor(const TextureProcessor&) = delete;
    TextureProcessor& operator=(const TextureProcessor&) = delete;
    TextureProcessor(TextureProcessor&&) noexcept = default;
    TextureProcessor& operator=(TextureProcessor&&) noexcept = default;

    /// <summary>
    /// Copy texture to buffer with canonical stride.
    /// Handles non-canonical strides via row-by-row copy.
    /// </summary>
    Result<void> CopyTextureToBuffer(ID3D11Texture2D* texture, std::vector<uint8_t>& outBuffer);

    uint32_t GetRequiredBufferSize() const { return m_width * m_height * BYTES_PER_PIXEL_RGB32; }
    uint32_t GetWidth() const { return m_width; }
    uint32_t GetHeight() const { return m_height; }

private:
    static constexpr uint32_t BYTES_PER_PIXEL_RGB32 = 4;
    
    wil::com_ptr<ID3D11Device> m_device;
    wil::com_ptr<ID3D11DeviceContext> m_context;
    wil::com_ptr<ID3D11Texture2D> m_stagingTexture;
    uint32_t m_width;
    uint32_t m_height;

    Result<void> EnsureStagingTexture();
};
