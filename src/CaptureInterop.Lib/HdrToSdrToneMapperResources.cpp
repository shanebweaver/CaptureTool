#include "pch.h"
#include "HdrToSdrToneMapperResources.h"

Result<HdrToSdrToneMapperResources> HdrToSdrToneMapperResources::Create(
    ID3D11Device* device,
    ID3D11DeviceContext* context,
    uint32_t width,
    uint32_t height,
    const HdrToSdrToneMapperConstants& constants)
{
    if (!device || !context)
    {
        return Result<HdrToSdrToneMapperResources>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Device and context are required", "HdrToSdrToneMapperResources::Create"));
    }

    if (width == 0 || height == 0)
    {
        return Result<HdrToSdrToneMapperResources>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Width and height must be non-zero", "HdrToSdrToneMapperResources::Create"));
    }

    HdrToSdrToneMapperResources resources;
    resources.m_device = device;
    resources.m_context = context;
    resources.m_width = width;
    resources.m_height = height;

    D3D11_TEXTURE2D_DESC textureDesc{};
    textureDesc.Width = width;
    textureDesc.Height = height;
    textureDesc.MipLevels = 1;
    textureDesc.ArraySize = 1;
    textureDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    textureDesc.SampleDesc.Count = 1;
    textureDesc.Usage = D3D11_USAGE_DEFAULT;
    textureDesc.BindFlags = D3D11_BIND_RENDER_TARGET;

    HRESULT hr = device->CreateTexture2D(&textureDesc, nullptr, resources.m_outputTexture.put());
    if (FAILED(hr))
    {
        return Result<HdrToSdrToneMapperResources>::Error(
            ErrorInfo::FromHResult(hr, "HdrToSdrToneMapperResources: CreateTexture2D failed"));
    }

    hr = device->CreateRenderTargetView(resources.m_outputTexture.get(), nullptr, resources.m_outputRenderTargetView.put());
    if (FAILED(hr))
    {
        return Result<HdrToSdrToneMapperResources>::Error(
            ErrorInfo::FromHResult(hr, "HdrToSdrToneMapperResources: CreateRenderTargetView failed"));
    }

    D3D11_SAMPLER_DESC samplerDesc{};
    samplerDesc.Filter = D3D11_FILTER_MIN_MAG_MIP_POINT;
    samplerDesc.AddressU = D3D11_TEXTURE_ADDRESS_CLAMP;
    samplerDesc.AddressV = D3D11_TEXTURE_ADDRESS_CLAMP;
    samplerDesc.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
    samplerDesc.ComparisonFunc = D3D11_COMPARISON_NEVER;
    samplerDesc.MinLOD = 0;
    samplerDesc.MaxLOD = D3D11_FLOAT32_MAX;

    hr = device->CreateSamplerState(&samplerDesc, resources.m_pointSampler.put());
    if (FAILED(hr))
    {
        return Result<HdrToSdrToneMapperResources>::Error(
            ErrorInfo::FromHResult(hr, "HdrToSdrToneMapperResources: CreateSamplerState failed"));
    }

    D3D11_BUFFER_DESC constantsDesc{};
    constantsDesc.ByteWidth = sizeof(HdrToSdrToneMapperConstants);
    constantsDesc.Usage = D3D11_USAGE_DEFAULT;
    constantsDesc.BindFlags = D3D11_BIND_CONSTANT_BUFFER;

    D3D11_SUBRESOURCE_DATA constantsData{};
    constantsData.pSysMem = &constants;

    hr = device->CreateBuffer(&constantsDesc, &constantsData, resources.m_constantsBuffer.put());
    if (FAILED(hr))
    {
        return Result<HdrToSdrToneMapperResources>::Error(
            ErrorInfo::FromHResult(hr, "HdrToSdrToneMapperResources: CreateBuffer failed"));
    }

    return Result<HdrToSdrToneMapperResources>::Ok(std::move(resources));
}
