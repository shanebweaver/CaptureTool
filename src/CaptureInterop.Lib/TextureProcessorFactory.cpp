#include "pch.h"
#include "TextureProcessorFactory.h"
#include "TextureProcessor.h"

std::unique_ptr<ITextureProcessor> TextureProcessorFactory::CreateTextureProcessor(
    ID3D11Device* device,
    ID3D11DeviceContext* context,
    uint32_t width,
    uint32_t height)
{
    return std::make_unique<TextureProcessor>(device, context, width, height);
}
