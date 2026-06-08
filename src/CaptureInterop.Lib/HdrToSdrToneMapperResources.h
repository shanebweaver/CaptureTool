#pragma once

#include "Result.h"

#include <cstdint>
#include <d3d11.h>
#include <wil/com.h>

struct HdrToSdrToneMapperConstants
{
    float sdrWhiteNits = 100.0f;
    float exposureScale = 1.0f;
    float shoulderStrength = 1.0f;
    float padding0 = 0.0f;
};

class HdrToSdrToneMapperResources
{
public:
    static Result<HdrToSdrToneMapperResources> Create(
        ID3D11Device* device,
        ID3D11DeviceContext* context,
        uint32_t width,
        uint32_t height,
        const HdrToSdrToneMapperConstants& constants = {});

    ID3D11Texture2D* GetOutputTexture() const { return m_outputTexture.get(); }
    ID3D11RenderTargetView* GetOutputRenderTargetView() const { return m_outputRenderTargetView.get(); }
    ID3D11SamplerState* GetPointSampler() const { return m_pointSampler.get(); }
    ID3D11Buffer* GetConstantsBuffer() const { return m_constantsBuffer.get(); }
    uint32_t GetWidth() const { return m_width; }
    uint32_t GetHeight() const { return m_height; }

private:
    wil::com_ptr<ID3D11Device> m_device;
    wil::com_ptr<ID3D11DeviceContext> m_context;
    wil::com_ptr<ID3D11Texture2D> m_outputTexture;
    wil::com_ptr<ID3D11RenderTargetView> m_outputRenderTargetView;
    wil::com_ptr<ID3D11SamplerState> m_pointSampler;
    wil::com_ptr<ID3D11Buffer> m_constantsBuffer;
    uint32_t m_width = 0;
    uint32_t m_height = 0;
};
