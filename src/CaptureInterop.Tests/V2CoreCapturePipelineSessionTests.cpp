#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Core/CapturePipelineSession.h"

#include <memory>
#include <string>
#include <utility>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;

namespace
{
    using EventLog = std::vector<std::string>;

    CapturePipelineConfig CreateValidConfig()
    {
        CapturePipelineConfig config;

        DesktopSourceConfig desktop;
        desktop.id = SourceId::FromValue(1);
        desktop.name = "Desktop";
        desktop.frameRate = Rational::From(60, 1);
        config.sources.push_back(SourceConfig::Desktop(desktop));

        config.output.container = ContainerFormat::Mp4;
        config.output.outputPath = L"C:\\Temp\\session.mp4";
        config.output.video = VideoEncodingSettings{ VideoCodec::H264, 8'000'000, Rational::From(60, 1), 120, true };

        return config;
    }

    class LoggingClock final : public IRecordingClock
    {
    public:
        explicit LoggingClock(EventLog& log)
            : m_log(log)
        {
        }

        ~LoggingClock() override
        {
            m_log.push_back("release-clock");
        }

        OperationResult Start() noexcept override
        {
            m_log.push_back("start-clock");
            m_started = true;
            return OperationResult::Success();
        }

        OperationResult Pause() noexcept override { return OperationResult::Success(); }
        OperationResult Resume() noexcept override { return OperationResult::Success(); }
        MediaTime CurrentTime() const noexcept override { return MediaTime::Zero(); }
        bool IsStarted() const noexcept override { return m_started; }
        bool IsPaused() const noexcept override { return false; }

    private:
        EventLog& m_log;
        bool m_started{ false };
    };

    class LoggingSource final : public IMediaSource
    {
    public:
        LoggingSource(EventLog& log, OperationResult stopResult = OperationResult::Success())
            : m_log(log),
              m_stopResult(std::move(stopResult))
        {
        }

        ~LoggingSource() override
        {
            m_log.push_back("release-source");
        }

        SourceDescriptor Describe() const override
        {
            return SourceDescriptor{ SourceId::FromValue(1), SourceKind::Desktop, "Source" };
        }

        std::vector<StreamDescriptor> Streams() const override
        {
            return { StreamDescriptor{ StreamId::FromValue(1), SourceId::FromValue(1), MediaKind::Video, "Video" } };
        }

        OperationResult Start() noexcept override
        {
            m_log.push_back("start-source");
            return OperationResult::Success();
        }

        OperationResult Stop() noexcept override
        {
            m_log.push_back("stop-source");
            return m_stopResult;
        }

    private:
        EventLog& m_log;
        OperationResult m_stopResult;
    };

    class LoggingProcessor final : public IMediaProcessor
    {
    public:
        explicit LoggingProcessor(EventLog& log)
            : m_log(log)
        {
        }

        ~LoggingProcessor() override
        {
            m_log.push_back("release-processor");
        }

        MediaKind Kind() const noexcept override { return MediaKind::Video; }
        OperationResult Configure(const MediaType&, const MediaType&) noexcept override { return OperationResult::Success(); }
        OperationResult Process(const MediaSample&) noexcept override { return OperationResult::Success(); }
        CallbackRegistrationToken RegisterOutputHandler(MediaSampleHandler) override { return nullptr; }

    private:
        EventLog& m_log;
    };

    class LoggingSink final : public IOutputSink
    {
    public:
        LoggingSink(EventLog& log, OperationResult finalizeResult = OperationResult::Success())
            : m_log(log),
              m_finalizeResult(std::move(finalizeResult))
        {
        }

        ~LoggingSink() override
        {
            m_log.push_back("release-sink");
        }

        OperationResult Open(const OutputPlan&) noexcept override
        {
            m_log.push_back("open-sink");
            return OperationResult::Success();
        }

        OperationResult WriteSample(const MediaSample&) noexcept override
        {
            return OperationResult::Success();
        }

        OperationResult Finalize() noexcept override
        {
            m_log.push_back("finalize-sink");
            return m_finalizeResult;
        }

    private:
        EventLog& m_log;
        OperationResult m_finalizeResult;
    };

