#pragma once
#include "ITextureProcessorFactory.h"

/// <summary>
/// Factory for creating texture processor instances.
/// </summary>
class TextureProcessorFactory : public ITextureProcessorFactory
{
public:
    TextureProcessorFactory() = default;
    ~TextureProcessorFactory() override = default;

    // ITextureProcessorFactory implementation
    std::unique_ptr<ITextureProcessor> CreateTextureProcessor(
        ID3D11Device* device,
        ID3D11DeviceContext* context,
        uint32_t width,
        uint32_t height) override;
};
