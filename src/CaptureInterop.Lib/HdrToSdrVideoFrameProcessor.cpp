#include "pch.h"
#include "HdrToSdrVideoFrameProcessor.h"

namespace
{
    class D3D11PipelineStateGuard
    {
    public:
        explicit D3D11PipelineStateGuard(ID3D11DeviceContext* context)
            : m_context(context)
        {
            if (!m_context)
            {
                return;
            }

            m_context->IAGetInputLayout(m_inputLayout.put());
            m_context->IAGetPrimitiveTopology(&m_topology);
            m_context->VSGetShader(m_vertexShader.put(), nullptr, nullptr);
            m_context->PSGetShader(m_pixelShader.put(), nullptr, nullptr);
            m_context->PSGetShaderResources(0, 1, m_pixelShaderResourceView.put());
            m_context->PSGetSamplers(0, 1, m_samplerState.put());
            m_context->PSGetConstantBuffers(0, 1, m_constantBuffer.put());
            m_viewportCount = D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE;
            m_context->RSGetViewports(&m_viewportCount, m_viewports);
            m_context->OMGetRenderTargets(1, m_renderTargetView.put(), m_depthStencilView.put());
        }

        ~D3D11PipelineStateGuard()
        {
            if (!m_context)
            {
                return;
            }

            ID3D11ShaderResourceView* nullSrv = nullptr;
            m_context->PSSetShaderResources(0, 1, &nullSrv);

            m_context->IASetInputLayout(m_inputLayout.get());
            m_context->IASetPrimitiveTopology(m_topology);
            m_context->VSSetShader(m_vertexShader.get(), nullptr, 0);
            m_context->PSSetShader(m_pixelShader.get(), nullptr, 0);
            ID3D11ShaderResourceView* srv = m_pixelShaderResourceView.get();
            m_context->PSSetShaderResources(0, 1, &srv);
            ID3D11SamplerState* sampler = m_samplerState.get();
            m_context->PSSetSamplers(0, 1, &sampler);
            ID3D11Buffer* constantBuffer = m_constantBuffer.get();
            m_context->PSSetConstantBuffers(0, 1, &constantBuffer);
            if (m_viewportCount > 0)
            {
                m_context->RSSetViewports(m_viewportCount, m_viewports);
            }
            ID3D11RenderTargetView* rtv = m_renderTargetView.get();
            m_context->OMSetRenderTargets(1, &rtv, m_depthStencilView.get());
        }

    private:
        ID3D11DeviceContext* m_context;
        wil::com_ptr<ID3D11InputLayout> m_inputLayout;
        D3D11_PRIMITIVE_TOPOLOGY m_topology = D3D11_PRIMITIVE_TOPOLOGY_UNDEFINED;
        wil::com_ptr<ID3D11VertexShader> m_vertexShader;
        wil::com_ptr<ID3D11PixelShader> m_pixelShader;
        wil::com_ptr<ID3D11ShaderResourceView> m_pixelShaderResourceView;
        wil::com_ptr<ID3D11SamplerState> m_samplerState;
        wil::com_ptr<ID3D11Buffer> m_constantBuffer;
        UINT m_viewportCount = 0;
        D3D11_VIEWPORT m_viewports[D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE]{};
        wil::com_ptr<ID3D11RenderTargetView> m_renderTargetView;
        wil::com_ptr<ID3D11DepthStencilView> m_depthStencilView;
    };
}

Result<std::unique_ptr<HdrToSdrVideoFrameProcessor>> HdrToSdrVideoFrameProcessor::Create(
    ID3D11Device* device,
    ID3D11DeviceContext* context,
    uint32_t width,
    uint32_t height,
    std::span<const uint8_t> vertexShaderBytecode,
    std::span<const uint8_t> pixelShaderBytecode,
    const HdrToSdrToneMapperConstants& constants)
{
    if (!device || !context || vertexShaderBytecode.empty() || pixelShaderBytecode.empty())
    {
        return Result<std::unique_ptr<HdrToSdrVideoFrameProcessor>>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Device, context, and shader bytecode are required", "HdrToSdrVideoFrameProcessor::Create"));
    }

    auto resourcesResult = HdrToSdrToneMapperResources::Create(device, context, width, height, constants);
    if (resourcesResult.IsError())
    {
        return Result<std::unique_ptr<HdrToSdrVideoFrameProcessor>>::Error(resourcesResult.Error());
    }

    auto processor = std::unique_ptr<HdrToSdrVideoFrameProcessor>(new HdrToSdrVideoFrameProcessor());
    processor->m_device = device;
    processor->m_context = context;
    processor->m_resources = std::move(resourcesResult.Value());

    HRESULT hr = device->CreateVertexShader(
        vertexShaderBytecode.data(),
        vertexShaderBytecode.size(),
        nullptr,
        processor->m_vertexShader.put());
    if (FAILED(hr))
    {
        return Result<std::unique_ptr<HdrToSdrVideoFrameProcessor>>::Error(
            ErrorInfo::FromHResult(hr, "HdrToSdrVideoFrameProcessor: CreateVertexShader failed"));
    }

    hr = device->CreatePixelShader(
        pixelShaderBytecode.data(),
        pixelShaderBytecode.size(),
        nullptr,
        processor->m_pixelShader.put());
    if (FAILED(hr))
    {
        return Result<std::unique_ptr<HdrToSdrVideoFrameProcessor>>::Error(
            ErrorInfo::FromHResult(hr, "HdrToSdrVideoFrameProcessor: CreatePixelShader failed"));
    }

    return Result<std::unique_ptr<HdrToSdrVideoFrameProcessor>>::Ok(std::move(processor));
}

