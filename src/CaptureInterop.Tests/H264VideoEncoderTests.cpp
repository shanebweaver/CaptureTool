#include "pch.h"
#include "CppUnitTest.h"
#include "../CaptureInterop/H264VideoEncoder.h"
#include "../CaptureInterop/EncoderPresets.h"
#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop;

namespace CaptureInteropTests
{
    // Sub-Task 2: H264VideoEncoder Tests
    // Tests the H.264 video encoder with hardware/software paths

    TEST_CLASS(H264VideoEncoderTests)
    {
    private:
        // Helper method to create a synthetic NV12 Media Foundation sample
        static HRESULT CreateNV12Sample(uint32_t width, uint32_t height, IMFSample** ppSample)
        {
            HRESULT hr = S_OK;
            IMFMediaBuffer* pBuffer = nullptr;
            IMFSample* pSample = nullptr;

            // Calculate NV12 buffer size (Y plane + UV plane)
            DWORD bufferSize = width * height * 3 / 2; // NV12 is 12 bits per pixel

            // Create Media Foundation buffer
            hr = MFCreateMemoryBuffer(bufferSize, &pBuffer);
            if (FAILED(hr)) return hr;

            // Lock buffer and fill with test pattern (gray color)
            BYTE* pData = nullptr;
            DWORD maxLength = 0, currentLength = 0;
            hr = pBuffer->Lock(&pData, &maxLength, &currentLength);
            if (SUCCEEDED(hr))
            {
                // Fill Y plane with 128 (gray)
                memset(pData, 128, width * height);
                
                // Fill UV plane with 128 (neutral chroma)
                memset(pData + width * height, 128, width * height / 2);
                
                pBuffer->Unlock();
                pBuffer->SetCurrentLength(bufferSize);
            }

            // Create sample and add buffer
            hr = MFCreateSample(&pSample);
            if (SUCCEEDED(hr))
            {
                hr = pSample->AddBuffer(pBuffer);
            }

            if (pBuffer) pBuffer->Release();

            if (SUCCEEDED(hr))
            {
                *ppSample = pSample;
            }
            else if (pSample)
            {
                pSample->Release();
            }

            return hr;
        }

    public:
        TEST_CLASS_INITIALIZE(ClassInitialize)
        {
            // Initialize Media Foundation for all tests
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
            VideoEncoderConfig config = EncoderPresets::CreateBalancedVideoPreset(1920, 1080, 30);
            config.hardwareAcceleration = true;
            
            H264VideoEncoder encoder;

            // Act
            HRESULT hr = encoder.Initialize(config);

            // Assert
            Assert::IsTrue(SUCCEEDED(hr), L"Hardware encoder initialization should succeed or fallback to software");
            
            // Check if hardware was actually used (may fallback to software)
            EncoderStatistics stats = encoder.GetStatistics();
            // Note: isHardwareAccelerated flag indicates actual encoder type used
        }

        TEST_METHOD(H264Encoder_InitializeWithSoftware_Success)
        {
            // Arrange
            VideoEncoderConfig config = EncoderPresets::CreateBalancedVideoPreset(1920, 1080, 30);
            config.hardwareAcceleration = false; // Force software
            
            H264VideoEncoder encoder;

            // Act
            HRESULT hr = encoder.Initialize(config);

            // Assert
            Assert::IsTrue(SUCCEEDED(hr), L"Software encoder initialization should succeed");
        }

        TEST_METHOD(H264Encoder_InitializeInvalidResolution_Fails)
        {
            // Arrange
            VideoEncoderConfig config = EncoderPresets::CreateBalancedVideoPreset(0, 0, 30);
            H264VideoEncoder encoder;

            // Act
            HRESULT hr = encoder.Initialize(config);

            // Assert
            Assert::IsTrue(FAILED(hr), L"Encoder should fail with 0x0 resolution");
        }

        TEST_METHOD(H264Encoder_InitializeInvalidFrameRate_Fails)
        {
            // Arrange
            VideoEncoderConfig config = EncoderPresets::CreateBalancedVideoPreset(1920, 1080, 0);
            H264VideoEncoder encoder;

            // Act
            HRESULT hr = encoder.Initialize(config);

            // Assert
            Assert::IsTrue(FAILED(hr), L"Encoder should fail with 0 fps");
        }

        // 2.2 Encoding Tests

        TEST_METHOD(H264Encoder_EncodeNV12Sample_Success)
        {
            // Arrange
            VideoEncoderConfig config = EncoderPresets::CreateFastVideoPreset(1920, 1080, 30);
            H264VideoEncoder encoder;
            HRESULT hr = encoder.Initialize(config);
            Assert::IsTrue(SUCCEEDED(hr), L"Encoder initialization failed");

            IMFSample* pInputSample = nullptr;
            hr = CreateNV12Sample(1920, 1080, &pInputSample);
            Assert::IsTrue(SUCCEEDED(hr), L"Failed to create test sample");
            Assert::IsNotNull(pInputSample, L"Sample should not be null");

            pInputSample->SetSampleTime(0);
            pInputSample->SetSampleDuration(333333); // ~30fps

            IMFSample* pOutputSample = nullptr;

            // Act
            hr = encoder.Encode(pInputSample, &pOutputSample);

            // Assert
            Assert::IsTrue(SUCCEEDED(hr), L"Encoding should succeed");
            Assert::IsNotNull(pOutputSample, L"Output sample should not be null");

            // Verify output has data
            DWORD bufferCount = 0;
            pOutputSample->GetBufferCount(&bufferCount);
            Assert::IsTrue(bufferCount > 0, L"Output should have at least one buffer");

            IMFMediaBuffer* pBuffer = nullptr;
            pOutputSample->GetBufferByIndex(0, &pBuffer);
            if (pBuffer)
            {
                DWORD length = 0;
                pBuffer->GetCurrentLength(&length);
                Assert::IsTrue(length > 0, L"Encoded data should have non-zero size");
                pBuffer->Release();
            }

            // Cleanup
            if (pOutputSample) pOutputSample->Release();
            if (pInputSample) pInputSample->Release();
            encoder.Shutdown();
        }

        TEST_METHOD(H264Encoder_EncodeMultipleFrames_Success)
        {
            // Arrange
            VideoEncoderConfig config = EncoderPresets::CreateFastVideoPreset(1280, 720, 30);
            H264VideoEncoder encoder;
            HRESULT hr = encoder.Initialize(config);
            Assert::IsTrue(SUCCEEDED(hr), L"Encoder initialization failed");

            const int frameCount = 100;
            int successCount = 0;

            // Act - Encode 100 frames
            for (int i = 0; i < frameCount; i++)
            {
                IMFSample* pInputSample = nullptr;
                hr = CreateNV12Sample(1280, 720, &pInputSample);
                if (FAILED(hr)) continue;

                LONGLONG timestamp = i * 333333LL; // 30fps timing
                pInputSample->SetSampleTime(timestamp);
                pInputSample->SetSampleDuration(333333);

                IMFSample* pOutputSample = nullptr;
                hr = encoder.Encode(pInputSample, &pOutputSample);
                
                if (SUCCEEDED(hr) && pOutputSample)
                {
                    successCount++;
                    
                    // Verify timestamp progression
                    LONGLONG outputTime = 0;
                    pOutputSample->GetSampleTime(&outputTime);
                    // Allow some tolerance for encoding delays
                    Assert::IsTrue(outputTime >= 0, L"Output timestamp should be valid");
                    
                    pOutputSample->Release();
                }

                pInputSample->Release();
            }

            // Assert - Most frames should encode successfully
            Assert::IsTrue(successCount >= frameCount * 0.9, L"At least 90% of frames should encode successfully");

            // Verify statistics
            EncoderStatistics stats = encoder.GetStatistics();
            Assert::IsTrue(stats.framesEncoded >= successCount * 0.9, L"Statistics should track encoded frames");

            encoder.Shutdown();
        }

        TEST_METHOD(H264Encoder_EncodeNullSample_Fails)
        {
            // Arrange
            VideoEncoderConfig config = EncoderPresets::CreateFastVideoPreset(1920, 1080, 30);
            H264VideoEncoder encoder;
            HRESULT hr = encoder.Initialize(config);
            Assert::IsTrue(SUCCEEDED(hr), L"Encoder initialization failed");

            IMFSample* pOutputSample = nullptr;

            // Act
            hr = encoder.Encode(nullptr, &pOutputSample);

            // Assert
            Assert::IsTrue(FAILED(hr) || pOutputSample == nullptr, L"Encoding null sample should fail");

            encoder.Shutdown();
        }

        TEST_METHOD(H264Encoder_EncodeAfterShutdown_Fails)
        {
            // Arrange
            VideoEncoderConfig config = EncoderPresets::CreateFastVideoPreset(1920, 1080, 30);
            H264VideoEncoder encoder;
            HRESULT hr = encoder.Initialize(config);
            Assert::IsTrue(SUCCEEDED(hr), L"Encoder initialization failed");

            // Shutdown encoder
            encoder.Shutdown();

            // Create sample
            IMFSample* pInputSample = nullptr;
            hr = CreateNV12Sample(1920, 1080, &pInputSample);
            Assert::IsTrue(SUCCEEDED(hr), L"Failed to create test sample");

            IMFSample* pOutputSample = nullptr;

            // Act
            hr = encoder.Encode(pInputSample, &pOutputSample);

            // Assert
            Assert::IsTrue(FAILED(hr) || pOutputSample == nullptr, L"Encoding after shutdown should fail");

            if (pInputSample) pInputSample->Release();
        }

        // 2.3 Preset Tests

        TEST_METHOD(H264Encoder_FastPreset_ProducesLowBitrate)
        {
            // Arrange
            VideoEncoderConfig fastConfig = EncoderPresets::CreateFastVideoPreset(1920, 1080, 30);
            VideoEncoderConfig balancedConfig = EncoderPresets::CreateBalancedVideoPreset(1920, 1080, 30);

            // Assert - Fast preset should have lower bitrate than Balanced
            Assert::IsTrue(fastConfig.bitrate < balancedConfig.bitrate, 
                L"Fast preset should have lower bitrate than Balanced preset");
        }

        TEST_METHOD(H264Encoder_QualityPreset_ProducesHighBitrate)
        {
            // Arrange
            VideoEncoderConfig qualityConfig = EncoderPresets::CreateQualityVideoPreset(1920, 1080, 30);
            VideoEncoderConfig balancedConfig = EncoderPresets::CreateBalancedVideoPreset(1920, 1080, 30);

            // Assert - Quality preset should have higher bitrate than Balanced
            Assert::IsTrue(qualityConfig.bitrate > balancedConfig.bitrate, 
                L"Quality preset should have higher bitrate than Balanced preset");
        }

        TEST_METHOD(H264Encoder_LosslessPreset_HasMaximumBitrate)
        {
            // Arrange
            VideoEncoderConfig losslessConfig = EncoderPresets::CreateLosslessVideoPreset(1920, 1080, 30);
            VideoEncoderConfig qualityConfig = EncoderPresets::CreateQualityVideoPreset(1920, 1080, 30);

            // Assert - Lossless should have highest bitrate or be 0 (uncompressed)
            Assert::IsTrue(losslessConfig.bitrate == 0 || losslessConfig.bitrate >= qualityConfig.bitrate, 
                L"Lossless preset should have maximum or zero bitrate");
        }

        // 2.4 Statistics Tests

        TEST_METHOD(H264Encoder_StatisticsTracking)
        {
            // Arrange
            VideoEncoderConfig config = EncoderPresets::CreateFastVideoPreset(1280, 720, 30);
            H264VideoEncoder encoder;
            HRESULT hr = encoder.Initialize(config);
            Assert::IsTrue(SUCCEEDED(hr), L"Encoder initialization failed");

            const int frameCount = 50;

            // Act - Encode 50 frames
            for (int i = 0; i < frameCount; i++)
            {
                IMFSample* pInputSample = nullptr;
                hr = CreateNV12Sample(1280, 720, &pInputSample);
                if (FAILED(hr)) continue;

                pInputSample->SetSampleTime(i * 333333LL);
                pInputSample->SetSampleDuration(333333);

                IMFSample* pOutputSample = nullptr;
                hr = encoder.Encode(pInputSample, &pOutputSample);
                
                if (pOutputSample) pOutputSample->Release();
                pInputSample->Release();
            }

            // Assert
            EncoderStatistics stats = encoder.GetStatistics();
            Assert::IsTrue(stats.framesEncoded > 0, L"Should have encoded at least some frames");
            Assert::IsTrue(stats.framesEncoded <= frameCount, L"Encoded frame count should not exceed input count");

            encoder.Shutdown();
        }

        TEST_METHOD(H264Encoder_LatencyMeasurement)
        {
            // Arrange
            VideoEncoderConfig config = EncoderPresets::CreateFastVideoPreset(1920, 1080, 30);
            H264VideoEncoder encoder;
            HRESULT hr = encoder.Initialize(config);
            Assert::IsTrue(SUCCEEDED(hr), L"Encoder initialization failed");

            IMFSample* pInputSample = nullptr;
            hr = CreateNV12Sample(1920, 1080, &pInputSample);
            Assert::IsTrue(SUCCEEDED(hr), L"Failed to create test sample");

            pInputSample->SetSampleTime(0);
            pInputSample->SetSampleDuration(333333);

            // Act - Measure encoding time
            auto startTime = std::chrono::high_resolution_clock::now();
            
            IMFSample* pOutputSample = nullptr;
            hr = encoder.Encode(pInputSample, &pOutputSample);
            
            auto endTime = std::chrono::high_resolution_clock::now();
            auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(endTime - startTime);

            // Assert
            Assert::IsTrue(SUCCEEDED(hr), L"Encoding should succeed");
            
            // Target: < 5ms for 1080p30 (may not always be met depending on hardware)
            // We'll use 50ms as a generous upper bound for the test
            Assert::IsTrue(duration.count() < 50, L"Encoding latency should be reasonable (<50ms)");

            // Cleanup
            if (pOutputSample) pOutputSample->Release();
            if (pInputSample) pInputSample->Release();
            encoder.Shutdown();
        }

        TEST_METHOD(H264Encoder_HardwareFallbackToSoftware)
        {
            // Arrange - Request hardware but may fallback
            VideoEncoderConfig config = EncoderPresets::CreateBalancedVideoPreset(1920, 1080, 30);
            config.hardwareAcceleration = true;
            
            H264VideoEncoder encoder;

            // Act
            HRESULT hr = encoder.Initialize(config);

            // Assert - Should succeed regardless of hardware availability
            Assert::IsTrue(SUCCEEDED(hr), L"Encoder should initialize with hardware or fallback to software");

            // If initialization succeeded, verify encoding works
            if (SUCCEEDED(hr))
            {
                IMFSample* pInputSample = nullptr;
                hr = CreateNV12Sample(1920, 1080, &pInputSample);
                if (SUCCEEDED(hr))
                {
                    pInputSample->SetSampleTime(0);
                    pInputSample->SetSampleDuration(333333);

                    IMFSample* pOutputSample = nullptr;
                    hr = encoder.Encode(pInputSample, &pOutputSample);
                    
                    Assert::IsTrue(SUCCEEDED(hr), L"Encoding should work with either hardware or software");
                    
                    if (pOutputSample) pOutputSample->Release();
                    pInputSample->Release();
                }
                
                encoder.Shutdown();
            }
        }

        TEST_METHOD(H264Encoder_MultipleResolutions)
        {
            // Test encoding at different resolutions
            struct ResolutionTest
            {
                uint32_t width;
                uint32_t height;
                const wchar_t* name;
            };

            ResolutionTest resolutions[] = {
                { 1280, 720, L"720p" },
                { 1920, 1080, L"1080p" },
                { 2560, 1440, L"1440p" }
            };

            for (const auto& res : resolutions)
            {
                // Arrange
                VideoEncoderConfig config = EncoderPresets::CreateFastVideoPreset(res.width, res.height, 30);
                H264VideoEncoder encoder;
                
                // Act
                HRESULT hr = encoder.Initialize(config);
                
                // Assert
                Assert::IsTrue(SUCCEEDED(hr), (std::wstring(L"Should initialize for ") + res.name).c_str());
                
                if (SUCCEEDED(hr))
                {
                    // Try encoding one frame
                    IMFSample* pInputSample = nullptr;
                    hr = CreateNV12Sample(res.width, res.height, &pInputSample);
                    
                    if (SUCCEEDED(hr))
                    {
                        pInputSample->SetSampleTime(0);
                        pInputSample->SetSampleDuration(333333);

                        IMFSample* pOutputSample = nullptr;
                        hr = encoder.Encode(pInputSample, &pOutputSample);
                        
                        Assert::IsTrue(SUCCEEDED(hr), 
                            (std::wstring(L"Should encode frame for ") + res.name).c_str());
                        
                        if (pOutputSample) pOutputSample->Release();
                        pInputSample->Release();
                    }
                    
                    encoder.Shutdown();
                }
            }
        }
    };
}
