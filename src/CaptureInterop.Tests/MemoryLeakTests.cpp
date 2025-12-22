#include "pch.h"
#include "CppUnitTest.h"
#include "WindowsMFMP4SinkWriter.h"
#include <dxgiformat.h>
#include <strsafe.h>
#include <wchar.h>
#include <d3d11.h>
#include <d3dcommon.h>
#include <Windows.h>
#include <psapi.h>
#include <vector>
#include <wil/com.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    /// <summary>
    /// Tests to detect memory leaks during video recording operations.
    /// These tests simulate extended recording sessions to identify memory accumulation.
    /// </summary>
    TEST_CLASS(MemoryLeakTests)
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
                // Fall back to WARP device for testing
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

        SIZE_T GetProcessMemoryUsage()
        {
            PROCESS_MEMORY_COUNTERS_EX pmc;
            if (GetProcessMemoryInfo(GetCurrentProcess(), (PROCESS_MEMORY_COUNTERS*)&pmc, sizeof(pmc)))
            {
                return pmc.WorkingSetSize;
            }
            return 0;
        }

    public:
        /// <summary>
        /// Test that memory usage stays stable during extended recording.
        /// This simulates a 60-second recording at 30fps (1800 frames).
        /// Memory should not grow continuously - it should stabilize after initial allocation.
        /// </summary>
        TEST_METHOD(WriteMultipleFrames_MemoryStaysStable)
        {
            auto device = CreateTestDevice();
            WindowsMFMP4SinkWriter writer;
            
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);
            wcscat_s(tempPath, L"test_memory_stable.mp4");
            
            HRESULT hr;
            bool initResult = writer.Initialize(tempPath, device.get(), 1280, 720, &hr);
            Assert::IsTrue(initResult, L"Initialize should succeed");
            
            auto texture = CreateTestTexture(device.get(), 1280, 720);
            
            const LONGLONG FRAME_DURATION = 333333; // ~30 FPS in 100ns units
            const int FRAME_COUNT = 1800; // 60 seconds @ 30fps
            const int SAMPLE_INTERVAL = 300; // Sample memory every 10 seconds
            
            std::vector<SIZE_T> memoryReadings;
            
            // Write frames and sample memory usage
            for (int i = 0; i < FRAME_COUNT; i++)
            {
                LONGLONG timestamp = i * FRAME_DURATION;
                hr = writer.WriteFrame(texture.get(), timestamp);
                Assert::IsTrue(SUCCEEDED(hr), L"WriteFrame should succeed");
                
                // Sample memory at intervals
                if (i % SAMPLE_INTERVAL == 0)
                {
                    SIZE_T memUsage = GetProcessMemoryUsage();
                    memoryReadings.push_back(memUsage);
                    
                    char msg[256];
                    sprintf_s(msg, "[MemoryTest] Frame %d: Memory usage = %.2f MB\n", 
                             i, memUsage / (1024.0 * 1024.0));
                    OutputDebugStringA(msg);
                }
            }
            
            // Verify memory isn't growing continuously
            // After initial allocation, memory should stabilize
            Assert::IsTrue(memoryReadings.size() >= 3, L"Need at least 3 memory samples");
            
            // Calculate memory growth between first and last samples
            SIZE_T initialMemory = memoryReadings[1]; // Skip first sample (cold start)
            SIZE_T finalMemory = memoryReadings.back();
            
            // Allow 20MB growth for buffering, but no more (leak would be hundreds of MB)
            const SIZE_T ACCEPTABLE_GROWTH = 20 * 1024 * 1024; // 20 MB
            SIZE_T growth = (finalMemory > initialMemory) ? (finalMemory - initialMemory) : 0;
            
            char summaryMsg[512];
            sprintf_s(summaryMsg, "[MemoryTest] Memory growth: %.2f MB (Initial: %.2f MB, Final: %.2f MB)\n",
                     growth / (1024.0 * 1024.0),
                     initialMemory / (1024.0 * 1024.0),
                     finalMemory / (1024.0 * 1024.0));
            OutputDebugStringA(summaryMsg);
            
            Assert::IsTrue(growth < ACCEPTABLE_GROWTH, 
                          L"Memory leak detected: memory grew more than 20MB during recording");
            
            // Cleanup
            writer.Finalize();
            DeleteFileW(tempPath);
        }

        /// <summary>
        /// Test that writing many frames doesn't leak staging textures.
        /// Before the fix, each frame would create a new staging texture causing memory to grow.
        /// </summary>
        TEST_METHOD(WriteFrames_NoStagingTextureLeaks)
        {
            auto device = CreateTestDevice();
            WindowsMFMP4SinkWriter writer;
            
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);
            wcscat_s(tempPath, L"test_no_staging_leak.mp4");
            
            writer.Initialize(tempPath, device.get(), 1920, 1080);
            
            auto texture = CreateTestTexture(device.get(), 1920, 1080);
            
            SIZE_T memBefore = GetProcessMemoryUsage();
            
            // Write 900 frames (30 seconds @ 30fps)
            const LONGLONG FRAME_DURATION = 333333;
            for (int i = 0; i < 900; i++)
            {
                LONGLONG timestamp = i * FRAME_DURATION;
                HRESULT hr = writer.WriteFrame(texture.get(), timestamp);
                Assert::IsTrue(SUCCEEDED(hr), L"WriteFrame should succeed");
            }
            
            SIZE_T memAfter = GetProcessMemoryUsage();
            
            // With 1920x1080x4 bytes per frame, if each frame leaked a staging texture,
            // we'd leak ~8MB per frame * 900 = ~7.2GB
            // Allow 50MB growth for encoder buffers, but memory leak would be much larger
            const SIZE_T MAX_ACCEPTABLE_GROWTH = 50 * 1024 * 1024; // 50 MB
            SIZE_T growth = (memAfter > memBefore) ? (memAfter - memBefore) : 0;
            
            char msg[256];
            sprintf_s(msg, "[NoLeakTest] Memory growth after 900 frames: %.2f MB\n", 
                     growth / (1024.0 * 1024.0));
            OutputDebugStringA(msg);
            
            Assert::IsTrue(growth < MAX_ACCEPTABLE_GROWTH, 
                          L"Staging texture memory leak detected");
            
            // Cleanup
            writer.Finalize();
            DeleteFileW(tempPath);
        }

        /// <summary>
        /// Test memory is properly released after Finalize.
        /// </summary>
        TEST_METHOD(Finalize_ReleasesMemory)
        {
            auto device = CreateTestDevice();
            
            SIZE_T memBefore = GetProcessMemoryUsage();
            
            {
                WindowsMFMP4SinkWriter writer;
                
                wchar_t tempPath[MAX_PATH];
                GetTempPathW(MAX_PATH, tempPath);
                wcscat_s(tempPath, L"test_finalize_memory.mp4");
                
                writer.Initialize(tempPath, device.get(), 1280, 720);
                
                auto texture = CreateTestTexture(device.get(), 1280, 720);
                
                // Write some frames
                const LONGLONG FRAME_DURATION = 333333;
                for (int i = 0; i < 100; i++)
                {
                    LONGLONG timestamp = i * FRAME_DURATION;
                    writer.WriteFrame(texture.get(), timestamp);
                }
                
                SIZE_T memDuringWrite = GetProcessMemoryUsage();
                
                // Finalize should release resources
                writer.Finalize();
                DeleteFileW(tempPath);
                
                SIZE_T memAfterFinalize = GetProcessMemoryUsage();
                
                char msg[512];
                sprintf_s(msg, "[FinalizeTest] Before: %.2f MB, During: %.2f MB, After: %.2f MB\n",
                         memBefore / (1024.0 * 1024.0),
                         memDuringWrite / (1024.0 * 1024.0),
                         memAfterFinalize / (1024.0 * 1024.0));
                OutputDebugStringA(msg);
                
                // Memory after finalize should be close to initial (allow 10MB margin)
                const SIZE_T ACCEPTABLE_RETENTION = 10 * 1024 * 1024;
                SIZE_T retained = (memAfterFinalize > memBefore) ? (memAfterFinalize - memBefore) : 0;
                
                Assert::IsTrue(retained < ACCEPTABLE_RETENTION,
                              L"Memory not properly released after Finalize");
            }
        }

        /// <summary>
        /// Test rapid start/stop cycles don't leak memory.
        /// </summary>
        TEST_METHOD(MultipleRecordingSessions_NoMemoryLeaks)
        {
            auto device = CreateTestDevice();
            
            SIZE_T memBefore = GetProcessMemoryUsage();
            
            // Simulate 10 recording sessions
            for (int session = 0; session < 10; session++)
            {
                WindowsMFMP4SinkWriter writer;
                
                wchar_t tempPath[MAX_PATH];
                GetTempPathW(MAX_PATH, tempPath);
                wchar_t sessionFile[MAX_PATH];
                swprintf_s(sessionFile, L"test_session_%d.mp4", session);
                wcscat_s(tempPath, sessionFile);
                
                writer.Initialize(tempPath, device.get(), 1280, 720);
                
                auto texture = CreateTestTexture(device.get(), 1280, 720);
                
                // Write 60 frames per session (2 seconds)
                const LONGLONG FRAME_DURATION = 333333;
                for (int i = 0; i < 60; i++)
                {
                    LONGLONG timestamp = i * FRAME_DURATION;
                    writer.WriteFrame(texture.get(), timestamp);
                }
                
                writer.Finalize();
                DeleteFileW(tempPath);
            }
            
            SIZE_T memAfter = GetProcessMemoryUsage();
            
            // After 10 sessions, memory should be similar to initial
            // Allow 30MB growth for various caches
            const SIZE_T ACCEPTABLE_GROWTH = 30 * 1024 * 1024;
            SIZE_T growth = (memAfter > memBefore) ? (memAfter - memBefore) : 0;
            
            char msg[256];
            sprintf_s(msg, "[MultiSessionTest] Memory growth after 10 sessions: %.2f MB\n",
                     growth / (1024.0 * 1024.0));
            OutputDebugStringA(msg);
            
            Assert::IsTrue(growth < ACCEPTABLE_GROWTH,
                          L"Memory leak detected across multiple recording sessions");
        }
    };
}
