#include "pch.h"
#include "CppUnitTest.h"
#include "MP4SinkWriter.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    TEST_CLASS(MP4SinkWriterTests)
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

    public:
        TEST_METHOD(Constructor_CreatesInstance)
        {
            MP4SinkWriter* writer = new MP4SinkWriter();
            Assert::IsNotNull(writer);
            delete writer;
        }

        TEST_METHOD(AddRef_Release_ManagesReferenceCount)
        {
            MP4SinkWriter* writer = new MP4SinkWriter();
            
            ULONG ref1 = writer->AddRef();
            Assert::AreEqual(2UL, ref1);
            
            ULONG ref2 = writer->AddRef();
            Assert::AreEqual(3UL, ref2);
            
            ULONG ref3 = writer->Release();
            Assert::AreEqual(2UL, ref3);
            
            ULONG ref4 = writer->Release();
            Assert::AreEqual(1UL, ref4);
            
            writer->Release(); // Final release deletes the object
        }

        TEST_METHOD(Initialize_WithValidParameters_Succeeds)
        {
            auto device = CreateTestDevice();
            MP4SinkWriter writer;
            
            HRESULT hr;
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);
            wcscat_s(tempPath, L"test_video.mp4");
            
            bool result = writer.Initialize(tempPath, device.get(), 1280, 720, &hr);
            
            Assert::IsTrue(result, L"Initialize should succeed");
            Assert::AreEqual(S_OK, hr);
            
            // Cleanup
            writer.Finalize();
            DeleteFileW(tempPath);
        }

        TEST_METHOD(SetRecordingStartTime_StoresValue)
        {
            MP4SinkWriter writer;
            LONGLONG testTime = 123456789LL;
            
            writer.SetRecordingStartTime(testTime);
            
            Assert::AreEqual(testTime, writer.GetRecordingStartTime());
        }

        TEST_METHOD(GetRecordingStartTime_DefaultsToZero)
        {
            MP4SinkWriter writer;
            
            Assert::AreEqual(0LL, writer.GetRecordingStartTime());
        }

        TEST_METHOD(InitializeAudioStream_WithValidFormat_Succeeds)
        {
            auto device = CreateTestDevice();
            MP4SinkWriter writer;
            
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);
            wcscat_s(tempPath, L"test_audio.mp4");
            
            HRESULT hr;
            bool initResult = writer.Initialize(tempPath, device.get(), 1280, 720, &hr);
            Assert::IsTrue(initResult);
            
            WAVEFORMATEX audioFormat = CreateTestAudioFormat();
            bool audioResult = writer.InitializeAudioStream(&audioFormat, &hr);
            
            Assert::IsTrue(audioResult, L"InitializeAudioStream should succeed");
            Assert::AreEqual(S_OK, hr);
            
            // Cleanup
            writer.Finalize();
            DeleteFileW(tempPath);
        }

        TEST_METHOD(InitializeAudioStream_WithNullFormat_Fails)
        {
            auto device = CreateTestDevice();
            MP4SinkWriter writer;
            
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);
            wcscat_s(tempPath, L"test_null_audio.mp4");
            
            writer.Initialize(tempPath, device.get(), 1280, 720);
            
            HRESULT hr;
            bool result = writer.InitializeAudioStream(nullptr, &hr);
            
            Assert::IsFalse(result, L"InitializeAudioStream should fail with null format");
            Assert::AreEqual(E_INVALIDARG, hr);
            
            // Cleanup
            writer.Finalize();
            DeleteFileW(tempPath);
        }

        TEST_METHOD(WriteFrame_WithValidTexture_Succeeds)
        {
            auto device = CreateTestDevice();
            MP4SinkWriter writer;
            
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);
            wcscat_s(tempPath, L"test_frame.mp4");
            
            writer.Initialize(tempPath, device.get(), 1280, 720);
            
            auto texture = CreateTestTexture(device.get(), 1280, 720);
            LONGLONG timestamp = 0;
            
            HRESULT hr = writer.WriteFrame(texture.get(), timestamp);
            
            Assert::IsTrue(SUCCEEDED(hr), L"WriteFrame should succeed");
            
            // Cleanup
            writer.Finalize();
            DeleteFileW(tempPath);
        }

        TEST_METHOD(WriteFrame_WithNullTexture_Fails)
        {
            auto device = CreateTestDevice();
            MP4SinkWriter writer;
            
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);
            wcscat_s(tempPath, L"test_null_frame.mp4");
            
            writer.Initialize(tempPath, device.get(), 1280, 720);
            
            HRESULT hr = writer.WriteFrame(nullptr, 0);
            
            Assert::AreEqual(E_FAIL, hr);
            
            // Cleanup
            writer.Finalize();
            DeleteFileW(tempPath);
        }

        TEST_METHOD(WriteAudioSample_WithValidData_Succeeds)
        {
            auto device = CreateTestDevice();
            MP4SinkWriter writer;
            
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);
            wcscat_s(tempPath, L"test_audio_sample.mp4");
            
            writer.Initialize(tempPath, device.get(), 1280, 720);
            
            WAVEFORMATEX audioFormat = CreateTestAudioFormat();
            writer.InitializeAudioStream(&audioFormat);
            
            // Create dummy audio data
            const UINT32 numFrames = 480; // 10ms at 48kHz
            const UINT32 bufferSize = numFrames * audioFormat.nBlockAlign;
            std::vector<BYTE> audioData(bufferSize, 0);
            
            HRESULT hr = writer.WriteAudioSample(audioData.data(), numFrames, 0);
            
            Assert::IsTrue(SUCCEEDED(hr), L"WriteAudioSample should succeed");
            
            // Cleanup
            writer.Finalize();
            DeleteFileW(tempPath);
        }

        TEST_METHOD(WriteAudioSample_WithoutAudioStream_Fails)
        {
            auto device = CreateTestDevice();
            MP4SinkWriter writer;
            
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);
            wcscat_s(tempPath, L"test_no_audio.mp4");
            
            writer.Initialize(tempPath, device.get(), 1280, 720);
            
            BYTE dummyData[100] = {};
            HRESULT hr = writer.WriteAudioSample(dummyData, 10, 0);
            
            Assert::AreEqual(E_FAIL, hr);
            
            // Cleanup
            writer.Finalize();
            DeleteFileW(tempPath);
        }

        TEST_METHOD(Finalize_CanBeCalledMultipleTimes)
        {
            auto device = CreateTestDevice();
            MP4SinkWriter writer;
            
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);
            wcscat_s(tempPath, L"test_finalize.mp4");
            
            writer.Initialize(tempPath, device.get(), 1280, 720);
            
            // Should not crash when called multiple times
            writer.Finalize();
            writer.Finalize();
            writer.Finalize();
            
            DeleteFileW(tempPath);
        }

        TEST_METHOD(WriteMultipleFrames_MaintainsTimestamps)
        {
            auto device = CreateTestDevice();
            MP4SinkWriter writer;
            
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);
            wcscat_s(tempPath, L"test_multiple_frames.mp4");
            
            writer.Initialize(tempPath, device.get(), 640, 480);
            
            auto texture = CreateTestTexture(device.get(), 640, 480);
            
            const LONGLONG FRAME_DURATION = 333333; // ~30 FPS in 100ns units
            
            for (int i = 0; i < 10; i++)
            {
                LONGLONG timestamp = i * FRAME_DURATION;
                HRESULT hr = writer.WriteFrame(texture.get(), timestamp);
                Assert::IsTrue(SUCCEEDED(hr), L"WriteFrame should succeed for all frames");
            }
            
            // Cleanup
            writer.Finalize();
            DeleteFileW(tempPath);
        }
    };
}
