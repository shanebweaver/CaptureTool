#include "pch.h"
#include "CppUnitTest.h"
#include "WindowsGraphicsCaptureSession.h"
#include "CaptureSessionConfig.h"
#include "IAudioCaptureSource.h"
#include "IVideoCaptureSource.h"
#include "IMP4SinkWriter.h"
#include "IMediaClock.h"

#include <stdexcept>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    class StopTestMediaClock final : public IMediaClock
    {
    public:
        LONGLONG GetCurrentTime() const override { return 0; }
        LONGLONG GetStartTime() const override { return 0; }
        LONGLONG GetRelativeTime(LONGLONG) const override { return 0; }
        bool IsRunning() const override { return m_running; }
        LONGLONG GetQpcFrequency() const override { return 1; }
        void Start(LONGLONG) override { m_running = true; }
        void Reset() override { m_running = false; }
        void Pause() override { }
        void Resume() override { }
        void SetClockAdvancer(IMediaClockAdvancer* advancer) override
        {
            if (advancer)
            {
                advancer->SetClockWriter(this);
            }
        }
        void AdvanceByAudioSamples(UINT32, UINT32) override { }

    private:
        bool m_running = false;
    };

    class StopTestAudioSource final : public IAudioCaptureSource
    {
    public:
        explicit StopTestAudioSource(HRESULT stopResult = S_OK)
            : m_stopResult(stopResult)
        {
        }

        bool Initialize(HRESULT* outHr) override
        {
            if (outHr) *outHr = S_OK;
            return true;
        }

        bool Start(HRESULT* outHr) override
        {
            m_running = true;
            if (outHr) *outHr = S_OK;
            return true;
        }

        HRESULT Stop() override
        {
            m_running = false;
            ++stopCount;
            return m_stopResult;
        }

        WAVEFORMATEX* GetFormat() const override { return nullptr; }
        void SetAudioSampleReadyCallback(AudioSampleReadyCallback callback) override
        {
            m_callback = std::move(callback);
        }
        void SetEnabled(bool enabled) override { m_enabled = enabled; }
        bool IsEnabled() const override { return m_enabled; }
        bool IsRunning() const override { return m_running; }
        void SetClockWriter(IMediaClockWriter* writer) override { m_writer = writer; }

        int stopCount = 0;

    private:
        HRESULT m_stopResult;
        bool m_enabled = true;
        bool m_running = false;
        IMediaClockWriter* m_writer = nullptr;
        AudioSampleReadyCallback m_callback;
    };

    class StopTestVideoSource final : public IVideoCaptureSource
    {
    public:
        explicit StopTestVideoSource(HRESULT stopResult = S_OK)
            : m_stopResult(stopResult)
        {
        }

        bool Initialize(HRESULT* outHr) override
        {
            if (outHr) *outHr = S_OK;
            return true;
        }

        bool Start(HRESULT* outHr) override
        {
            m_running = true;
            if (outHr) *outHr = S_OK;
            return true;
        }

        HRESULT Stop() override
        {
            m_running = false;
            ++stopCount;
            return m_stopResult;
        }

        UINT32 GetWidth() const override { return 1920; }
        UINT32 GetHeight() const override { return 1080; }
        ID3D11Device* GetDevice() const override { return nullptr; }
        void SetVideoFrameReadyCallback(VideoFrameReadyCallback callback) override
        {
            m_callback = std::move(callback);
        }
        bool IsRunning() const override { return m_running; }

        int stopCount = 0;

    private:
        HRESULT m_stopResult;
        bool m_running = false;
        VideoFrameReadyCallback m_callback;
    };

    class StopTestSinkWriter final : public IMP4SinkWriter
    {
    public:
        explicit StopTestSinkWriter(HRESULT finalizeResult = S_OK, bool throwOnFinalize = false)
            : m_finalizeResult(finalizeResult)
            , m_throwOnFinalize(throwOnFinalize)
        {
        }

        bool Initialize(const wchar_t*, ID3D11Device*, uint32_t, uint32_t, long* outHr) override
        {
            if (outHr) *outHr = S_OK;
            return true;
        }

        bool InitializeAudioStream(WAVEFORMATEX*, long* outHr) override
        {
            if (outHr) *outHr = S_OK;
            return true;
        }

        long WriteFrame(ID3D11Texture2D*, int64_t) override { return S_OK; }
        long WriteAudioSample(std::span<const uint8_t>, int64_t) override { return S_OK; }

        HRESULT Finalize() override
        {
            ++finalizeCount;
            if (m_throwOnFinalize)
            {
                throw std::runtime_error("finalize failed");
            }
            return m_finalizeResult;
        }

        int finalizeCount = 0;

    private:
        HRESULT m_finalizeResult;
        bool m_throwOnFinalize;
    };

    TEST_CLASS(CaptureSessionStopTests)
    {
    private:
        static std::unique_ptr<WindowsGraphicsCaptureSession> CreateActiveSession(
            std::unique_ptr<StopTestAudioSource> audioSource,
            std::unique_ptr<StopTestVideoSource> videoSource,
            std::unique_ptr<StopTestSinkWriter> sinkWriter)
        {
            CaptureSessionConfig config(
                reinterpret_cast<HMONITOR>(1),
                L"C:\\Temp\\capture-stop-test.mp4",
                false);

            auto session = std::make_unique<WindowsGraphicsCaptureSession>(
                config,
                std::make_unique<StopTestMediaClock>(),
                std::move(audioSource),
                std::move(videoSource),
                std::move(sinkWriter));

            HRESULT hr = E_FAIL;
            Assert::IsTrue(session->Initialize(&hr));
            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(hr));
            Assert::IsTrue(session->Start(&hr));
            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(hr));
            return session;
        }

    public:
        TEST_METHOD(Stop_WhenFinalizeFails_ReturnsSinkStage)
        {
            auto audio = std::make_unique<StopTestAudioSource>();
            auto video = std::make_unique<StopTestVideoSource>();
            auto sink = std::make_unique<StopTestSinkWriter>(E_FAIL);
            StopTestAudioSource* audioPtr = audio.get();
            StopTestVideoSource* videoPtr = video.get();
            StopTestSinkWriter* sinkPtr = sink.get();
            auto session = CreateActiveSession(std::move(audio), std::move(video), std::move(sink));

            CaptureOperationResult result = session->Stop();

            Assert::AreEqual(static_cast<long>(E_FAIL), static_cast<long>(result.hr));
            Assert::AreEqual(
                static_cast<int>(CaptureOperationStage::SinkFinalize),
                static_cast<int>(result.stage));
            Assert::AreEqual(1, audioPtr->stopCount);
            Assert::AreEqual(1, videoPtr->stopCount);
            Assert::AreEqual(1, sinkPtr->finalizeCount);
        }

        TEST_METHOD(Stop_WhenMultipleStepsFail_PreservesFirstFailureAndContinuesCleanup)
        {
            auto audio = std::make_unique<StopTestAudioSource>(E_ACCESSDENIED);
            auto video = std::make_unique<StopTestVideoSource>(E_ABORT);
            auto sink = std::make_unique<StopTestSinkWriter>(E_FAIL);
            StopTestAudioSource* audioPtr = audio.get();
            StopTestVideoSource* videoPtr = video.get();
            StopTestSinkWriter* sinkPtr = sink.get();
            auto session = CreateActiveSession(std::move(audio), std::move(video), std::move(sink));

            CaptureOperationResult result = session->Stop();

            Assert::AreEqual(static_cast<long>(E_ABORT), static_cast<long>(result.hr));
            Assert::AreEqual(
                static_cast<int>(CaptureOperationStage::VideoSourceStop),
                static_cast<int>(result.stage));
            Assert::AreEqual(1, audioPtr->stopCount);
            Assert::AreEqual(1, videoPtr->stopCount);
            Assert::AreEqual(1, sinkPtr->finalizeCount);
        }

        TEST_METHOD(Stop_WhenFinalizeThrows_ConvertsExceptionToFailure)
        {
            auto session = CreateActiveSession(
                std::make_unique<StopTestAudioSource>(),
                std::make_unique<StopTestVideoSource>(),
                std::make_unique<StopTestSinkWriter>(S_OK, true));

            CaptureOperationResult result = session->Stop();

            Assert::AreEqual(static_cast<long>(E_FAIL), static_cast<long>(result.hr));
            Assert::AreEqual(
                static_cast<int>(CaptureOperationStage::SinkFinalize),
                static_cast<int>(result.stage));
        }
    };
}
