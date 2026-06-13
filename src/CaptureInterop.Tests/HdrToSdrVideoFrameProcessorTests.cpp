#include "pch.h"
#include "CppUnitTest.h"
#include "HdrToSdrVideoFrameProcessor.h"

#include <filesystem>
#include <fstream>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    namespace
    {
        std::filesystem::path GetPackagedShaderPath(const wchar_t* fileName)
        {
            const std::filesystem::path testSourcePath = __FILE__;
#if defined(_DEBUG)
            constexpr const wchar_t* ConfigurationName = L"Debug";
#else
            constexpr const wchar_t* ConfigurationName = L"Release";
#endif

#if defined(_M_ARM64)
            constexpr const wchar_t* PlatformName = L"ARM64";
#else
            constexpr const wchar_t* PlatformName = L"x64";
#endif

            return testSourcePath.parent_path().parent_path().parent_path() /
                L"bin" /
                ConfigurationName /
                PlatformName /
                fileName;
        }

        std::vector<uint8_t> ReadAllBytes(const std::filesystem::path& path)
        {
            std::ifstream stream(path, std::ios::binary);
            return std::vector<uint8_t>(
                std::istreambuf_iterator<char>(stream),
                std::istreambuf_iterator<char>());
        }

        void CreateWarpDevice(wil::com_ptr<ID3D11Device>& device, wil::com_ptr<ID3D11DeviceContext>& context)
        {
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
        }

        uint32_t PackBgra(uint8_t blue, uint8_t green, uint8_t red, uint8_t alpha)
        {
            return static_cast<uint32_t>(blue) |
                (static_cast<uint32_t>(green) << 8) |
                (static_cast<uint32_t>(red) << 16) |
                (static_cast<uint32_t>(alpha) << 24);
        }

        std::unique_ptr<HdrToSdrVideoFrameProcessor> CreateProcessor(
            ID3D11Device* device,
            ID3D11DeviceContext* context,
            uint32_t width,
            uint32_t height)
        {
            auto vertexShader = ReadAllBytes(GetPackagedShaderPath(L"HdrToSdrToneMapperVertex.cso"));
            auto pixelShader = ReadAllBytes(GetPackagedShaderPath(L"HdrToSdrToneMapper.cso"));
            Assert::IsFalse(vertexShader.empty());
            Assert::IsFalse(pixelShader.empty());

            auto processorResult = HdrToSdrVideoFrameProcessor::Create(
                device,
                context,
                width,
                height,
                vertexShader,
                pixelShader);
            Assert::IsTrue(processorResult.IsOk());
            return std::move(processorResult.Value());
        }

        wil::com_ptr<ID3D11Texture2D> CreateShaderResourceTexture(
            ID3D11Device* device,
            uint32_t width,
            uint32_t height,
            const std::vector<uint32_t>& pixels)
        {
            D3D11_TEXTURE2D_DESC sourceDesc{};
            sourceDesc.Width = width;
            sourceDesc.Height = height;
            sourceDesc.MipLevels = 1;
            sourceDesc.ArraySize = 1;
            sourceDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
            sourceDesc.SampleDesc.Count = 1;
            sourceDesc.Usage = D3D11_USAGE_DEFAULT;
            sourceDesc.BindFlags = D3D11_BIND_SHADER_RESOURCE;

            D3D11_SUBRESOURCE_DATA sourceData{};
            sourceData.pSysMem = pixels.data();
            sourceData.SysMemPitch = width * sizeof(uint32_t);

            wil::com_ptr<ID3D11Texture2D> sourceTexture;
            HRESULT hr = device->CreateTexture2D(&sourceDesc, &sourceData, sourceTexture.put());
            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(hr));
            return sourceTexture;
        }

        std::vector<uint32_t> ReadTexturePixels(
            ID3D11Device* device,
            ID3D11DeviceContext* context,
            ID3D11Texture2D* texture)
        {
            D3D11_TEXTURE2D_DESC outputDesc{};
            texture->GetDesc(&outputDesc);
            outputDesc.Usage = D3D11_USAGE_STAGING;
            outputDesc.BindFlags = 0;
            outputDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
            outputDesc.MiscFlags = 0;

            wil::com_ptr<ID3D11Texture2D> stagingTexture;
            HRESULT hr = device->CreateTexture2D(&outputDesc, nullptr, stagingTexture.put());
            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(hr));

            context->CopyResource(stagingTexture.get(), texture);

            D3D11_MAPPED_SUBRESOURCE mapped{};
            hr = context->Map(stagingTexture.get(), 0, D3D11_MAP_READ, 0, &mapped);
            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(hr));

            std::vector<uint32_t> pixels(outputDesc.Width * outputDesc.Height);
            for (UINT y = 0; y < outputDesc.Height; ++y)
            {
                memcpy(
                    pixels.data() + (y * outputDesc.Width),
                    static_cast<uint8_t*>(mapped.pData) + (y * mapped.RowPitch),
                    outputDesc.Width * sizeof(uint32_t));
            }

            context->Unmap(stagingTexture.get(), 0);
            return pixels;
        }

        uint8_t Blue(uint32_t bgra) { return static_cast<uint8_t>(bgra & 0xFF); }
        uint8_t Green(uint32_t bgra) { return static_cast<uint8_t>((bgra >> 8) & 0xFF); }
        uint8_t Red(uint32_t bgra) { return static_cast<uint8_t>((bgra >> 16) & 0xFF); }
        uint8_t Alpha(uint32_t bgra) { return static_cast<uint8_t>((bgra >> 24) & 0xFF); }
    }

    TEST_CLASS(HdrToSdrVideoFrameProcessorTests)
    {
    public:
        TEST_METHOD(Process_WithShaderResourceTexture_RendersOpaqueOutput)
        {
            wil::com_ptr<ID3D11Device> device;
            wil::com_ptr<ID3D11DeviceContext> context;
            CreateWarpDevice(device, context);

            auto vertexShader = ReadAllBytes(GetPackagedShaderPath(L"HdrToSdrToneMapperVertex.cso"));
            auto pixelShader = ReadAllBytes(GetPackagedShaderPath(L"HdrToSdrToneMapper.cso"));
            Assert::IsFalse(vertexShader.empty());
            Assert::IsFalse(pixelShader.empty());

            auto processorResult = HdrToSdrVideoFrameProcessor::Create(
                device.get(),
                context.get(),
                1,
                1,
                vertexShader,
                pixelShader);
            Assert::IsTrue(processorResult.IsOk());

            uint32_t bgraRed = 0xFFFF0000;
            D3D11_TEXTURE2D_DESC sourceDesc{};
            sourceDesc.Width = 1;
            sourceDesc.Height = 1;
            sourceDesc.MipLevels = 1;
            sourceDesc.ArraySize = 1;
            sourceDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
            sourceDesc.SampleDesc.Count = 1;
            sourceDesc.Usage = D3D11_USAGE_DEFAULT;
            sourceDesc.BindFlags = D3D11_BIND_SHADER_RESOURCE;

            D3D11_SUBRESOURCE_DATA sourceData{};
            sourceData.pSysMem = &bgraRed;
            sourceData.SysMemPitch = sizeof(bgraRed);

            wil::com_ptr<ID3D11Texture2D> sourceTexture;
            HRESULT hr = device->CreateTexture2D(&sourceDesc, &sourceData, sourceTexture.put());
            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(hr));

            auto processResult = processorResult.Value()->Process(sourceTexture.get());
            Assert::IsTrue(processResult.IsOk());
            Assert::IsNotNull(processResult.Value().texture);

            D3D11_TEXTURE2D_DESC outputDesc{};
            processResult.Value().texture->GetDesc(&outputDesc);
            Assert::AreEqual(static_cast<int>(DXGI_FORMAT_B8G8R8A8_UNORM), static_cast<int>(outputDesc.Format));
            outputDesc.Usage = D3D11_USAGE_STAGING;
            outputDesc.BindFlags = 0;
            outputDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
            outputDesc.MiscFlags = 0;

            wil::com_ptr<ID3D11Texture2D> stagingTexture;
            hr = device->CreateTexture2D(&outputDesc, nullptr, stagingTexture.put());
            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(hr));

            context->CopyResource(stagingTexture.get(), processResult.Value().texture);

            D3D11_MAPPED_SUBRESOURCE mapped{};
            hr = context->Map(stagingTexture.get(), 0, D3D11_MAP_READ, 0, &mapped);
            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(hr));
            auto* pixel = static_cast<uint8_t*>(mapped.pData);
            uint8_t blue = pixel[0];
            uint8_t green = pixel[1];
            uint8_t red = pixel[2];
            uint8_t alpha = pixel[3];
            context->Unmap(stagingTexture.get(), 0);

            Assert::AreEqual(static_cast<uint8_t>(255), alpha);
            Assert::IsTrue(red > 0);
            Assert::AreEqual(static_cast<uint8_t>(0), green);
            Assert::AreEqual(static_cast<uint8_t>(0), blue);
        }

        TEST_METHOD(Process_WithNonShaderResourceTexture_ReturnsError)
        {
            wil::com_ptr<ID3D11Device> device;
            wil::com_ptr<ID3D11DeviceContext> context;
            CreateWarpDevice(device, context);

            auto vertexShader = ReadAllBytes(GetPackagedShaderPath(L"HdrToSdrToneMapperVertex.cso"));
            auto pixelShader = ReadAllBytes(GetPackagedShaderPath(L"HdrToSdrToneMapper.cso"));
            auto processorResult = HdrToSdrVideoFrameProcessor::Create(
                device.get(),
                context.get(),
                1,
                1,
                vertexShader,
                pixelShader);
            Assert::IsTrue(processorResult.IsOk());

            D3D11_TEXTURE2D_DESC sourceDesc{};
            sourceDesc.Width = 1;
            sourceDesc.Height = 1;
            sourceDesc.MipLevels = 1;
            sourceDesc.ArraySize = 1;
            sourceDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
            sourceDesc.SampleDesc.Count = 1;
            sourceDesc.Usage = D3D11_USAGE_DEFAULT;
            sourceDesc.BindFlags = D3D11_BIND_RENDER_TARGET;

            wil::com_ptr<ID3D11Texture2D> sourceTexture;
            HRESULT hr = device->CreateTexture2D(&sourceDesc, nullptr, sourceTexture.put());
            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(hr));

            auto processResult = processorResult.Value()->Process(sourceTexture.get());

            Assert::IsTrue(processResult.IsError());
            Assert::AreEqual(static_cast<long>(E_INVALIDARG), static_cast<long>(processResult.Error().hr));
        }

        TEST_METHOD(Process_WithSyntheticPatches_ProducesBoundedOpaqueSdrOutput)
        {
            wil::com_ptr<ID3D11Device> device;
            wil::com_ptr<ID3D11DeviceContext> context;
            CreateWarpDevice(device, context);

            auto processor = CreateProcessor(device.get(), context.get(), 4, 1);
            std::vector<uint32_t> inputPixels{
                PackBgra(0, 0, 0, 17),
                PackBgra(128, 128, 128, 64),
                PackBgra(255, 255, 255, 128),
                PackBgra(255, 0, 0, 0)
            };
            auto sourceTexture = CreateShaderResourceTexture(device.get(), 4, 1, inputPixels);

            auto processResult = processor->Process(sourceTexture.get());

            Assert::IsTrue(processResult.IsOk());
            auto outputPixels = ReadTexturePixels(device.get(), context.get(), processResult.Value().texture);
            Assert::AreEqual(static_cast<size_t>(4), outputPixels.size());

            for (uint32_t pixel : outputPixels)
            {
                Assert::AreEqual(static_cast<uint8_t>(255), Alpha(pixel));
            }

            Assert::IsTrue(Red(outputPixels[1]) > Red(outputPixels[0]));
            Assert::IsTrue(Green(outputPixels[1]) > Green(outputPixels[0]));
            Assert::IsTrue(Blue(outputPixels[1]) > Blue(outputPixels[0]));
            Assert::IsTrue(Red(outputPixels[2]) >= Red(outputPixels[1]));
            Assert::IsTrue(Green(outputPixels[2]) >= Green(outputPixels[1]));
            Assert::IsTrue(Blue(outputPixels[2]) >= Blue(outputPixels[1]));

            Assert::IsTrue(Blue(outputPixels[3]) > Red(outputPixels[3]));
            Assert::IsTrue(Blue(outputPixels[3]) > Green(outputPixels[3]));
        }
    };
}
