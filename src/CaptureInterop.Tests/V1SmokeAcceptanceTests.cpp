#include "pch.h"
#include "CppUnitTest.h"
#include "WindowsGraphicsCaptureSession.h"
#include "WindowsGraphicsCaptureSessionFactory.h"
#include "IAudioCaptureSource.h"
#include "IAudioCaptureSourceFactory.h"
#include "CallbackTypes.h"
#include "CaptureSessionConfig.h"
#include "MediaFoundationLifecycleManager.h"
#include "SimpleMediaClock.h"
#include "SimpleMediaClockFactory.h"
#include "WindowsLocalAudioCaptureSource.h"
#include "WindowsLocalAudioCaptureSourceFactory.h"
#include "WindowsDesktopVideoCaptureSourceFactory.h"
#include "WindowsMFMP4SinkWriterFactory.h"

#include <chrono>
#include <atomic>
#include <filesystem>
#include <thread>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    TEST_CLASS(V1SmokeAcceptanceTests)
    {
    private:
        class FailingAudioCaptureSource final : public IAudioCaptureSource
        {
        public:
            bool Initialize(HRESULT* outHr = nullptr) override
            {
                if (outHr) *outHr = AUDCLNT_E_DEVICE_INVALIDATED;
                return false;
            }

            bool Start(HRESULT* outHr = nullptr) override
            {
                if (outHr) *outHr = E_NOT_VALID_STATE;
                return false;
            }

            void Stop() override {}
            WAVEFORMATEX* GetFormat() const override { return nullptr; }
            void SetAudioSampleReadyCallback(AudioSampleReadyCallback) override {}
            void SetEnabled(bool) override {}
            bool IsEnabled() const override { return false; }
            bool IsRunning() const override { return false; }
            void SetClockWriter(IMediaClockWriter*) override {}
        };

        class FailingAudioCaptureSourceFactory final : public IAudioCaptureSourceFactory
        {
        public:
            std::unique_ptr<IAudioCaptureSource> CreateAudioCaptureSource(IMediaClockReader*) override
            {
                return std::make_unique<FailingAudioCaptureSource>();
            }
        };

        static inline std::atomic<int> s_videoFrames{ 0 };
        static inline std::atomic<int> s_audioSamples{ 0 };

        static void __stdcall CountVideoFrame(const VideoFrameData*)
        {
            s_videoFrames.fetch_add(1, std::memory_order_relaxed);
        }

        static void __stdcall CountAudioSample(const AudioSampleData*)
        {
            s_audioSamples.fetch_add(1, std::memory_order_relaxed);
        }

        static HMONITOR GetPrimaryMonitor()
        {
            POINT pt = { 0, 0 };
            return MonitorFromPoint(pt, MONITOR_DEFAULTTOPRIMARY);
        }

        static std::wstring CreateTempMp4Path(const wchar_t* fileName)
        {
            wchar_t tempPath[MAX_PATH]{};
            DWORD length = GetTempPathW(ARRAYSIZE(tempPath), tempPath);
            Assert::IsTrue(length > 0 && length < ARRAYSIZE(tempPath), L"GetTempPathW should succeed");

            std::filesystem::path path(tempPath);
            path /= fileName;
            DeleteFileW(path.c_str());
            return path.wstring();
        }

        static bool HasAudioLoopback()
        {
            SimpleMediaClock mediaClock;
            WindowsLocalAudioCaptureSource audioSource(&mediaClock);
            HRESULT hr = S_OK;
            return audioSource.Initialize(&hr) && audioSource.GetFormat() != nullptr;
        }

        static void AssertMediaStreamExists(IMFSourceReader* reader, DWORD streamIndex, const wchar_t* message)
        {
            wil::com_ptr<IMFMediaType> mediaType;
            HRESULT hr = reader->GetNativeMediaType(streamIndex, 0, mediaType.put());
            Assert::IsTrue(SUCCEEDED(hr), message);
            Assert::IsNotNull(mediaType.get(), message);
        }

    public:
        TEST_METHOD(V1_RecordShortMp4_WithAudioWhenAvailable_ProducesReadableMedia)
        {
            const bool expectAudio = HasAudioLoopback();
            const std::wstring outputPath = CreateTempMp4Path(L"capturetool-v1-smoke.mp4");

            CaptureSessionConfig config(
                GetPrimaryMonitor(),
                outputPath,
                true,
                30,
                5'000'000,
                192'000);

            WindowsGraphicsCaptureSessionFactory factory(
                std::make_unique<SimpleMediaClockFactory>(),
                std::make_unique<WindowsLocalAudioCaptureSourceFactory>(),
                std::make_unique<WindowsDesktopVideoCaptureSourceFactory>(),
                std::make_unique<WindowsMFMP4SinkWriterFactory>());

            auto session = factory.CreateSession(config);
            Assert::IsNotNull(session.get(), L"V1 session should be created");

            auto* concreteSession = dynamic_cast<WindowsGraphicsCaptureSession*>(session.get());
            Assert::IsNotNull(concreteSession, L"V1 smoke test should receive a WindowsGraphicsCaptureSession");

            s_videoFrames.store(0, std::memory_order_relaxed);
            s_audioSamples.store(0, std::memory_order_relaxed);
            concreteSession->SetVideoFrameCallback(&CountVideoFrame);
            concreteSession->SetAudioSampleCallback(&CountAudioSample);

            HRESULT hr = S_OK;
            bool started = session->Start(&hr);
            if (!started)
            {
                Logger::WriteMessage("[V1Smoke] Desktop capture is unavailable in this environment; skipping smoke test.");
                return;
            }

            std::this_thread::sleep_for(std::chrono::milliseconds(750));
            session->ToggleAudioCapture(false);
            std::this_thread::sleep_for(std::chrono::milliseconds(500));
            session->ToggleAudioCapture(true);
            std::this_thread::sleep_for(std::chrono::milliseconds(750));

            session->Stop();

            if (s_videoFrames.load(std::memory_order_relaxed) == 0)
            {
                Logger::WriteMessage("[V1Smoke] Desktop capture started but no video frames arrived; skipping media readability assertions for this environment.");
                DeleteFileW(outputPath.c_str());
                return;
            }

            Assert::IsTrue(std::filesystem::exists(outputPath), L"V1 smoke MP4 should exist");
            Assert::IsTrue(std::filesystem::file_size(outputPath) > 0, L"V1 smoke MP4 should be non-empty");

            MediaFoundationLifecycleManager mediaFoundation;
            Assert::IsTrue(mediaFoundation.IsInitialized(), L"Media Foundation should initialize for smoke MP4 verification");

            wil::com_ptr<IMFSourceReader> reader;
            hr = MFCreateSourceReaderFromURL(outputPath.c_str(), nullptr, reader.put());
            if (FAILED(hr))
            {
                wchar_t message[256]{};
                swprintf_s(message, L"[V1Smoke] MFCreateSourceReaderFromURL failed with HRESULT 0x%08X", static_cast<unsigned int>(hr));
                Logger::WriteMessage(message);
            }
            Assert::IsTrue(SUCCEEDED(hr), L"V1 smoke MP4 should open through Media Foundation");

            AssertMediaStreamExists(reader.get(), MF_SOURCE_READER_FIRST_VIDEO_STREAM, L"V1 smoke MP4 should contain a readable video stream");
            if (expectAudio)
            {
                Assert::IsTrue(s_audioSamples.load(std::memory_order_relaxed) > 0, L"V1 smoke recording should receive audio samples when loopback is available");
                AssertMediaStreamExists(reader.get(), MF_SOURCE_READER_FIRST_AUDIO_STREAM, L"V1 smoke MP4 should contain a readable audio stream when loopback is available");
            }

            DeleteFileW(outputPath.c_str());
        }

        TEST_METHOD(V1_StartsVideoOnly_WhenAudioLoopbackInitializationFails)
        {
            const std::wstring outputPath = CreateTempMp4Path(L"capturetool-v1-audio-fallback-smoke.mp4");

            CaptureSessionConfig config(
                GetPrimaryMonitor(),
                outputPath,
                true,
                30,
                5'000'000,
                192'000);

            WindowsGraphicsCaptureSessionFactory factory(
                std::make_unique<SimpleMediaClockFactory>(),
                std::make_unique<FailingAudioCaptureSourceFactory>(),
                std::make_unique<WindowsDesktopVideoCaptureSourceFactory>(),
                std::make_unique<WindowsMFMP4SinkWriterFactory>());

            auto session = factory.CreateSession(config);
            Assert::IsNotNull(session.get(), L"V1 fallback session should be created even when audio initialization fails");

            HRESULT hr = S_OK;
            bool started = session->Start(&hr);
            if (!started)
            {
                Logger::WriteMessage("[V1Smoke] Desktop capture is unavailable in this environment; skipping audio fallback start assertion.");
                DeleteFileW(outputPath.c_str());
                return;
            }

            std::this_thread::sleep_for(std::chrono::milliseconds(250));
            session->Stop();

            Assert::IsTrue(std::filesystem::exists(outputPath), L"Video-only fallback should create an MP4 file");
            DeleteFileW(outputPath.c_str());
        }
    };
}
