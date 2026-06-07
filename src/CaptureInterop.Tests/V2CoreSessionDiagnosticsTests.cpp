#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Core/CapturePipelineSession.h"
#include "V2CoreTestComponents.h"

#include <memory>
#include <utility>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;
using namespace CaptureInterop::V2::Testing;

namespace
{
    class ManualTimeProvider final : public IClockTimeProvider
    {
    public:
        MediaTime Now() const noexcept override
        {
            return m_now;
        }

    private:
        MediaTime m_now;
    };

    CapturePipelineConfig CreateAudioConfig()
    {
        CapturePipelineConfig config;

        SystemAudioSourceConfig audio;
        audio.id = SourceId::FromValue(2);
        audio.name = "Audio";
        audio.armed = true;
        config.sources.push_back(SourceConfig::SystemAudio(audio));

        config.output.container = ContainerFormat::Mp4;
        config.output.outputPath = L"C:\\Temp\\diagnostics.mp4";
        config.output.audio = AudioEncodingSettings{ AudioCodec::Aac, 192000, 48000, 2 };

        return config;
    }

    CapturePipelineConfig CreateMp3ConfigWithIncidentalVideo()
    {
        CapturePipelineConfig config;

        DesktopSourceConfig desktop;
        desktop.id = SourceId::FromValue(1);
        desktop.name = "Incidental desktop";
        desktop.frameRate = Rational::From(60, 1);
        config.sources.push_back(SourceConfig::Desktop(desktop));

        SystemAudioSourceConfig audio;
        audio.id = SourceId::FromValue(2);
        audio.name = "Audio";
        audio.armed = true;
        config.sources.push_back(SourceConfig::SystemAudio(audio));

        config.output.container = ContainerFormat::Mp3;
        config.output.outputPath = L"C:\\Temp\\diagnostics.mp3";
        config.output.audio = AudioEncodingSettings{ AudioCodec::Mp3, 192000, 48000, 2 };

        return config;
    }

    CapturePipelineConfig CreateVideoConfig()
    {
        CapturePipelineConfig config;

        DesktopSourceConfig desktop;
        desktop.id = SourceId::FromValue(1);
        desktop.name = "Desktop";
        desktop.frameRate = Rational::From(60, 1);
        config.sources.push_back(SourceConfig::Desktop(desktop));

        config.output.container = ContainerFormat::Mp4;
        config.output.outputPath = L"C:\\Temp\\diagnostics.mp4";
        config.output.video = VideoEncodingSettings{ VideoCodec::H264, 8'000'000, Rational::From(60, 1), 120, true };

        return config;
    }

    class NoopProcessorFactory final : public IMediaProcessorFactory
    {
    public:
        std::vector<std::unique_ptr<IMediaProcessor>> CreateProcessors(const OutputPlan&) override
        {
            return {};
        }
    };

    class AudioSourceFactory final : public IMediaSourceFactory
    {
    public:
        std::vector<std::unique_ptr<IMediaSource>> CreateSources(const CapturePipelineConfig&) override
        {
            std::vector<std::unique_ptr<IMediaSource>> sources;
            sources.push_back(std::make_unique<FakeAudioSource>());
            return sources;
        }
    };

    class VideoSourceFactory final : public IMediaSourceFactory
    {
    public:
        std::vector<std::unique_ptr<IMediaSource>> CreateSources(const CapturePipelineConfig&) override
        {
            auto video = std::make_unique<FakeVideoSource>();
            videoSource = video.get();

            std::vector<std::unique_ptr<IMediaSource>> sources;
            sources.push_back(std::move(video));
            return sources;
        }

        FakeVideoSource* videoSource{ nullptr };
    };

    class NullSinkFactory final : public IOutputSinkFactory
    {
    public:
        std::unique_ptr<IOutputSink> CreateSink(const OutputPlan&) override
        {
            return std::make_unique<NullOutputSink>();
        }
    };

    class FailingStopSource final : public IMediaSource
    {
    public:
        SourceDescriptor Describe() const override
        {
            return SourceDescriptor{ SourceId::FromValue(2), SourceKind::SystemAudio, "Failing source" };
        }

        std::vector<StreamDescriptor> Streams() const override
        {
            return { StreamDescriptor{ StreamId::FromValue(2), SourceId::FromValue(2), MediaKind::Audio, "Audio" } };
        }

        OperationResult Start() noexcept override
        {
            return OperationResult::Success();
        }

