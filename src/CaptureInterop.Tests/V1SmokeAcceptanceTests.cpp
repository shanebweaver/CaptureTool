#include "pch.h"
#include "CppUnitTest.h"
#include "WindowsGraphicsCaptureSession.h"
#include "WindowsGraphicsCaptureSessionFactory.h"
#include "IAudioCaptureSource.h"
#include "IAudioCaptureSourceFactory.h"
#include "IVideoCaptureSource.h"
#include "CallbackTypes.h"
#include "CaptureSessionConfig.h"
#include "MediaFoundationLifecycleManager.h"
#include "MediaTimeConstants.h"
#include "SimpleMediaClock.h"
#include "SimpleMediaClockFactory.h"
#include "WindowsLocalAudioCaptureSource.h"
#include "WindowsLocalAudioCaptureSourceFactory.h"
#include "WindowsDesktopVideoCaptureSourceFactory.h"
#include "WindowsMFMP4SinkWriter.h"
#include "WindowsMFMP4SinkWriterFactory.h"

#include <chrono>
#include <atomic>
#include <filesystem>
#include <psapi.h>
#include <thread>
#include <vector>

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
            void SetVolume(uint32_t) override {}
            bool IsRunning() const override { return false; }
            bool SetInputDeviceId(const wchar_t*, HRESULT* outHr = nullptr) override
            {
                if (outHr) *outHr = AUDCLNT_E_DEVICE_INVALIDATED;
                return false;
            }
            void SetClockWriter(IMediaClockWriter*) override {}
        };

        class FailingAudioCaptureSourceFactory final : public IAudioCaptureSourceFactory
        {
        public:
            std::unique_ptr<IAudioCaptureSource> CreateAudioCaptureSource(IMediaClockReader*, const std::wstring&) override
            {
                return std::make_unique<FailingAudioCaptureSource>();
            }
        };

        class StartFailingAudioCaptureSource final : public IAudioCaptureSource
        {
        public:
            bool Initialize(HRESULT* outHr = nullptr) override
            {
                if (outHr) *outHr = S_OK;
                return true;
            }

            bool Start(HRESULT* outHr = nullptr) override
            {
                if (outHr) *outHr = AUDCLNT_E_DEVICE_INVALIDATED;
                return false;
            }

            void Stop() override {}
            WAVEFORMATEX* GetFormat() const override { return const_cast<WAVEFORMATEX*>(&m_format); }
            void SetAudioSampleReadyCallback(AudioSampleReadyCallback callback) override { m_callback = std::move(callback); }
            void SetEnabled(bool enabled) override { m_isEnabled.store(enabled); }
            bool IsEnabled() const override { return m_isEnabled.load(); }
            void SetVolume(uint32_t) override {}
            bool IsRunning() const override { return false; }
            bool SetInputDeviceId(const wchar_t*, HRESULT* outHr = nullptr) override
            {
                if (outHr) *outHr = AUDCLNT_E_DEVICE_INVALIDATED;
                return false;
            }
            void SetClockWriter(IMediaClockWriter*) override {}
            bool HasCallback() const { return static_cast<bool>(m_callback); }

        private:
            WAVEFORMATEX m_format{
                WAVE_FORMAT_PCM,
                2,
                48000,
                48000 * 4,
                4,
                16,
                0
            };
            AudioSampleReadyCallback m_callback;
            std::atomic<bool> m_isEnabled{ true };
        };

        class SyntheticAudioCaptureSource final : public IAudioCaptureSource
        {
        public:
            bool Initialize(HRESULT* outHr = nullptr) override
            {
                if (outHr) *outHr = S_OK;
                return true;
            }

            bool Start(HRESULT* outHr = nullptr) override
            {
                if (!m_clockWriter || m_isRunning.load())
                {
                    if (outHr) *outHr = E_NOT_VALID_STATE;
                    return false;
                }

                m_isRunning.store(true);
                m_audioThread = std::thread([this] {
                    constexpr UINT32 FramesPerPacket = 480;
                    std::vector<uint8_t> silence(FramesPerPacket * m_format.nBlockAlign, 0);

                    while (m_isRunning.load())
                    {
                        if (m_callback)
                        {
                            AudioSampleReadyEventArgs args{};
                            args.data = std::span<const uint8_t>(silence.data(), silence.size());
                            args.timestamp = m_timestamp;
                            args.pFormat = &m_format;
                            m_callback(args);
                        }

                        m_clockWriter->AdvanceByAudioSamples(FramesPerPacket, m_format.nSamplesPerSec);
                        m_timestamp += MediaTimeConstants::TicksFromAudioFrames(FramesPerPacket, m_format.nSamplesPerSec);
                        std::this_thread::sleep_for(std::chrono::milliseconds(10));
                    }
                });

                if (outHr) *outHr = S_OK;
                return true;
            }

            void Stop() override
            {
                m_isRunning.store(false);
                if (m_audioThread.joinable())
                {
                    m_audioThread.join();
                }
            }

            WAVEFORMATEX* GetFormat() const override { return const_cast<WAVEFORMATEX*>(&m_format); }
            void SetAudioSampleReadyCallback(AudioSampleReadyCallback callback) override { m_callback = std::move(callback); }
            void SetEnabled(bool enabled) override { m_isEnabled.store(enabled); }
            bool IsEnabled() const override { return m_isEnabled.load(); }
            void SetVolume(uint32_t) override {}
            bool IsRunning() const override { return m_isRunning.load(); }
            bool SetInputDeviceId(const wchar_t*, HRESULT* outHr = nullptr) override
            {
                if (outHr) *outHr = S_OK;
                return true;
            }
            void SetClockWriter(IMediaClockWriter* clockWriter) override { m_clockWriter = clockWriter; }

        private:
            WAVEFORMATEX m_format{
                WAVE_FORMAT_PCM,
                2,
                48000,
                48000 * 4,
                4,
                16,
                0
            };
            AudioSampleReadyCallback m_callback;
            IMediaClockWriter* m_clockWriter = nullptr;
            std::thread m_audioThread;
            std::atomic<bool> m_isRunning{ false };
            std::atomic<bool> m_isEnabled{ true };
            LONGLONG m_timestamp = 0;
        };

        class SyntheticVideoCaptureSource final : public IVideoCaptureSource
        {
        public:
            bool Initialize(HRESULT* outHr = nullptr) override
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
                    m_device.put(),
                    &featureLevel,
                    m_context.put());
                if (FAILED(hr))
                {
                    if (outHr) *outHr = hr;
                    return false;
                }

                D3D11_TEXTURE2D_DESC desc{};
                desc.Width = Width;
                desc.Height = Height;
                desc.MipLevels = 1;
                desc.ArraySize = 1;
                desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
                desc.SampleDesc.Count = 1;
                desc.Usage = D3D11_USAGE_DEFAULT;
                desc.BindFlags = D3D11_BIND_RENDER_TARGET;

                hr = m_device->CreateTexture2D(&desc, nullptr, m_texture.put());
                if (FAILED(hr))
                {
                    if (outHr) *outHr = hr;
                    return false;
                }

                if (outHr) *outHr = S_OK;
                return true;
            }

            bool Start(HRESULT* outHr = nullptr) override
            {
                if (!m_callback || m_isRunning.load())
                {
                    if (outHr) *outHr = E_NOT_VALID_STATE;
                    return false;
                }

                m_isRunning.store(true);
                m_videoThread = std::thread([this] {
                    while (m_isRunning.load())
                    {
                        VideoFrameReadyEventArgs args{};
                        args.pTexture = m_texture.get();
                        args.timestamp = GetTimestamp();
                        m_callback(args);
                        std::this_thread::sleep_for(std::chrono::milliseconds(33));
                    }
                });

                if (outHr) *outHr = S_OK;
                return true;
            }

            void Stop() override
            {
                m_isRunning.store(false);
                if (m_videoThread.joinable())
                {
                    m_videoThread.join();
                }
            }

            UINT32 GetWidth() const override { return Width; }
            UINT32 GetHeight() const override { return Height; }
            ID3D11Device* GetDevice() const override { return m_device.get(); }
            void SetVideoFrameReadyCallback(VideoFrameReadyCallback callback) override { m_callback = std::move(callback); }
            bool IsRunning() const override { return m_isRunning.load(); }

        private:
            static constexpr UINT32 Width = 320;
            static constexpr UINT32 Height = 240;

            LONGLONG GetTimestamp() const
            {
                if (!m_clockReader || !m_clockReader->IsRunning())
                {
                    return 0;
                }

                LONGLONG timestamp = m_clockReader->GetCurrentTime();
                if (timestamp > 0)
                {
                    return timestamp;
                }

                LARGE_INTEGER qpc{};
                QueryPerformanceCounter(&qpc);
                return m_clockReader->GetRelativeTime(qpc.QuadPart);
            }

            IMediaClockReader* m_clockReader = nullptr;
            wil::com_ptr<ID3D11Device> m_device;
            wil::com_ptr<ID3D11DeviceContext> m_context;
            wil::com_ptr<ID3D11Texture2D> m_texture;
            VideoFrameReadyCallback m_callback;
            std::thread m_videoThread;
            std::atomic<bool> m_isRunning{ false };

        public:
            explicit SyntheticVideoCaptureSource(IMediaClockReader* clockReader)
                : m_clockReader(clockReader)
            {
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

        static void AssertMediaSampleReadable(IMFSourceReader* reader, DWORD streamIndex, const wchar_t* message)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                DWORD actualStreamIndex = 0;
                DWORD streamFlags = 0;
                LONGLONG timestamp = 0;
                wil::com_ptr<IMFSample> sample;
                HRESULT hr = reader->ReadSample(
                    streamIndex,
                    0,
                    &actualStreamIndex,
                    &streamFlags,
                    &timestamp,
                    sample.put());

                Assert::IsTrue(SUCCEEDED(hr), message);
                if ((streamFlags & MF_SOURCE_READERF_ENDOFSTREAM) != 0)
                {
                    break;
                }

                if (sample)
                {
                    DWORD bufferCount = 0;
                    hr = sample->GetBufferCount(&bufferCount);
                    Assert::IsTrue(SUCCEEDED(hr), message);
                    Assert::IsTrue(bufferCount > 0, message);
                    return;
                }
            }

            Assert::Fail(message);
        }

        static void AssertMediaStreamDoesNotExist(IMFSourceReader* reader, DWORD streamIndex, const wchar_t* message)
        {
            wil::com_ptr<IMFMediaType> mediaType;
            HRESULT hr = reader->GetNativeMediaType(streamIndex, 0, mediaType.put());
            Assert::IsFalse(SUCCEEDED(hr), message);
        }

        static SIZE_T GetCurrentWorkingSetBytes()
        {
            PROCESS_MEMORY_COUNTERS counters{};
            counters.cb = sizeof(counters);
            BOOL ok = GetProcessMemoryInfo(GetCurrentProcess(), &counters, sizeof(counters));
            Assert::IsTrue(ok != FALSE, L"GetProcessMemoryInfo should succeed");
            return counters.WorkingSetSize;
        }

        static bool IsRealWgcMemorySmokeEnabled()
        {
            wchar_t value[16]{};
            DWORD length = GetEnvironmentVariableW(L"CAPTURETOOL_RUN_REAL_WGC_MEMORY_SMOKE", value, ARRAYSIZE(value));
            return length > 0 && wcscmp(value, L"1") == 0;
        }

        static DWORD GetRealWgcMemorySmokeDurationSeconds()
        {
            wchar_t value[16]{};
            DWORD length = GetEnvironmentVariableW(L"CAPTURETOOL_REAL_WGC_MEMORY_SMOKE_SECONDS", value, ARRAYSIZE(value));
            if (length == 0)
            {
                return 180;
            }

            wchar_t* end = nullptr;
            unsigned long parsed = wcstoul(value, &end, 10);
            if (end == value || parsed < 30 || parsed > 600)
            {
                return 180;
            }

            return static_cast<DWORD>(parsed);
        }

    public:
        TEST_METHOD(V1_SyntheticSources_ProduceReadableAudioVideoMp4)
        {
            const std::wstring outputPath = CreateTempMp4Path(L"capturetool-v1-synthetic-av.mp4");
            auto mediaClock = std::make_unique<SimpleMediaClock>();
            auto* clockReader = mediaClock.get();

            CaptureSessionConfig config(
                GetPrimaryMonitor(),
                outputPath,
                true,
                30,
                5'000'000,
                192'000);

            auto session = std::make_unique<WindowsGraphicsCaptureSession>(
                config,
                std::move(mediaClock),
                std::make_unique<SyntheticAudioCaptureSource>(),
                std::make_unique<SyntheticVideoCaptureSource>(clockReader),
                std::make_unique<WindowsMFMP4SinkWriter>());

            HRESULT hr = S_OK;
            Assert::IsTrue(session->Initialize(&hr), L"Synthetic V1 session should initialize");
            Assert::IsTrue(session->Start(&hr), L"Synthetic V1 session should start");

            std::this_thread::sleep_for(std::chrono::seconds(2));
            session->Stop();

            Assert::IsTrue(std::filesystem::exists(outputPath), L"Synthetic V1 MP4 should exist");
            Assert::IsTrue(std::filesystem::file_size(outputPath) > 0, L"Synthetic V1 MP4 should be non-empty");

            MediaFoundationLifecycleManager mediaFoundation;
            Assert::IsTrue(mediaFoundation.IsInitialized(), L"Media Foundation should initialize for synthetic MP4 verification");

            wil::com_ptr<IMFSourceReader> reader;
            hr = MFCreateSourceReaderFromURL(outputPath.c_str(), nullptr, reader.put());
            Assert::IsTrue(SUCCEEDED(hr), L"Synthetic V1 MP4 should open through Media Foundation");
            AssertMediaStreamExists(reader.get(), MF_SOURCE_READER_FIRST_VIDEO_STREAM, L"Synthetic V1 MP4 should contain video");
            AssertMediaStreamExists(reader.get(), MF_SOURCE_READER_FIRST_AUDIO_STREAM, L"Synthetic V1 MP4 should contain audio");
            AssertMediaSampleReadable(reader.get(), MF_SOURCE_READER_FIRST_VIDEO_STREAM, L"Synthetic V1 MP4 should have readable video samples");
            AssertMediaSampleReadable(reader.get(), MF_SOURCE_READER_FIRST_AUDIO_STREAM, L"Synthetic V1 MP4 should have readable audio samples");

            DeleteFileW(outputPath.c_str());
        }

        TEST_METHOD(V1_SyntheticSources_SustainedRecording_DoesNotGrowWorkingSetUnbounded)
        {
            const std::wstring outputPath = CreateTempMp4Path(L"capturetool-v1-synthetic-memory.mp4");
            auto mediaClock = std::make_unique<SimpleMediaClock>();
            auto* clockReader = mediaClock.get();

            CaptureSessionConfig config(
                GetPrimaryMonitor(),
                outputPath,
                true,
                30,
                5'000'000,
                192'000);

            auto session = std::make_unique<WindowsGraphicsCaptureSession>(
                config,
                std::move(mediaClock),
                std::make_unique<SyntheticAudioCaptureSource>(),
                std::make_unique<SyntheticVideoCaptureSource>(clockReader),
                std::make_unique<WindowsMFMP4SinkWriter>());

            HRESULT hr = S_OK;
            Assert::IsTrue(session->Initialize(&hr), L"Synthetic V1 memory session should initialize");
            Assert::IsTrue(session->Start(&hr), L"Synthetic V1 memory session should start");

            std::this_thread::sleep_for(std::chrono::seconds(2));
            SIZE_T warmWorkingSet = GetCurrentWorkingSetBytes();

            std::this_thread::sleep_for(std::chrono::seconds(5));
            SIZE_T finalWorkingSet = GetCurrentWorkingSetBytes();
            session->Stop();

            const SIZE_T delta = finalWorkingSet > warmWorkingSet ? finalWorkingSet - warmWorkingSet : 0;
            constexpr SIZE_T MaxAllowedGrowthBytes = 96ull * 1024ull * 1024ull;

            wchar_t message[256]{};
            swprintf_s(
                message,
                L"[V1Smoke] Synthetic sustained recording working-set delta after warm-up: %zu bytes",
                static_cast<size_t>(delta));
            Logger::WriteMessage(message);

            Assert::IsTrue(delta <= MaxAllowedGrowthBytes, L"Synthetic V1 sustained recording should not grow working set unbounded after warm-up");
            Assert::IsTrue(std::filesystem::exists(outputPath), L"Synthetic sustained MP4 should exist");
            Assert::IsTrue(std::filesystem::file_size(outputPath) > 0, L"Synthetic sustained MP4 should be non-empty");

            DeleteFileW(outputPath.c_str());
        }

        TEST_METHOD(V1_StartsVideoOnly_WhenAudioLoopbackStartFails_ProducesReadableVideoOnlyMp4)
        {
            const std::wstring outputPath = CreateTempMp4Path(L"capturetool-v1-audio-start-fallback.mp4");
            auto mediaClock = std::make_unique<SimpleMediaClock>();
            auto* clockReader = mediaClock.get();

            CaptureSessionConfig config(
                GetPrimaryMonitor(),
                outputPath,
                true,
                30,
                5'000'000,
                192'000);

            auto session = std::make_unique<WindowsGraphicsCaptureSession>(
                config,
                std::move(mediaClock),
                std::make_unique<StartFailingAudioCaptureSource>(),
                std::make_unique<SyntheticVideoCaptureSource>(clockReader),
                std::make_unique<WindowsMFMP4SinkWriter>());

            HRESULT hr = S_OK;
            Assert::IsTrue(session->Initialize(&hr), L"V1 fallback session should initialize when loopback has a format");
            Assert::IsTrue(session->Start(&hr), L"V1 fallback session should start video-only when loopback start fails");

            std::this_thread::sleep_for(std::chrono::seconds(1));
            session->Stop();

            Assert::IsTrue(std::filesystem::exists(outputPath), L"Audio start fallback MP4 should exist");
            Assert::IsTrue(std::filesystem::file_size(outputPath) > 0, L"Audio start fallback MP4 should be non-empty");

            MediaFoundationLifecycleManager mediaFoundation;
            Assert::IsTrue(mediaFoundation.IsInitialized(), L"Media Foundation should initialize for fallback MP4 verification");

            wil::com_ptr<IMFSourceReader> reader;
            hr = MFCreateSourceReaderFromURL(outputPath.c_str(), nullptr, reader.put());
            Assert::IsTrue(SUCCEEDED(hr), L"Audio start fallback MP4 should open through Media Foundation");
            AssertMediaStreamExists(reader.get(), MF_SOURCE_READER_FIRST_VIDEO_STREAM, L"Audio start fallback MP4 should contain video");
            AssertMediaSampleReadable(reader.get(), MF_SOURCE_READER_FIRST_VIDEO_STREAM, L"Audio start fallback MP4 should have readable video samples");
            AssertMediaStreamDoesNotExist(reader.get(), MF_SOURCE_READER_FIRST_AUDIO_STREAM, L"Audio start fallback MP4 should not declare audio");

            DeleteFileW(outputPath.c_str());
        }

        TEST_METHOD(V1_StartsVideoOnly_WhenAudioLoopbackInitializationFails_ProducesReadableVideoOnlyMp4)
        {
            const std::wstring outputPath = CreateTempMp4Path(L"capturetool-v1-audio-init-fallback.mp4");
            auto mediaClock = std::make_unique<SimpleMediaClock>();
            auto* clockReader = mediaClock.get();

            CaptureSessionConfig config(
                GetPrimaryMonitor(),
                outputPath,
                true,
                30,
                5'000'000,
                192'000);

            auto session = std::make_unique<WindowsGraphicsCaptureSession>(
                config,
                std::move(mediaClock),
                std::make_unique<FailingAudioCaptureSource>(),
                std::make_unique<SyntheticVideoCaptureSource>(clockReader),
                std::make_unique<WindowsMFMP4SinkWriter>());

            HRESULT hr = S_OK;
            Assert::IsTrue(session->Initialize(&hr), L"V1 fallback session should initialize when loopback initialization fails");
            Assert::IsTrue(session->Start(&hr), L"V1 fallback session should start video-only when loopback initialization fails");

            std::this_thread::sleep_for(std::chrono::seconds(1));
            session->Stop();

            Assert::IsTrue(std::filesystem::exists(outputPath), L"Audio initialization fallback MP4 should exist");
            Assert::IsTrue(std::filesystem::file_size(outputPath) > 0, L"Audio initialization fallback MP4 should be non-empty");

            MediaFoundationLifecycleManager mediaFoundation;
            Assert::IsTrue(mediaFoundation.IsInitialized(), L"Media Foundation should initialize for initialization fallback MP4 verification");

            wil::com_ptr<IMFSourceReader> reader;
            hr = MFCreateSourceReaderFromURL(outputPath.c_str(), nullptr, reader.put());
            Assert::IsTrue(SUCCEEDED(hr), L"Audio initialization fallback MP4 should open through Media Foundation");
            AssertMediaStreamExists(reader.get(), MF_SOURCE_READER_FIRST_VIDEO_STREAM, L"Audio initialization fallback MP4 should contain video");
            AssertMediaSampleReadable(reader.get(), MF_SOURCE_READER_FIRST_VIDEO_STREAM, L"Audio initialization fallback MP4 should have readable video samples");
            AssertMediaStreamDoesNotExist(reader.get(), MF_SOURCE_READER_FIRST_AUDIO_STREAM, L"Audio initialization fallback MP4 should not declare audio");

            DeleteFileW(outputPath.c_str());
        }

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
            if (!session)
            {
                Logger::WriteMessage("[V1Smoke] Desktop capture session could not be created in this environment; skipping real WGC smoke test.");
                DeleteFileW(outputPath.c_str());
                return;
            }

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
                wchar_t message[256]{};
                swprintf_s(message, L"[V1Smoke] Desktop capture is unavailable in this environment; skipping smoke test. HRESULT=0x%08X", static_cast<unsigned int>(hr));
                Logger::WriteMessage(message);
                DeleteFileW(outputPath.c_str());
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
            AssertMediaSampleReadable(reader.get(), MF_SOURCE_READER_FIRST_VIDEO_STREAM, L"V1 smoke MP4 should have readable video samples");
            if (expectAudio)
            {
                Assert::IsTrue(s_audioSamples.load(std::memory_order_relaxed) > 0, L"V1 smoke recording should receive audio samples when loopback is available");
                AssertMediaStreamExists(reader.get(), MF_SOURCE_READER_FIRST_AUDIO_STREAM, L"V1 smoke MP4 should contain a readable audio stream when loopback is available");
                AssertMediaSampleReadable(reader.get(), MF_SOURCE_READER_FIRST_AUDIO_STREAM, L"V1 smoke MP4 should have readable audio samples when loopback is available");
            }

            DeleteFileW(outputPath.c_str());
        }

        TEST_METHOD(V1_ManualRealDesktopMemorySmoke_WhenEnabled_ProducesReadableMp4AndBoundedWorkingSet)
        {
            if (!IsRealWgcMemorySmokeEnabled())
            {
                Logger::WriteMessage("[V1Smoke] Skipping manual real WGC memory smoke. Set CAPTURETOOL_RUN_REAL_WGC_MEMORY_SMOKE=1 to run it on a desktop session.");
                return;
            }

            const DWORD durationSeconds = GetRealWgcMemorySmokeDurationSeconds();
            const DWORD warmupSeconds = durationSeconds / 3 < 10 ? durationSeconds / 3 : 10;
            const std::wstring outputPath = CreateTempMp4Path(L"capturetool-v1-real-wgc-memory-smoke.mp4");

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
            Assert::IsNotNull(session.get(), L"Manual real WGC memory smoke session should be created");

            HRESULT hr = S_OK;
            Assert::IsTrue(session->Start(&hr), L"Manual real WGC memory smoke should start desktop capture");

            std::this_thread::sleep_for(std::chrono::seconds(warmupSeconds));
            SIZE_T warmWorkingSet = GetCurrentWorkingSetBytes();
            SIZE_T peakWorkingSet = warmWorkingSet;

            for (DWORD elapsed = warmupSeconds; elapsed < durationSeconds; elapsed++)
            {
                std::this_thread::sleep_for(std::chrono::seconds(1));
                SIZE_T currentWorkingSet = GetCurrentWorkingSetBytes();
                peakWorkingSet = currentWorkingSet > peakWorkingSet ? currentWorkingSet : peakWorkingSet;
            }

            session->Stop();

            const SIZE_T peakDelta = peakWorkingSet > warmWorkingSet ? peakWorkingSet - warmWorkingSet : 0;
            constexpr SIZE_T MaxAllowedGrowthBytes = 256ull * 1024ull * 1024ull;

            wchar_t message[384]{};
            swprintf_s(
                message,
                L"[V1Smoke] Manual real WGC memory smoke duration=%lu sec warmWorkingSet=%zu peakWorkingSet=%zu peakDelta=%zu bytes output=%ls",
                durationSeconds,
                static_cast<size_t>(warmWorkingSet),
                static_cast<size_t>(peakWorkingSet),
                static_cast<size_t>(peakDelta),
                outputPath.c_str());
            Logger::WriteMessage(message);

            Assert::IsTrue(peakDelta <= MaxAllowedGrowthBytes, L"Manual real WGC memory smoke should keep working set roughly flat after warm-up");
            Assert::IsTrue(std::filesystem::exists(outputPath), L"Manual real WGC memory smoke MP4 should exist");
            Assert::IsTrue(std::filesystem::file_size(outputPath) > 0, L"Manual real WGC memory smoke MP4 should be non-empty");

            MediaFoundationLifecycleManager mediaFoundation;
            Assert::IsTrue(mediaFoundation.IsInitialized(), L"Media Foundation should initialize for manual real WGC MP4 verification");

            wil::com_ptr<IMFSourceReader> reader;
            hr = MFCreateSourceReaderFromURL(outputPath.c_str(), nullptr, reader.put());
            Assert::IsTrue(SUCCEEDED(hr), L"Manual real WGC memory smoke MP4 should open through Media Foundation");
            AssertMediaStreamExists(reader.get(), MF_SOURCE_READER_FIRST_VIDEO_STREAM, L"Manual real WGC memory smoke MP4 should contain video");
            AssertMediaSampleReadable(reader.get(), MF_SOURCE_READER_FIRST_VIDEO_STREAM, L"Manual real WGC memory smoke MP4 should have readable video samples");
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
            if (!session)
            {
                Logger::WriteMessage("[V1Smoke] Desktop capture fallback session could not be created in this environment; skipping real WGC audio fallback smoke test.");
                DeleteFileW(outputPath.c_str());
                return;
            }

            HRESULT hr = S_OK;
            bool started = session->Start(&hr);
            if (!started)
            {
                wchar_t message[256]{};
                swprintf_s(message, L"[V1Smoke] Desktop capture is unavailable in this environment; skipping audio fallback start assertion. HRESULT=0x%08X", static_cast<unsigned int>(hr));
                Logger::WriteMessage(message);
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
