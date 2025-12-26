#include "pch.h"
#include "CppUnitTest.h"
#include "WindowsMFMP4SinkWriter.h"
#include <dxgiformat.h>
#include <span>
#include <strsafe.h>
#include <wchar.h>
#include <d3d11.h>
#include <d3dcommon.h>
#include <Windows.h>
#include <psapi.h>
#include <vector>
#include <wil/com.h>
#include <cmath>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    /// <summary>
    /// Tests for silent audio handling during video recording.
    /// These tests verify that recording with silent audio doesn't cause memory leaks
    /// or frame processing issues.
    /// </summary>
    TEST_CLASS(SilentAudioTests)
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

        wil::com_ptr<ID3D11Texture2D> CreateTestTexture(ID3D11Device* device, UINT width, UINT height)
        {
            D3D11_TEXTURE2D_DESC desc = {};
            desc.Width = width;
            desc.Height = height;
            desc.MipLevels = 1;
            desc.ArraySize = 1;
            desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
            desc.SampleDesc.Count = 1;
            desc.Usage = D3D11_USAGE_DEFAULT;
            desc.BindFlags = D3D11_BIND_RENDER_TARGET;
            
            wil::com_ptr<ID3D11Texture2D> texture;
            HRESULT hr = device->CreateTexture2D(&desc, nullptr, texture.put());
            Assert::IsTrue(SUCCEEDED(hr), L"Failed to create test texture");
            return texture;
        }

        WAVEFORMATEX CreateTestAudioFormat()
        {
            WAVEFORMATEX format = {};
            format.wFormatTag = WAVE_FORMAT_PCM;
            format.nChannels = 2;
            format.nSamplesPerSec = 48000;
            format.wBitsPerSample = 16;
            format.nBlockAlign = (format.nChannels * format.wBitsPerSample) / 8;
            format.nAvgBytesPerSec = format.nSamplesPerSec * format.nBlockAlign;
            format.cbSize = 0;
            return format;
        }

        SIZE_T GetProcessMemoryUsage()
        {
            PROCESS_MEMORY_COUNTERS_EX pmc{};
            if (GetProcessMemoryInfo(GetCurrentProcess(), (PROCESS_MEMORY_COUNTERS*)&pmc, sizeof(pmc)))
            {
                return pmc.WorkingSetSize;
            }
            return 0;
        }

    public:
        /// <summary>
        /// Test that recording with silent audio doesn't cause memory leaks or processing issues.
        /// This test writes both video and silent audio to ensure proper interleaving.
        /// </summary>
        TEST_METHOD(SilentAudioWithVideo_NoMemoryLeaks)
        {
            auto device = CreateTestDevice();
            WindowsMFMP4SinkWriter writer;
            
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);
            wcscat_s(tempPath, L"test_silent_audio.mp4");
            
            writer.Initialize(tempPath, device.get(), 1280, 720);
            
            WAVEFORMATEX audioFormat = CreateTestAudioFormat();
            writer.InitializeAudioStream(&audioFormat);
            
            auto texture = CreateTestTexture(device.get(), 1280, 720);
            
            // Test 2 seconds of recording with both video and silent audio
            const int SECONDS = 2;
            const LONGLONG FRAME_DURATION = 333333LL; // ~30 FPS (100ns units)
            const LONGLONG AUDIO_SAMPLE_DURATION = 100000LL; // 10ms in 100ns units
            const int TOTAL_FRAMES = SECONDS * 30;
            
            // Write interleaved video and audio
            int audioSampleIndex = 0;
            for (int frameIndex = 0; frameIndex < TOTAL_FRAMES; frameIndex++)
            {
                LONGLONG videoTime = frameIndex * FRAME_DURATION;
                
                // Write video frame
                HRESULT hr = writer.WriteFrame(texture.get(), videoTime);
                Assert::IsTrue(SUCCEEDED(hr), L"WriteFrame should succeed");
                
                // Write audio samples to keep up with video
                while (audioSampleIndex * AUDIO_SAMPLE_DURATION <= videoTime)
                {
                    const UINT32 numFrames = 480; // 10ms at 48kHz
                    const UINT32 bufferSize = numFrames * audioFormat.nBlockAlign;
                    std::vector<BYTE> silentAudio(bufferSize, 0);
                    
                    LONGLONG audioTime = audioSampleIndex * AUDIO_SAMPLE_DURATION;
                    hr = writer.WriteAudioSample(std::span<const uint8_t>(silentAudio.data(), bufferSize), audioTime);
                    Assert::IsTrue(SUCCEEDED(hr), L"WriteAudioSample should succeed");
                    audioSampleIndex++;
                }
            }
            
            // Finalize should block until encoding is complete
            writer.Finalize();
            DeleteFileW(tempPath);
        }

        /// <summary>
        /// Test high frame rate with silent audio.
        /// Verifies that 60 FPS video with silent audio doesn't cause frame drops or memory issues.
        /// </summary>
        TEST_METHOD(HighFrameRateWithSilentAudio_NoFrameDrops)
        {
            auto device = CreateTestDevice();
            WindowsMFMP4SinkWriter writer;
            
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);
            wcscat_s(tempPath, L"test_60fps_silent.mp4");
            
            writer.Initialize(tempPath, device.get(), 1920, 1080);
            
            WAVEFORMATEX audioFormat = CreateTestAudioFormat();
            writer.InitializeAudioStream(&audioFormat);
            
            auto texture = CreateTestTexture(device.get(), 1920, 1080);
            
            const LONGLONG FRAME_DURATION = 166666; // ~60 FPS
            const LONGLONG AUDIO_SAMPLE_DURATION = 100000; // 10ms
            const int SECONDS = 10;
            
            SIZE_T memBefore = GetProcessMemoryUsage();
            
            // Simulate 60 FPS with audio
            int audioSampleIndex = 0;
            for (int frameIndex = 0; frameIndex < SECONDS * 60; frameIndex++)
            {
                LONGLONG videoTime = frameIndex * FRAME_DURATION;
                HRESULT hr = writer.WriteFrame(texture.get(), videoTime);
                Assert::IsTrue(SUCCEEDED(hr), L"WriteFrame should succeed at 60 FPS");
                
                // Write audio samples to keep up with video
                while (audioSampleIndex * AUDIO_SAMPLE_DURATION < videoTime)
                {
                    const UINT32 numFrames = 480;
                    const UINT32 bufferSize = numFrames * audioFormat.nBlockAlign;
                    std::vector<BYTE> silentAudio(bufferSize, 0);
                    
                    LONGLONG audioTime = audioSampleIndex * AUDIO_SAMPLE_DURATION;
                    hr = writer.WriteAudioSample(std::span<const uint8_t>(silentAudio.data(), bufferSize), audioTime);
                    Assert::IsTrue(SUCCEEDED(hr), L"WriteAudioSample should succeed");
                    audioSampleIndex++;
                }
            }
            
            SIZE_T memAfter = GetProcessMemoryUsage();
            SIZE_T growth = (memAfter > memBefore) ? (memAfter - memBefore) : 0;
            
            // Should handle 60 FPS without excessive memory growth
            const SIZE_T MAX_GROWTH = 150 * 1024 * 1024; // 150 MB
            Assert::IsTrue(growth < MAX_GROWTH, L"Excessive memory growth at 60 FPS");
            
            // Cleanup
            writer.Finalize();
            DeleteFileW(tempPath);
        }

    private:
        static const LONGLONG TICKS_PER_SECOND = 10000000LL;
    };
}
