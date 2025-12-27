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
/// Handles D3D11 texture processing for video frame capture.
/// Manages staging texture creation, texture copying, and pixel data extraction.
/// 
/// Implements RUST Principles:
/// - Principle #4 (Explicit Error Handling): Uses Result<T> for error handling
/// - Principle #5 (RAII Everything): Automatic cleanup of staging texture
/// - Principle #7 (Const Correctness): Read-only methods marked const
/// </summary>
class TextureProcessor
{
public:
    /// <summary>
    /// Create a texture processor with the specified device and dimensions.
    /// </summary>
    TextureProcessor(ID3D11Device* device, ID3D11DeviceContext* context, uint32_t width, uint32_t height);
    ~TextureProcessor() = default;

    // Delete copy operations
    TextureProcessor(const TextureProcessor&) = delete;
    TextureProcessor& operator=(const TextureProcessor&) = delete;

    // Allow move operations
    TextureProcessor(TextureProcessor&&) noexcept = default;
    TextureProcessor& operator=(TextureProcessor&&) noexcept = default;

    /// <summary>
    /// Copy texture data to a buffer in canonical stride format (width * 4 bytes per pixel).
    /// This handles textures with non-canonical stride by row-by-row copying.
    /// </summary>
    /// <param name="texture">Source texture to copy from.</param>
    /// <param name="outBuffer">Output buffer to receive the pixel data.</param>
    /// <returns>Result indicating success or error information.</returns>
    Result<void> CopyTextureToBuffer(ID3D11Texture2D* texture, std::vector<uint8_t>& outBuffer);

    /// <summary>
    /// Get the expected buffer size for a frame with current dimensions.
    /// </summary>
    uint32_t GetRequiredBufferSize() const { return m_width * m_height * 4; }

    /// <summary>
    /// Get the frame width.
    /// </summary>
    uint32_t GetWidth() const { return m_width; }

    /// <summary>
    /// Get the frame height.
    /// </summary>
    uint32_t GetHeight() const { return m_height; }

private:
    wil::com_ptr<ID3D11Device> m_device;
    wil::com_ptr<ID3D11DeviceContext> m_context;
    wil::com_ptr<ID3D11Texture2D> m_stagingTexture;
    uint32_t m_width;
    uint32_t m_height;

    /// <summary>
    /// Ensure staging texture exists and matches current dimensions.
    /// Lazy initialization - only creates texture when first needed.
    /// </summary>
    Result<void> EnsureStagingTexture();
};
