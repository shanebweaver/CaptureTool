#pragma once
#include "Result.h"
#include <cstdint>
#include <vector>

// Forward declarations
struct ID3D11Texture2D;

/// <summary>
/// Interface for handling D3D11 texture processing for video frames.
/// Provides abstraction for texture processing to enable dependency injection and testing.
/// </summary>
class ITextureProcessor
{
public:
    virtual ~ITextureProcessor() = default;

    /// <summary>
    /// Copy texture to buffer with canonical stride.
    /// Handles non-canonical strides via row-by-row copy.
    /// </summary>
    /// <param name="texture">Source D3D11 texture to copy from.</param>
    /// <param name="outBuffer">Output buffer to receive the texture data.</param>
    /// <returns>Result indicating success or error information.</returns>
    virtual Result<void> CopyTextureToBuffer(ID3D11Texture2D* texture, std::vector<uint8_t>& outBuffer) = 0;

    /// <summary>
    /// Get the required buffer size for texture data.
    /// </summary>
    /// <returns>Buffer size in bytes (width * height * 4 for RGB32).</returns>
    virtual uint32_t GetRequiredBufferSize() const = 0;

    /// <summary>
    /// Get the texture width.
    /// </summary>
    /// <returns>Width in pixels.</returns>
    virtual uint32_t GetWidth() const = 0;

    /// <summary>
    /// Get the texture height.
    /// </summary>
    /// <returns>Height in pixels.</returns>
    virtual uint32_t GetHeight() const = 0;
};