    class TestSourceFactory final : public IMediaSourceFactory
    {
    public:
        explicit TestSourceFactory(EventLog& log)
            : m_log(log)
        {
        }

        std::vector<std::unique_ptr<IMediaSource>> CreateSources(const CapturePipelineConfig&) override
        {
            std::vector<std::unique_ptr<IMediaSource>> sources;
            sources.push_back(std::make_unique<LoggingSource>(m_log, m_stopResult));
            return sources;
        }

        void SetStopResult(OperationResult result)
        {
            m_stopResult = std::move(result);
        }

    private:
        EventLog& m_log;
        OperationResult m_stopResult = OperationResult::Success();
    };

    class TestProcessorFactory final : public IMediaProcessorFactory
    {
    public:
        explicit TestProcessorFactory(EventLog& log)
            : m_log(log)
        {
        }

        std::vector<std::unique_ptr<IMediaProcessor>> CreateProcessors(const OutputPlan&) override
        {
            std::vector<std::unique_ptr<IMediaProcessor>> processors;
            processors.push_back(std::make_unique<LoggingProcessor>(m_log));
            return processors;
        }

    private:
        EventLog& m_log;
    };

    class TestSinkFactory final : public IOutputSinkFactory
    {
    public:
        explicit TestSinkFactory(EventLog& log)
            : m_log(log)
        {
        }

        std::unique_ptr<IOutputSink> CreateSink(const OutputPlan&) override
        {
            return std::make_unique<LoggingSink>(m_log, m_finalizeResult);
        }

        void SetFinalizeResult(OperationResult result)
        {
            m_finalizeResult = std::move(result);
        }

    private:
        EventLog& m_log;
        OperationResult m_finalizeResult = OperationResult::Success();
    };

