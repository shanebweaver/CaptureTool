#pragma once

#include "HdrToSdrToneMapperResources.h"
#include "IVideoFrameProcessor.h"

#include <span>
#include <vector>

class HdrToSdrVideoFrameProcessor final : public IVideoFrameProcessor
{
public:
    static Result<std::unique_ptr<HdrToSdrVideoFrameProcessor>> Create(
        ID3D11Device* device,
        ID3D11DeviceContext* context,
        uint32_t width,
        uint32_t height,
        std::span<const uint8_t> vertexShaderBytecode,
        std::span<const uint8_t> pixelShaderBytecode,
        const HdrToSdrToneMapperConstants& constants = {});

    Result<VideoFrameProcessorResult> Process(ID3D11Texture2D* texture) override;
    void Reset() override;

private:
    HdrToSdrVideoFrameProcessor() = default;

    Result<ID3D11ShaderResourceView*> GetOrCreateSourceView(ID3D11Texture2D* texture);

    wil::com_ptr<ID3D11Device> m_device;
    wil::com_ptr<ID3D11DeviceContext> m_context;
    HdrToSdrToneMapperResources m_resources;
    wil::com_ptr<ID3D11VertexShader> m_vertexShader;
    wil::com_ptr<ID3D11PixelShader> m_pixelShader;
    std::vector<std::pair<wil::com_ptr<ID3D11Texture2D>, wil::com_ptr<ID3D11ShaderResourceView>>> m_sourceViews;
};