Result<ID3D11ShaderResourceView*> HdrToSdrVideoFrameProcessor::GetOrCreateSourceView(ID3D11Texture2D* texture)
{
    for (auto& entry : m_sourceViews)
    {
        if (entry.first.get() == texture)
        {
            return Result<ID3D11ShaderResourceView*>::Ok(entry.second.get());
        }
    }

    D3D11_TEXTURE2D_DESC desc{};
    texture->GetDesc(&desc);
    if ((desc.BindFlags & D3D11_BIND_SHADER_RESOURCE) == 0)
    {
        return Result<ID3D11ShaderResourceView*>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Texture must be shader-resource bindable", "HdrToSdrVideoFrameProcessor::Process"));
    }

    wil::com_ptr<ID3D11ShaderResourceView> sourceView;
    HRESULT hr = m_device->CreateShaderResourceView(texture, nullptr, sourceView.put());
    if (FAILED(hr))
    {
        return Result<ID3D11ShaderResourceView*>::Error(
            ErrorInfo::FromHResult(hr, "HdrToSdrVideoFrameProcessor: CreateShaderResourceView failed"));
    }

    wil::com_ptr<ID3D11Texture2D> textureRef;
    textureRef = texture;
    m_sourceViews.push_back({ std::move(textureRef), std::move(sourceView) });
    return Result<ID3D11ShaderResourceView*>::Ok(m_sourceViews.back().second.get());
}

Result<VideoFrameProcessorResult> HdrToSdrVideoFrameProcessor::Process(ID3D11Texture2D* texture)
{
    if (!texture)
    {
        return Result<VideoFrameProcessorResult>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Texture is null", "HdrToSdrVideoFrameProcessor::Process"));
    }

    D3D11_TEXTURE2D_DESC desc{};
    texture->GetDesc(&desc);
    if (desc.Width != m_resources.GetWidth() || desc.Height != m_resources.GetHeight())
    {
        return Result<VideoFrameProcessorResult>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Texture size does not match tone mapper output size", "HdrToSdrVideoFrameProcessor::Process"));
    }

    auto sourceViewResult = GetOrCreateSourceView(texture);
    if (sourceViewResult.IsError())
    {
        return Result<VideoFrameProcessorResult>::Error(sourceViewResult.Error());
    }

    D3D11PipelineStateGuard stateGuard(m_context.get());

    D3D11_VIEWPORT viewport{};
    viewport.Width = static_cast<float>(m_resources.GetWidth());
    viewport.Height = static_cast<float>(m_resources.GetHeight());
    viewport.MinDepth = 0.0f;
    viewport.MaxDepth = 1.0f;

    ID3D11RenderTargetView* rtv = m_resources.GetOutputRenderTargetView();
    m_context->OMSetRenderTargets(1, &rtv, nullptr);
    m_context->RSSetViewports(1, &viewport);
    m_context->IASetInputLayout(nullptr);
    m_context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    m_context->VSSetShader(m_vertexShader.get(), nullptr, 0);
    m_context->PSSetShader(m_pixelShader.get(), nullptr, 0);

    ID3D11ShaderResourceView* srv = sourceViewResult.Value();
    ID3D11SamplerState* sampler = m_resources.GetPointSampler();
    ID3D11Buffer* constants = m_resources.GetConstantsBuffer();
    m_context->PSSetShaderResources(0, 1, &srv);
    m_context->PSSetSamplers(0, 1, &sampler);
    m_context->PSSetConstantBuffers(0, 1, &constants);
    m_context->Draw(3, 0);

    return Result<VideoFrameProcessorResult>::Ok(VideoFrameProcessorResult{ m_resources.GetOutputTexture() });
}

void HdrToSdrVideoFrameProcessor::Reset()
{
    m_sourceViews.clear();
}
