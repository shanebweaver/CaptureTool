#include "pch.h"
#include "CppUnitTest.h"
#include "HdrToSdrToneMapperResources.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    TEST_CLASS(HdrToSdrToneMapperResourcesTests)
    {
    public:
        TEST_METHOD(Create_WithWarpDevice_CreatesReusableResources)
        {
            wil::com_ptr<ID3D11Device> device;
            wil::com_ptr<ID3D11DeviceContext> context;
            D3D_FEATURE_LEVEL featureLevel{};
            HRESULT hr = D3D11CreateDevice(
                nullptr,
                D3D_DRIVER_TYPE_WARP,
                nullptr,
                D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                nullptr,
                0,
                D3D11_SDK_VERSION,
                device.put(),
                &featureLevel,
                context.put());
            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(hr));

            HdrToSdrToneMapperConstants constants{};
            constants.sdrWhiteNits = 203.0f;
            constants.exposureScale = 1.25f;
            constants.shoulderStrength = 0.8f;

            auto result = HdrToSdrToneMapperResources::Create(device.get(), context.get(), 320, 180, constants);

            Assert::IsTrue(result.IsOk());
            auto& resources = result.Value();
            Assert::IsNotNull(resources.GetOutputTexture());
            Assert::IsNotNull(resources.GetOutputRenderTargetView());
            Assert::IsNotNull(resources.GetPointSampler());
            Assert::IsNotNull(resources.GetConstantsBuffer());
            Assert::AreEqual(static_cast<uint32_t>(320), resources.GetWidth());
            Assert::AreEqual(static_cast<uint32_t>(180), resources.GetHeight());

            D3D11_TEXTURE2D_DESC textureDesc{};
            resources.GetOutputTexture()->GetDesc(&textureDesc);
            Assert::AreEqual(static_cast<UINT>(320), textureDesc.Width);
            Assert::AreEqual(static_cast<UINT>(180), textureDesc.Height);
            Assert::AreEqual(static_cast<int>(DXGI_FORMAT_B8G8R8A8_UNORM), static_cast<int>(textureDesc.Format));
            Assert::AreEqual(static_cast<UINT>(D3D11_BIND_RENDER_TARGET), textureDesc.BindFlags);

            D3D11_SAMPLER_DESC samplerDesc{};
            resources.GetPointSampler()->GetDesc(&samplerDesc);
            Assert::AreEqual(static_cast<int>(D3D11_FILTER_MIN_MAG_MIP_POINT), static_cast<int>(samplerDesc.Filter));

            D3D11_BUFFER_DESC bufferDesc{};
            resources.GetConstantsBuffer()->GetDesc(&bufferDesc);
            Assert::AreEqual(static_cast<UINT>(sizeof(HdrToSdrToneMapperConstants)), bufferDesc.ByteWidth);
            Assert::AreEqual(static_cast<UINT>(D3D11_BIND_CONSTANT_BUFFER), bufferDesc.BindFlags);
        }

        TEST_METHOD(Create_WithInvalidArguments_ReturnsError)
        {
            auto nullDeviceResult = HdrToSdrToneMapperResources::Create(nullptr, nullptr, 320, 180);
            Assert::IsTrue(nullDeviceResult.IsError());
            Assert::AreEqual(static_cast<long>(E_INVALIDARG), static_cast<long>(nullDeviceResult.Error().hr));

            wil::com_ptr<ID3D11Device> device;
            wil::com_ptr<ID3D11DeviceContext> context;
            D3D_FEATURE_LEVEL featureLevel{};
            HRESULT hr = D3D11CreateDevice(
                nullptr,
                D3D_DRIVER_TYPE_WARP,
                nullptr,
                D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                nullptr,
                0,
                D3D11_SDK_VERSION,
                device.put(),
                &featureLevel,
                context.put());
            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(hr));

            auto zeroSizeResult = HdrToSdrToneMapperResources::Create(device.get(), context.get(), 0, 180);
            Assert::IsTrue(zeroSizeResult.IsError());
            Assert::AreEqual(static_cast<long>(E_INVALIDARG), static_cast<long>(zeroSizeResult.Error().hr));
        }
    };
}
