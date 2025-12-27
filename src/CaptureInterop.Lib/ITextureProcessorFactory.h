#pragma once
#include "ITextureProcessor.h"
#include <memory>
#include <cstdint>

// Forward declarations
struct ID3D11Device;
struct ID3D11DeviceContext;

/// <summary>
/// Factory interface for creating texture processor instances.
/// Provides abstraction for texture processor creation to enable dependency injection and testing.
/// </summary>
class ITextureProcessorFactory
{
public:
    virtual ~ITextureProcessorFactory() = default;

    /// <summary>
    /// Create a new texture processor instance.
    /// </summary>
    /// <param name="device">D3D11 device for texture operations.</param>
    /// <param name="context">D3D11 device context for texture operations.</param>
    /// <param name="width">Expected texture width in pixels.</param>
    /// <param name="height">Expected texture height in pixels.</param>
    /// <returns>A unique pointer to a new ITextureProcessor implementation.</returns>
    virtual std::unique_ptr<ITextureProcessor> CreateTextureProcessor(
        ID3D11Device* device,
        ID3D11DeviceContext* context,
        uint32_t width,
        uint32_t height) = 0;
};