        OperationResult Stop() noexcept override
        {
            return OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "FailingStopSource",
                "Stop",
                "Injected stop failure");
        }
    };

    class FailingStopSourceFactory final : public IMediaSourceFactory
    {
    public:
        std::vector<std::unique_ptr<IMediaSource>> CreateSources(const CapturePipelineConfig&) override
        {
            std::vector<std::unique_ptr<IMediaSource>> sources;
            sources.push_back(std::make_unique<FailingStopSource>());
            return sources;
        }
    };

    CapturePipelineSession CreateSession(
        CapturePipelineConfig config,
        IMediaSourceFactory& sourceFactory,
        IMediaProcessorFactory& processorFactory,
        IOutputSinkFactory& sinkFactory,
        ManualTimeProvider& timeProvider)
    {
        return CapturePipelineSession(
            std::move(config),
            sourceFactory,
            processorFactory,
            sinkFactory,
            std::make_unique<RecordingClock>(timeProvider));
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2CoreSessionDiagnosticsTests)
    {
    public:
        TEST_METHOD(NormalStop_HasFinalStateNoFailureStageAndZeroCounters)
        {
            ManualTimeProvider timeProvider;
            AudioSourceFactory sourceFactory;
            NoopProcessorFactory processorFactory;
            NullSinkFactory sinkFactory;
            CapturePipelineSession session = CreateSession(
                CreateAudioConfig(),
                sourceFactory,
                processorFactory,
                sinkFactory,
                timeProvider);

            Assert::IsTrue(session.Start().IsSuccess());
            const CapturePipelineStopResult stopResult = session.Stop();

            Assert::IsTrue(stopResult.result.IsSuccess());
            Assert::AreEqual(static_cast<int>(PipelineState::Finalized), static_cast<int>(stopResult.finalState));
            Assert::AreEqual(static_cast<int>(TeardownStage::None), static_cast<int>(stopResult.failureStage));
            Assert::AreEqual(0ull, session.Counters().droppedVideoFrames);
            Assert::AreEqual(0ull, session.Counters().unsupportedCommands);
            Assert::AreEqual(0ull, session.Counters().validationWarnings);
            Assert::AreEqual(static_cast<size_t>(0), session.Diagnostics().size());
        }

        TEST_METHOD(InvalidRuntimeCommand_AddsDiagnosticAndUnsupportedCounter)
        {
            ManualTimeProvider timeProvider;
            AudioSourceFactory sourceFactory;
            NoopProcessorFactory processorFactory;
            NullSinkFactory sinkFactory;
            CapturePipelineSession session = CreateSession(
                CreateAudioConfig(),
                sourceFactory,
                processorFactory,
                sinkFactory,
                timeProvider);

            const OperationResult result = session.Pause();

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(1ull, session.Counters().unsupportedCommands);
            Assert::AreEqual(static_cast<size_t>(1), session.Diagnostics().size());
            Assert::AreEqual("Pause", session.Diagnostics()[0].operation.c_str());
        }

        TEST_METHOD(ValidationFailure_AddsDiagnosticAndFailsSession)
        {
            ManualTimeProvider timeProvider;
            AudioSourceFactory sourceFactory;
            NoopProcessorFactory processorFactory;
            NullSinkFactory sinkFactory;
            CapturePipelineConfig config = CreateAudioConfig();
            config.output.outputPath.clear();
            CapturePipelineSession session = CreateSession(
                config,
                sourceFactory,
                processorFactory,
                sinkFactory,
                timeProvider);

            const OperationResult result = session.Start();

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(static_cast<int>(PipelineState::Failed), static_cast<int>(session.State()));
            Assert::AreEqual(static_cast<size_t>(1), session.Diagnostics().size());
            Assert::AreEqual("Output path is required", session.Diagnostics()[0].message.c_str());
            Assert::AreEqual(0ull, session.Counters().unsupportedCommands);
        }

        TEST_METHOD(ValidationWarning_IncrementsWarningCounterAndStoresDiagnostic)
        {
            ManualTimeProvider timeProvider;
            AudioSourceFactory sourceFactory;
            NoopProcessorFactory processorFactory;
            NullSinkFactory sinkFactory;
            CapturePipelineSession session = CreateSession(
                CreateMp3ConfigWithIncidentalVideo(),
                sourceFactory,
                processorFactory,
                sinkFactory,
                timeProvider);

            const OperationResult result = session.Start();

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(1ull, session.Counters().validationWarnings);
            Assert::AreEqual(static_cast<size_t>(1), session.Diagnostics().size());
            Assert::IsTrue(session.Diagnostics()[0].IsWarning());
            Assert::AreEqual("Incidental video sources are pruned from MP3 output", session.Diagnostics()[0].message.c_str());
        }

        TEST_METHOD(PausedVideoSample_IncrementsDroppedVideoCounter)
        {
            ManualTimeProvider timeProvider;
            VideoSourceFactory sourceFactory;
            NoopProcessorFactory processorFactory;
            NullSinkFactory sinkFactory;
            CapturePipelineSession session = CreateSession(
                CreateVideoConfig(),
                sourceFactory,
                processorFactory,
                sinkFactory,
                timeProvider);

            Assert::IsTrue(session.Start().IsSuccess());
            Assert::IsTrue(session.Pause().IsSuccess());
            Assert::IsTrue(sourceFactory.videoSource->Emit(SampleBuilder::Video()).IsSuccess());

            Assert::AreEqual(1ull, session.Counters().droppedVideoFrames);
        }

        TEST_METHOD(TeardownFailure_AddsDiagnosticAndFailureStage)
        {
            ManualTimeProvider timeProvider;
            FailingStopSourceFactory sourceFactory;
            NoopProcessorFactory processorFactory;
            NullSinkFactory sinkFactory;
            CapturePipelineSession session = CreateSession(
                CreateAudioConfig(),
                sourceFactory,
                processorFactory,
                sinkFactory,
                timeProvider);

            Assert::IsTrue(session.Start().IsSuccess());
            const CapturePipelineStopResult stopResult = session.Stop();

            Assert::IsTrue(stopResult.result.IsFailure());
            Assert::AreEqual(static_cast<int>(PipelineState::Failed), static_cast<int>(stopResult.finalState));
            Assert::AreEqual(static_cast<int>(TeardownStage::StopSources), static_cast<int>(stopResult.failureStage));
            Assert::AreEqual(static_cast<size_t>(1), session.Diagnostics().size());
            Assert::AreEqual("Injected stop failure", session.Diagnostics()[0].message.c_str());
        }
    };
}