    std::unique_ptr<IRecordingClock> CreateClock(EventLog& log)
    {
        return std::make_unique<LoggingClock>(log);
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2CoreCapturePipelineSessionTests)
    {
    public:
        TEST_METHOD(Start_BuildsAndStartsGraphOnce)
        {
            EventLog log;
            TestSourceFactory sourceFactory(log);
            TestProcessorFactory processorFactory(log);
            TestSinkFactory sinkFactory(log);
            CapturePipelineSession session(
                CreateValidConfig(),
                sourceFactory,
                processorFactory,
                sinkFactory,
                CreateClock(log));

            const OperationResult startResult = session.Start();
            const OperationResult secondStartResult = session.Start();

            Assert::IsTrue(startResult.IsSuccess());
            Assert::AreEqual(static_cast<int>(PipelineState::Recording), static_cast<int>(session.State()));
            Assert::IsTrue(secondStartResult.IsFailure());
            Assert::AreEqual("Start", secondStartResult.diagnostic->operation.c_str());
            Assert::AreEqual(static_cast<size_t>(3), log.size());
            Assert::AreEqual("open-sink", log[0].c_str());
            Assert::AreEqual("start-clock", log[1].c_str());
            Assert::AreEqual("start-source", log[2].c_str());
        }

        TEST_METHOD(Stop_FinalizesAndReleasesGraphInDeterministicOrder)
        {
            EventLog log;
            TestSourceFactory sourceFactory(log);
            TestProcessorFactory processorFactory(log);
            TestSinkFactory sinkFactory(log);
            CapturePipelineSession session(
                CreateValidConfig(),
                sourceFactory,
                processorFactory,
                sinkFactory,
                CreateClock(log));

            Assert::IsTrue(session.Start().IsSuccess());
            log.clear();

            const CapturePipelineStopResult stopResult = session.Stop();

            Assert::IsTrue(stopResult.result.IsSuccess());
            Assert::AreEqual(static_cast<int>(PipelineState::Finalized), static_cast<int>(stopResult.finalState));
            Assert::AreEqual(static_cast<int>(TeardownStage::None), static_cast<int>(stopResult.failureStage));
            Assert::AreEqual(static_cast<size_t>(6), log.size());
            Assert::AreEqual("stop-source", log[0].c_str());
            Assert::AreEqual("finalize-sink", log[1].c_str());
            Assert::AreEqual("release-sink", log[2].c_str());
            Assert::AreEqual("release-processor", log[3].c_str());
            Assert::AreEqual("release-source", log[4].c_str());
            Assert::AreEqual("release-clock", log[5].c_str());
        }

        TEST_METHOD(Stop_RecordsTeardownStages)
        {
            EventLog log;
            TestSourceFactory sourceFactory(log);
            TestProcessorFactory processorFactory(log);
            TestSinkFactory sinkFactory(log);
            CapturePipelineSession session(
                CreateValidConfig(),
                sourceFactory,
                processorFactory,
                sinkFactory,
                CreateClock(log));

            Assert::IsTrue(session.Start().IsSuccess());
            const CapturePipelineStopResult stopResult = session.Stop();

            Assert::IsTrue(stopResult.result.IsSuccess());
            Assert::AreEqual(static_cast<size_t>(9), session.TeardownStages().size());
            Assert::AreEqual(static_cast<int>(TeardownStage::StopAcceptingCallbacks), static_cast<int>(session.TeardownStages()[0]));
            Assert::AreEqual(static_cast<int>(TeardownStage::StopSources), static_cast<int>(session.TeardownStages()[1]));
            Assert::AreEqual(static_cast<int>(TeardownStage::FinalizeSink), static_cast<int>(session.TeardownStages()[4]));
            Assert::AreEqual(static_cast<int>(TeardownStage::ReleaseInfrastructure), static_cast<int>(session.TeardownStages()[8]));
        }

        TEST_METHOD(Stop_IsIdempotentAfterFinalized)
        {
            EventLog log;
            TestSourceFactory sourceFactory(log);
            TestProcessorFactory processorFactory(log);
            TestSinkFactory sinkFactory(log);
            CapturePipelineSession session(
                CreateValidConfig(),
                sourceFactory,
                processorFactory,
                sinkFactory,
                CreateClock(log));

            Assert::IsTrue(session.Start().IsSuccess());
            const CapturePipelineStopResult firstStop = session.Stop();
            const CapturePipelineStopResult secondStop = session.Stop();

            Assert::IsTrue(firstStop.result.IsSuccess());
            Assert::IsTrue(secondStop.result.IsSuccess());
            Assert::IsFalse(firstStop.alreadyStopped);
            Assert::IsTrue(secondStop.alreadyStopped);
            Assert::AreEqual(static_cast<int>(PipelineState::Finalized), static_cast<int>(secondStop.finalState));
        }

        TEST_METHOD(Stop_BeforeStart_ReturnsStableInvalidState)
        {
            EventLog log;
            TestSourceFactory sourceFactory(log);
            TestProcessorFactory processorFactory(log);
            TestSinkFactory sinkFactory(log);
            CapturePipelineSession session(
                CreateValidConfig(),
                sourceFactory,
                processorFactory,
                sinkFactory,
                CreateClock(log));

            const CapturePipelineStopResult stopResult = session.Stop();

            Assert::IsTrue(stopResult.result.IsFailure());
            Assert::AreEqual(static_cast<int>(PipelineState::Created), static_cast<int>(stopResult.finalState));
            Assert::AreEqual("Stop", stopResult.result.diagnostic->operation.c_str());
        }

        TEST_METHOD(Stop_ReportsFirstMeaningfulFailureStage)
        {
            EventLog log;
            TestSourceFactory sourceFactory(log);
            TestProcessorFactory processorFactory(log);
            TestSinkFactory sinkFactory(log);
            sourceFactory.SetStopResult(OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "LoggingSource",
                "Stop",
                "Injected stop failure"));
            sinkFactory.SetFinalizeResult(OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "LoggingSink",
                "Finalize",
                "Injected finalize failure"));

            CapturePipelineSession session(
                CreateValidConfig(),
                sourceFactory,
                processorFactory,
                sinkFactory,
                CreateClock(log));

            Assert::IsTrue(session.Start().IsSuccess());
            const CapturePipelineStopResult stopResult = session.Stop();

            Assert::IsTrue(stopResult.result.IsFailure());
            Assert::AreEqual(static_cast<int>(PipelineState::Failed), static_cast<int>(stopResult.finalState));
            Assert::AreEqual(static_cast<int>(TeardownStage::StopSources), static_cast<int>(stopResult.failureStage));
            Assert::AreEqual("Injected stop failure", stopResult.result.diagnostic->message.c_str());
        }
    };
}
