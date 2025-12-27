#include "pch.h"
#include "CppUnitTest.h"
#include "MediaFoundationLifecycleManager.h"
#include "StreamConfigurationBuilder.h"
#include "TextureProcessor.h"
#include "SampleBuilder.h"

#include <d3d11.h>
#include <dxgiformat.h>
#include <vector>
#include <wil/com.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    TEST_CLASS(MediaFoundationLifecycleManagerTests)
    {
    public:
        TEST_METHOD(Constructor_InitializesMediaFoundation)
        {
            MediaFoundationLifecycleManager manager;
            Assert::IsTrue(manager.IsInitialized());
            Assert::AreEqual(S_OK, manager.GetInitializationResult());
        }

        TEST_METHOD(MultipleInstances_SharesMediaFoundation)
        {
            MediaFoundationLifecycleManager manager1;
            MediaFoundationLifecycleManager manager2;
            MediaFoundationLifecycleManager manager3;

            Assert::IsTrue(manager1.IsInitialized());
            Assert::IsTrue(manager2.IsInitialized());
            Assert::IsTrue(manager3.IsInitialized());
        }

        TEST_METHOD(Destructor_CleansUpProperly)
        {
            {
                MediaFoundationLifecycleManager manager;
                Assert::IsTrue(manager.IsInitialized());
            }
            // Should not crash - MF properly shut down
        }
    };

    TEST_CLASS(StreamConfigurationBuilderTests)
    {
    public:
        TEST_METHOD(CreateVideoOutputType_WithDefaultConfig_Succeeds)
        {
            StreamConfigurationBuilder builder;
            auto config = StreamConfigurationBuilder::VideoConfig::Default(1920, 1080);

            auto result = builder.CreateVideoOutputType(config);

            Assert::IsTrue(result.IsOk());
            Assert::IsNotNull(result.Value().get());
        }

        TEST_METHOD(CreateVideoInputType_WithDefaultConfig_Succeeds)
        {
            StreamConfigurationBuilder builder;
            auto config = StreamConfigurationBuilder::VideoConfig::Default(1280, 720);

            auto result = builder.CreateVideoInputType(config);

            Assert::IsTrue(result.IsOk());
            Assert::IsNotNull(result.Value().get());
        }

        TEST_METHOD(CreateAudioOutputType_WithValidConfig_Succeeds)
        {
            StreamConfigurationBuilder builder;
            StreamConfigurationBuilder::AudioConfig config{};
            config.sampleRate = 48000;
            config.channels = 2;
            config.bitsPerSample = 16;
            config.bitrate = StreamConfigurationBuilder::AudioConfig::DEFAULT_AAC_BITRATE;
            config.isFloatFormat = false;

            auto result = builder.CreateAudioOutputType(config);

            Assert::IsTrue(result.IsOk());
            Assert::IsNotNull(result.Value().get());
        }

        TEST_METHOD(CreateAudioInputType_WithPCMFormat_Succeeds)
        {
            StreamConfigurationBuilder builder;
            StreamConfigurationBuilder::AudioConfig config{};
            config.sampleRate = 48000;
            config.channels = 2;
            config.bitsPerSample = 16;
            config.bitrate = StreamConfigurationBuilder::AudioConfig::DEFAULT_AAC_BITRATE;
            config.isFloatFormat = false;

            auto result = builder.CreateAudioInputType(config);

            Assert::IsTrue(result.IsOk());
            Assert::IsNotNull(result.Value().get());
        }

        TEST_METHOD(CreateAudioInputType_WithFloatFormat_Succeeds)
        {
            StreamConfigurationBuilder builder;
            StreamConfigurationBuilder::AudioConfig config{};
            config.sampleRate = 48000;
            config.channels = 2;
            config.bitsPerSample = 32;
            config.bitrate = StreamConfigurationBuilder::AudioConfig::DEFAULT_AAC_BITRATE;
            config.isFloatFormat = true;

            auto result = builder.CreateAudioInputType(config);

            Assert::IsTrue(result.IsOk());
            Assert::IsNotNull(result.Value().get());
        }

        TEST_METHOD(AudioConfig_FromWaveFormat_ParsesPCM)
        {
            WAVEFORMATEX waveFormat{};
            waveFormat.wFormatTag = WAVE_FORMAT_PCM;
            waveFormat.nChannels = 2;
            waveFormat.nSamplesPerSec = 48000;
            waveFormat.wBitsPerSample = 16;
            waveFormat.nBlockAlign = 4;
            waveFormat.nAvgBytesPerSec = 192000;

            auto config = StreamConfigurationBuilder::AudioConfig::FromWaveFormat(waveFormat);

            Assert::AreEqual(48000u, config.sampleRate);
            Assert::AreEqual(2u, config.channels);
            Assert::AreEqual(16u, config.bitsPerSample);
            Assert::IsFalse(config.isFloatFormat);
        }

        TEST_METHOD(AudioConfig_FromWaveFormat_ParsesFloat)
        {
            WAVEFORMATEX waveFormat{};
            waveFormat.wFormatTag = WAVE_FORMAT_IEEE_FLOAT;
            waveFormat.nChannels = 2;
            waveFormat.nSamplesPerSec = 48000;
            waveFormat.wBitsPerSample = 32;
            waveFormat.nBlockAlign = 8;
            waveFormat.nAvgBytesPerSec = 384000;

            auto config = StreamConfigurationBuilder::AudioConfig::FromWaveFormat(waveFormat);

            Assert::AreEqual(48000u, config.sampleRate);
            Assert::AreEqual(2u, config.channels);
            Assert::AreEqual(32u, config.bitsPerSample);
            Assert::IsTrue(config.isFloatFormat);
        }
    };

    TEST_CLASS(TextureProcessorTests)
    {
    private:
        wil::com_ptr<ID3D11Device> CreateTestDevice()
        {
            wil::com_ptr<ID3D11Device> device;
            D3D_FEATURE_LEVEL featureLevel;

            HRESULT hr = D3D11CreateDevice(
                nullptr,
                D3D_DRIVER_TYPE_HARDWARE,
                nullptr,
                0,
                nullptr,
                0,
                D3D11_SDK_VERSION,
                device.put(),
                &featureLevel,
                nullptr
            );

            if (FAILED(hr))
            {
                hr = D3D11CreateDevice(
                    nullptr,
                    D3D_DRIVER_TYPE_WARP,
                    nullptr,
                    0,
                    nullptr,
                    0,
                    D3D11_SDK_VERSION,
                    device.put(),
                    &featureLevel,
                    nullptr
                );
            }

            Assert::IsTrue(SUCCEEDED(hr), L"Failed to create D3D11 device");
            return device;
        }

        wil::com_ptr<ID3D11Texture2D> CreateTestTexture(ID3D11Device* device, UINT width, UINT height, DXGI_FORMAT format)
        {
            D3D11_TEXTURE2D_DESC desc{};
            desc.Width = width;
            desc.Height = height;
            desc.MipLevels = 1;
            desc.ArraySize = 1;
            desc.Format = format;
            desc.SampleDesc.Count = 1;
            desc.Usage = D3D11_USAGE_DEFAULT;
            desc.BindFlags = D3D11_BIND_RENDER_TARGET;

            wil::com_ptr<ID3D11Texture2D> texture;
            HRESULT hr = device->CreateTexture2D(&desc, nullptr, texture.put());
            Assert::IsTrue(SUCCEEDED(hr), L"Failed to create test texture");
            return texture;
        }

    public:
        TEST_METHOD(Constructor_InitializesSuccessfully)
        {
            auto device = CreateTestDevice();
            wil::com_ptr<ID3D11DeviceContext> context;
            device->GetImmediateContext(context.put());

            TextureProcessor processor(device.get(), context.get(), 640, 480);

            Assert::AreEqual(640u, processor.GetWidth());
            Assert::AreEqual(480u, processor.GetHeight());
            Assert::AreEqual(640u * 480u * 4u, processor.GetRequiredBufferSize());
        }

        TEST_METHOD(CopyTextureToBuffer_WithValidTexture_Succeeds)
        {
            auto device = CreateTestDevice();
            wil::com_ptr<ID3D11DeviceContext> context;
            device->GetImmediateContext(context.put());

            TextureProcessor processor(device.get(), context.get(), 640, 480);
            auto texture = CreateTestTexture(device.get(), 640, 480, DXGI_FORMAT_B8G8R8A8_UNORM);

            std::vector<uint8_t> buffer;
            auto result = processor.CopyTextureToBuffer(texture.get(), buffer);

            Assert::IsTrue(result.IsOk());
            Assert::AreEqual(640u * 480u * 4u, static_cast<uint32_t>(buffer.size()));
        }

        TEST_METHOD(CopyTextureToBuffer_WithNullTexture_Fails)
        {
            auto device = CreateTestDevice();
            wil::com_ptr<ID3D11DeviceContext> context;
            device->GetImmediateContext(context.put());

            TextureProcessor processor(device.get(), context.get(), 640, 480);

            std::vector<uint8_t> buffer;
            auto result = processor.CopyTextureToBuffer(nullptr, buffer);

            Assert::IsTrue(result.IsError());
        }

        TEST_METHOD(CopyTextureToBuffer_WithInvalidFormat_Fails)
        {
            auto device = CreateTestDevice();
            wil::com_ptr<ID3D11DeviceContext> context;
            device->GetImmediateContext(context.put());

            TextureProcessor processor(device.get(), context.get(), 640, 480);
            auto texture = CreateTestTexture(device.get(), 640, 480, DXGI_FORMAT_R8G8B8A8_UNORM);

            std::vector<uint8_t> buffer;
            auto result = processor.CopyTextureToBuffer(texture.get(), buffer);

            Assert::IsTrue(result.IsError());
        }
    };

    TEST_CLASS(SampleBuilderTests)
    {
    public:
        TEST_METHOD(CreateVideoSample_WithValidData_Succeeds)
        {
            SampleBuilder builder;
            std::vector<uint8_t> data(1920 * 1080 * 4, 0);

            auto result = builder.CreateVideoSample(data, 1000000, 333333);

            Assert::IsTrue(result.IsOk());
            Assert::IsNotNull(result.Value().get());
        }

        TEST_METHOD(CreateAudioSample_WithValidData_Succeeds)
        {
            SampleBuilder builder;
            std::vector<uint8_t> data(4800, 0); // 25ms of 48kHz stereo 16-bit audio

            auto result = builder.CreateAudioSample(data, 1000000, 1000000);

            Assert::IsTrue(result.IsOk());
            Assert::IsNotNull(result.Value().get());
        }

        TEST_METHOD(CreateVideoSample_WithEmptyData_Fails)
        {
            SampleBuilder builder;
            std::vector<uint8_t> data;

            auto result = builder.CreateVideoSample(data, 0, 0);

            Assert::IsTrue(result.IsError());
        }

        TEST_METHOD(CreateAudioSample_WithEmptyData_Fails)
        {
            SampleBuilder builder;
            std::vector<uint8_t> data;

            auto result = builder.CreateAudioSample(data, 0, 0);

            Assert::IsTrue(result.IsError());
        }

        TEST_METHOD(CreateSample_WithSmallData_Succeeds)
        {
            SampleBuilder builder;
            std::vector<uint8_t> data(100, 0);

            auto result = builder.CreateVideoSample(data, 0, 333333);

            Assert::IsTrue(result.IsOk());
        }

        TEST_METHOD(CreateSample_WithLargeData_Succeeds)
        {
            SampleBuilder builder;
            std::vector<uint8_t> data(1920 * 1080 * 4, 0);

            auto result = builder.CreateVideoSample(data, 0, 333333);

            Assert::IsTrue(result.IsOk());
        }
    };
}
