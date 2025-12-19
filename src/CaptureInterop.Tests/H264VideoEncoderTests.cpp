#include "pch.h"
#include "CppUnitTest.h"
#include "../CaptureInterop/H264VideoEncoder.h"
#include "../CaptureInterop/EncoderPresets.h"
#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <d3d11.h>
#include <chrono>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop;

namespace CaptureInteropTests
{
    // Sub-Task 2: H264VideoEncoder Tests
    // Tests the H.264 video encoder with hardware/software paths

    TEST_CLASS(H264VideoEncoderTests)
    {
    private:
        // Helper method to create a D3D11 device
        static HRESULT CreateD3D11Device(ID3D11Device** ppDevice)
        {
            D3D_FEATURE_LEVEL featureLevels[] = {
                D3D_FEATURE_LEVEL_11_1,
                D3D_FEATURE_LEVEL_11_0
            };

            return D3D11CreateDevice(
                nullptr,
                D3D_DRIVER_TYPE_HARDWARE,
                nullptr,
                D3D11_CREATE_DEVICE_VIDEO_SUPPORT,
                featureLevels,
                ARRAYSIZE(featureLevels),
                D3D11_SDK_VERSION,
                ppDevice,
                nullptr,
                nullptr
            );
        }

        // Helper method to create a test texture
        static HRESULT CreateTestTexture(ID3D11Device* pDevice, UINT32 width, UINT32 height, ID3D11Texture2D** ppTexture)
        {
            D3D11_TEXTURE2D_DESC desc = {};
            desc.Width = width;
            desc.Height = height;
            desc.MipLevels = 1;
            desc.ArraySize = 1;
            desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
            desc.SampleDesc.Count = 1;
            desc.Usage = D3D11_USAGE_DEFAULT;
            desc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;

            return pDevice->CreateTexture2D(&desc, nullptr, ppTexture);
        }

    public:
        TEST_CLASS_INITIALIZE(ClassInitialize)
        {
            // Initialize Media Foundation and COM for all tests
            CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
            MFStartup(MF_VERSION);
        }

        TEST_CLASS_CLEANUP(ClassCleanup)
        {
            // Shutdown Media Foundation
            MFShutdown();
            CoUninitialize();
        }

        // 2.1 Initialization Tests

        TEST_METHOD(H264Encoder_InitializeWithHardware_Success)
        {
            // Arrange
            VideoEncoderConfig config = {};
            config.codec = VideoCodec::H264;
            config.width = 1920;
            config.height = 1080;
            config.frameRateNum = 30;
            config.frameRateDen = 1;
            config.preset = EncoderPreset::Balanced;
            config.hardwareAcceleration = true;
            config.bitrate = 0; // Auto
            
            H264VideoEncoder* pEncoder = new H264VideoEncoder();

            // Act
            bool initResult = pEncoder->Initialize();
            HRESULT configResult = pEncoder->Configure(config);
            bool startResult = pEncoder->Start();

            // Assert
            Assert::IsTrue(initResult, L"Initialize should succeed");
            Assert::IsTrue(SUCCEEDED(configResult), L"Configure should succeed");
            Assert::IsTrue(startResult, L"Start should succeed");
            
            // Cleanup
            pEncoder->Stop();
            pEncoder->Release();
        }

        TEST_METHOD(H264Encoder_InitializeWithSoftware_Success)
        {
            // Arrange
            VideoEncoderConfig config = {};
            config.codec = VideoCodec::H264;
            config.width = 1920;
            config.height = 1080;
            config.frameRateNum = 30;
            config.frameRateDen = 1;
            config.preset = EncoderPreset::Balanced;
            config.hardwareAcceleration = false; // Force software
            config.bitrate = 0; // Auto
            
            H264VideoEncoder* pEncoder = new H264VideoEncoder();

            // Act
            bool initResult = pEncoder->Initialize();
            HRESULT configResult = pEncoder->Configure(config);
            bool startResult = pEncoder->Start();

            // Assert
            Assert::IsTrue(initResult, L"Initialize should succeed");
            Assert::IsTrue(SUCCEEDED(configResult), L"Configure should succeed");
            Assert::IsTrue(startResult, L"Start should succeed");

            // Cleanup
            pEncoder->Stop();
            pEncoder->Release();
        }

        TEST_METHOD(H264Encoder_ConfigureInvalidResolution_Fails)
        {
            // Arrange
            VideoEncoderConfig config = {};
            config.codec = VideoCodec::H264;
            config.width = 0;
            config.height = 0;
            config.frameRateNum = 30;
            config.frameRateDen = 1;
            
            H264VideoEncoder* pEncoder = new H264VideoEncoder();
            pEncoder->Initialize();

            // Act
            HRESULT hr = pEncoder->Configure(config);

            // Assert
            Assert::IsTrue(FAILED(hr), L"Configure should fail with 0x0 resolution");

            // Cleanup
            pEncoder->Release();
        }

        TEST_METHOD(H264Encoder_ConfigureWrongCodec_Fails)
        {
            // Arrange
            VideoEncoderConfig config = {};
            config.codec = VideoCodec::H265; // Wrong codec
            config.width = 1920;
            config.height = 1080;
            config.frameRateNum = 30;
            config.frameRateDen = 1;
            
            H264VideoEncoder* pEncoder = new H264VideoEncoder();
            pEncoder->Initialize();

            // Act
            HRESULT hr = pEncoder->Configure(config);

            // Assert
            Assert::IsTrue(FAILED(hr), L"Configure should fail with wrong codec");

            // Cleanup
            pEncoder->Release();
        }

        // 2.2 Encoding Tests

        TEST_METHOD(H264Encoder_EncodeFrame_Success)
        {
            // Arrange
            ID3D11Device* pDevice = nullptr;
            HRESULT hr = CreateD3D11Device(&pDevice);
            Assert::IsTrue(SUCCEEDED(hr), L"Failed to create D3D11 device");

            VideoEncoderConfig config = {};
            config.codec = VideoCodec::H264;
            config.width = 1920;
            config.height = 1080;
            config.frameRateNum = 30;
            config.frameRateDen = 1;
            config.preset = EncoderPreset::Fast;
            config.hardwareAcceleration = false;
            config.bitrate = 0;
            
            H264VideoEncoder* pEncoder = new H264VideoEncoder();
            pEncoder->Initialize();
            pEncoder->Configure(config);
            pEncoder->Start();

            ID3D11Texture2D* pTexture = nullptr;
            hr = CreateTestTexture(pDevice, 1920, 1080, &pTexture);
            Assert::IsTrue(SUCCEEDED(hr), L"Failed to create test texture");

            IMFSample* pOutputSample = nullptr;

            // Act
            hr = pEncoder->EncodeFrame(pTexture, 0, &pOutputSample);

            // Assert
            // Note: First frame might return S_FALSE (need more input)
            Assert::IsTrue(hr == S_OK || hr == S_FALSE, L"EncodeFrame should return S_OK or S_FALSE");

            // Cleanup
            if (pOutputSample) pOutputSample->Release();
            if (pTexture) pTexture->Release();
            pEncoder->Stop();
            pEncoder->Release();
            if (pDevice) pDevice->Release();
        }

        TEST_METHOD(H264Encoder_GetCapabilities_Success)
        {
            // Arrange
            H264VideoEncoder* pEncoder = new H264VideoEncoder();
            pEncoder->Initialize();

            VideoEncoderCapabilities caps = {};

            // Act
            HRESULT hr = pEncoder->GetCapabilities(&caps);

            // Assert
            Assert::IsTrue(SUCCEEDED(hr), L"GetCapabilities should succeed");
            Assert::IsTrue(caps.supportsH264, L"Should support H264");
            Assert::IsTrue(caps.maxWidth > 0, L"Max width should be positive");
            Assert::IsTrue(caps.maxHeight > 0, L"Max height should be positive");

            // Cleanup
            pEncoder->Release();
        }

        TEST_METHOD(H264Encoder_StatsTracking_Success)
        {
            // Arrange
            ID3D11Device* pDevice = nullptr;
            HRESULT hr = CreateD3D11Device(&pDevice);
            if (FAILED(hr))
            {
                // Skip test if D3D11 device creation fails
                Logger::WriteMessage(L"D3D11 device creation failed - skipping test");
                return;
            }

            VideoEncoderConfig config = {};
            config.codec = VideoCodec::H264;
            config.width = 1280;
            config.height = 720;
            config.frameRateNum = 30;
            config.frameRateDen = 1;
            config.preset = EncoderPreset::Fast;
            config.hardwareAcceleration = false;
            config.bitrate = 0;
            
            H264VideoEncoder* pEncoder = new H264VideoEncoder();
            pEncoder->Initialize();
            pEncoder->Configure(config);
            pEncoder->Start();

            ID3D11Texture2D* pTexture = nullptr;
            hr = CreateTestTexture(pDevice, 1280, 720, &pTexture);
            if (FAILED(hr))
            {
                pEncoder->Release();
                pDevice->Release();
                // Skip test if texture creation fails
                Logger::WriteMessage(L"Texture creation failed - skipping test");
                return;
            }

            // Act - Encode multiple frames
            const int frameCount = 10;
            for (int i = 0; i < frameCount; i++)
            {
                IMFSample* pOutputSample = nullptr;
                pEncoder->EncodeFrame(pTexture, i * 333333LL, &pOutputSample);
                if (pOutputSample) pOutputSample->Release();
            }

            // Assert
            uint64_t encodedCount = pEncoder->GetEncodedFrameCount();
            Assert::IsTrue(encodedCount > 0, L"Should have encoded at least some frames");

            // Cleanup
            if (pTexture) pTexture->Release();
            pEncoder->Stop();
            pEncoder->Release();
            if (pDevice) pDevice->Release();
        }

        // 2.3 Preset Tests

        TEST_METHOD(H264Encoder_FastPreset_LowerBitrate)
        {
            // Arrange - Compare calculated bitrates
            uint32_t fastBitrate = EncoderPresets::CalculateVideoBitrate(
                EncoderPreset::Fast, 1920, 1080, 30);
            uint32_t balancedBitrate = EncoderPresets::CalculateVideoBitrate(
                EncoderPreset::Balanced, 1920, 1080, 30);

            // Assert
            Assert::IsTrue(fastBitrate < balancedBitrate, 
                L"Fast preset should have lower bitrate than Balanced");
        }

        TEST_METHOD(H264Encoder_QualityPreset_HigherBitrate)
        {
            // Arrange
            uint32_t qualityBitrate = EncoderPresets::CalculateVideoBitrate(
                EncoderPreset::Quality, 1920, 1080, 30);
            uint32_t balancedBitrate = EncoderPresets::CalculateVideoBitrate(
                EncoderPreset::Balanced, 1920, 1080, 30);

            // Assert
            Assert::IsTrue(qualityBitrate > balancedBitrate, 
                L"Quality preset should have higher bitrate than Balanced");
        }

        TEST_METHOD(H264Encoder_SupportsCodec_H264Only)
        {
            // Arrange
            H264VideoEncoder* pEncoder = new H264VideoEncoder();

            // Act & Assert
            Assert::IsTrue(pEncoder->SupportsCodec(VideoCodec::H264), 
                L"Should support H264");
            Assert::IsFalse(pEncoder->SupportsCodec(VideoCodec::H265), 
                L"Should not support H265");
            Assert::IsFalse(pEncoder->SupportsCodec(VideoCodec::VP9), 
                L"Should not support VP9");

            // Cleanup
            pEncoder->Release();
        }
    };
}
