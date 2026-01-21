#pragma once
#include <Windows.h>
#include <cstdint>

/// <summary>
/// Helper functions for converting D3D11 textures to pixel buffers for metadata scanners.
/// </summary>

extern "C"
{
    /// <summary>
    /// Convert a D3D11 texture to BGRA pixel buffer.
    /// Creates a staging texture, copies the data, and maps it to a CPU-readable buffer.
    /// </summary>
    /// <param name="pTexture">Pointer to ID3D11Texture2D.</param>
    /// <param name="pDevice">Pointer to ID3D11Device (optional, will create if null).</param>
    /// <param name="pContext">Pointer to ID3D11DeviceContext (optional, will create if null).</param>
    /// <param name="outBuffer">Output buffer to receive BGRA pixel data.</param>
    /// <param name="bufferSize">Size of the output buffer in bytes.</param>
    /// <param name="outRowPitch">Output parameter for row pitch (stride) of the data.</param>
    /// <returns>True if successful, false otherwise.</returns>
    __declspec(dllexport) bool ConvertTextureToPixelBuffer(
        void* pTexture,
        void* pDevice,
        void* pContext,
        uint8_t* outBuffer,
        uint32_t bufferSize,
        uint32_t* outRowPitch);
}
