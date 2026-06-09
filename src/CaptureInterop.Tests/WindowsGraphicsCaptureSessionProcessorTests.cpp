#include "pch.h"
#include "CppUnitTest.h"
#include "CallbackTypes.h"
#include "CaptureSessionConfig.h"
#include "IAudioCaptureSource.h"
#include "IMP4SinkWriter.h"
#include "IMediaClock.h"
#include "IVideoCaptureSource.h"
#include "IVideoFrameProcessorFactory.h"
#include "WindowsGraphicsCaptureSession.h"

#include <memory>
#include <span>
#include <utility>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    namespace
    {
        ID3D11Texture2D* g_callbackTexture = nullptr;

        void __stdcall CaptureVideoFrameCallback(const VideoFrameData* frameData)
        {
            g_callbackTexture = frameData ? static_cast<ID3D11Texture2D*>(frameData->pTexture) : nullptr;
        }

        class FakeMediaClock final : public IMediaClock
        {
        public:
            LONGLONG GetCurrentTime() const override { return 0; }
            LONGLONG GetStartTime() const override { return 0; }
            LONGLONG GetRelativeTime(LONGLONG) const override { return 0; }
            bool IsRunning() const override { return m_isRunning; }
            LONGLONG GetQpcFrequency() const override { return 10'000'000; }
            void Start(LONGLONG) override { m_isRunning = true; }
            void Reset() override { m_isRunning = false; }
            void Pause() override { m_isRunning = false; }
            void Resume() override { m_isRunning = true; }
            void SetClockAdvancer(IMediaClockAdvancer*) override {}
            void AdvanceByAudioSamples(UINT32, UINT32) override {}

        private:
            bool m_isRunning = false;
        };

        class FakeAudioCaptureSource final : public IAudioCaptureSource
        {
        public:
            bool Initialize(HRESULT* outHr = nullptr) override
            {
                if (outHr) *outHr = E_FAIL;
                return false;
            }

            bool Start(HRESULT* outHr = nullptr) override
            {
                if (outHr) *outHr = S_OK;
                return true;
            }

            void Stop() override {}
            WAVEFORMATEX* GetFormat() const override { return nullptr; }
            void SetAudioSampleReadyCallback(AudioSampleReadyCallback) override {}
            void SetEnabled(bool) override {}
            bool IsEnabled() const override { return false; }
            bool IsRunning() const override { return false; }
            void SetClockWriter(IMediaClockWriter*) override {}
        };

        class FakeVideoCaptureSource final : public IVideoCaptureSource
        {
        public:
            bool Initialize(HRESULT* outHr = nullptr) override
            {
                if (outHr) *outHr = S_OK;
                return true;
            }

            bool Start(HRESULT* outHr = nullptr) override
            {
                if (outHr) *outHr = S_OK;
                return true;
            }

            void Stop() override {}
            UINT32 GetWidth() const override { return 640; }
            UINT32 GetHeight() const override { return 480; }
            ID3D11Device* GetDevice() const override { return reinterpret_cast<ID3D11Device*>(0x1000); }
            MonitorHdrInfo GetMonitorHdrInfo() const override { return MonitorHdrInfo::Sdr(); }
            void SetVideoFrameReadyCallback(VideoFrameReadyCallback callback) override { m_callback = std::move(callback); }
            bool IsRunning() const override { return true; }

            void EmitFrame(ID3D11Texture2D* texture, LONGLONG timestamp)
            {
                if (m_callback)
                {
                    VideoFrameReadyEventArgs args{};
                    args.pTexture = texture;
                    args.timestamp = timestamp;
                    m_callback(args);
                }
            }

        private:
            VideoFrameReadyCallback m_callback;
        };

        class FakeSinkWriter final : public IMP4SinkWriter
        {
        public:
            bool Initialize(
                const wchar_t*,
                ID3D11Device*,
                uint32_t,
                uint32_t,
                long* outHr = nullptr,
                uint32_t = 0,
                uint32_t = 0) override
            {
                if (outHr) *outHr = S_OK;
                return true;
            }

            bool InitializeAudioStream(WAVEFORMATEX*, long* outHr = nullptr) override
            {
                if (outHr) *outHr = S_OK;
                return true;
            }

            long WriteFrame(ID3D11Texture2D* texture, int64_t relativeTicks) override
            {
                lastTexture = texture;
                lastTimestamp = relativeTicks;
                ++writeFrameCount;
                return S_OK;
            }

            long WriteAudioSample(std::span<const uint8_t>, int64_t) override { return S_OK; }
            void Finalize() override { finalized = true; }

            ID3D11Texture2D* lastTexture = nullptr;
            int64_t lastTimestamp = 0;
            int writeFrameCount = 0;
            bool finalized = false;
        };

        class FakeVideoFrameProcessor final : public IVideoFrameProcessor
        {
        public:
            FakeVideoFrameProcessor(ID3D11Texture2D* outputTexture, int* processCount, HRESULT failureHr = S_OK)
                : m_outputTexture(outputTexture)
                , m_processCount(processCount)
                , m_failureHr(failureHr)
            {
            }

            Result<VideoFrameProcessorResult> Process(ID3D11Texture2D*) override
            {
                if (m_processCount)
                {
                    ++(*m_processCount);
                }

                if (FAILED(m_failureHr))
                {
                    return Result<VideoFrameProcessorResult>::Error(ErrorInfo::FromHResult(m_failureHr, "Fake processor failure"));
                }

                return Result<VideoFrameProcessorResult>::Ok(VideoFrameProcessorResult{ m_outputTexture });
            }

        private:
            ID3D11Texture2D* m_outputTexture;
            int* m_processCount;
            HRESULT m_failureHr;
        };

        class FakeVideoFrameProcessorFactory final : public IVideoFrameProcessorFactory
        {
        public:
            FakeVideoFrameProcessorFactory(
                ID3D11Texture2D* outputTexture,
                int* createCount,
                int* processCount,
                HRESULT failureHr = S_OK)
                : m_outputTexture(outputTexture)
                , m_createCount(createCount)
                , m_processCount(processCount)
                , m_failureHr(failureHr)
            {
            }

            Result<std::unique_ptr<IVideoFrameProcessor>> CreateProcessor(const VideoFrameProcessorFactoryContext&) override
            {
                if (m_createCount)
                {
                    ++(*m_createCount);
                }

                return Result<std::unique_ptr<IVideoFrameProcessor>>::Ok(
                    std::make_unique<FakeVideoFrameProcessor>(m_outputTexture, m_processCount, m_failureHr));
            }

        private:
            ID3D11Texture2D* m_outputTexture;
            int* m_createCount;
            int* m_processCount;
            HRESULT m_failureHr;
        };
    }

    TEST_CLASS(WindowsGraphicsCaptureSessionProcessorTests)
    {
    public:
        TEST_METHOD(VideoFrame_IsProcessedBeforeSinkAndCallbacks)
        {
            g_callbackTexture = nullptr;
            auto videoSource = std::make_unique<FakeVideoCaptureSource>();
            auto* rawVideoSource = videoSource.get();
            auto sinkWriter = std::make_unique<FakeSinkWriter>();
            auto* rawSinkWriter = sinkWriter.get();

            auto* rawTexture = reinterpret_cast<ID3D11Texture2D*>(0x2000);
            auto* processedTexture = reinterpret_cast<ID3D11Texture2D*>(0x3000);
            int processorCreateCount = 0;
            int processCount = 0;

            WindowsGraphicsCaptureSession session(
                CaptureSessionConfig(reinterpret_cast<HMONITOR>(1), L"test.mp4"),
                std::make_unique<FakeMediaClock>(),
                std::make_unique<FakeAudioCaptureSource>(),
                std::move(videoSource),
                std::move(sinkWriter),
                std::make_unique<FakeVideoFrameProcessorFactory>(
                    processedTexture,
                    &processorCreateCount,
                    &processCount));

            HRESULT hr = S_OK;
            Assert::IsTrue(session.Initialize(&hr));
            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(hr));
            Assert::AreEqual(1, processorCreateCount);

            session.SetVideoFrameCallback(&CaptureVideoFrameCallback);
            rawVideoSource->EmitFrame(rawTexture, 1234);

            Assert::AreEqual(1, processCount);
            Assert::AreEqual(1, rawSinkWriter->writeFrameCount);
            Assert::IsTrue(rawSinkWriter->lastTexture == processedTexture);
            Assert::AreEqual(static_cast<int64_t>(1234), rawSinkWriter->lastTimestamp);
            Assert::IsTrue(g_callbackTexture == processedTexture);
        }

        TEST_METHOD(VideoFrameProcessorFailure_DisablesProcessingForRestOfRecording)
        {
            g_callbackTexture = nullptr;
            auto videoSource = std::make_unique<FakeVideoCaptureSource>();
            auto* rawVideoSource = videoSource.get();
            auto sinkWriter = std::make_unique<FakeSinkWriter>();
            auto* rawSinkWriter = sinkWriter.get();

            auto* rawTexture = reinterpret_cast<ID3D11Texture2D*>(0x2000);
            auto* processedTexture = reinterpret_cast<ID3D11Texture2D*>(0x3000);
            int processorCreateCount = 0;
            int processCount = 0;

            WindowsGraphicsCaptureSession session(
                CaptureSessionConfig(reinterpret_cast<HMONITOR>(1), L"test.mp4"),
                std::make_unique<FakeMediaClock>(),
                std::make_unique<FakeAudioCaptureSource>(),
                std::move(videoSource),
                std::move(sinkWriter),
                std::make_unique<FakeVideoFrameProcessorFactory>(
                    processedTexture,
                    &processorCreateCount,
                    &processCount,
                    E_FAIL));

            HRESULT hr = S_OK;
            Assert::IsTrue(session.Initialize(&hr));
            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(hr));
            Assert::AreEqual(1, processorCreateCount);

            session.SetVideoFrameCallback(&CaptureVideoFrameCallback);
            rawVideoSource->EmitFrame(rawTexture, 1234);

            Assert::AreEqual(1, processCount);
            Assert::AreEqual(1, rawSinkWriter->writeFrameCount);
            Assert::IsTrue(rawSinkWriter->lastTexture == rawTexture);
            Assert::AreEqual(static_cast<int64_t>(1234), rawSinkWriter->lastTimestamp);
            Assert::IsTrue(g_callbackTexture == rawTexture);

            auto* secondRawTexture = reinterpret_cast<ID3D11Texture2D*>(0x4000);
            rawVideoSource->EmitFrame(secondRawTexture, 5678);

            Assert::AreEqual(1, processCount);
            Assert::AreEqual(2, rawSinkWriter->writeFrameCount);
            Assert::IsTrue(rawSinkWriter->lastTexture == secondRawTexture);
            Assert::AreEqual(static_cast<int64_t>(5678), rawSinkWriter->lastTimestamp);
            Assert::IsTrue(g_callbackTexture == secondRawTexture);
        }
    };
}
