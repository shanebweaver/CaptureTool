#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Core/CapturePipelineSession.h"
#include "V2CoreTestComponents.h"

#include <cstdint>
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

        void Advance(MediaDuration duration) noexcept
        {
            m_now = m_now + duration;
        }

    private:
        MediaTime m_now;
    };

    CapturePipelineConfig CreateRoutingConfig()
    {
        CapturePipelineConfig config;

        DesktopSourceConfig desktop;
        desktop.id = SourceId::FromValue(1);
        desktop.name = "Desktop";
        desktop.frameRate = Rational::From(60, 1);
        config.sources.push_back(SourceConfig::Desktop(desktop));

        SystemAudioSourceConfig audio;
        audio.id = SourceId::FromValue(2);
        audio.name = "Audio";
        audio.armed = true;
        config.sources.push_back(SourceConfig::SystemAudio(audio));

        config.output.container = ContainerFormat::Mp4;
        config.output.outputPath = L"C:\\Temp\\routing.mp4";
        config.output.video = VideoEncodingSettings{ VideoCodec::H264, 8'000'000, Rational::From(60, 1), 120, true };
        config.output.audio = AudioEncodingSettings{ AudioCodec::Aac, 192000, 48000, 2 };

        return config;
    }

    class RoutingSourceFactory final : public IMediaSourceFactory
    {
    public:
        std::vector<std::unique_ptr<IMediaSource>> CreateSources(const CapturePipelineConfig&) override
        {
            auto video = std::make_unique<FakeVideoSource>();
            videoSource = video.get();

            auto audio = std::make_unique<FakeAudioSource>();
            audioSource = audio.get();

            std::vector<std::unique_ptr<IMediaSource>> sources;
            sources.push_back(std::move(video));
            sources.push_back(std::move(audio));
            return sources;
        }

        FakeVideoSource* videoSource{ nullptr };
        FakeAudioSource* audioSource{ nullptr };
    };

    class RoutingProcessorFactory final : public IMediaProcessorFactory
    {
    public:
        std::vector<std::unique_ptr<IMediaProcessor>> CreateProcessors(const OutputPlan&) override
        {
            std::vector<std::unique_ptr<IMediaProcessor>> processors;
            processors.push_back(std::make_unique<PassThroughProcessor>(MediaKind::Video));
            processors.push_back(std::make_unique<PassThroughProcessor>(MediaKind::Audio));
            return processors;
        }
    };

    class AudioTagProcessor final : public IMediaProcessor
    {
    public:
        explicit AudioTagProcessor(uint8_t tag) noexcept
            : m_tag(tag)
        {
        }

        MediaKind Kind() const noexcept override
        {
            return MediaKind::Audio;
        }

        OperationResult Configure(const MediaType&, const MediaType&) noexcept override
        {
            return OperationResult::Success();
        }

        OperationResult Process(const MediaSample& sample) noexcept override
        {
            MediaSample taggedSample = sample;
            std::get<AudioSample>(taggedSample.data).pcmData.push_back(m_tag);

            if (m_outputHandler)
            {
                m_outputHandler(taggedSample);
            }

            return OperationResult::Success();
        }

        CallbackRegistrationToken RegisterOutputHandler(MediaSampleHandler handler) override
        {
            m_outputHandler = std::move(handler);
            return std::make_unique<CallbackRegistration>(
                [this]()
                {
                    m_outputHandler = nullptr;
                });
        }

    private:
        uint8_t m_tag{ 0 };
        MediaSampleHandler m_outputHandler;
    };

    class ChainedAudioProcessorFactory final : public IMediaProcessorFactory
    {
    public:
        std::vector<std::unique_ptr<IMediaProcessor>> CreateProcessors(const OutputPlan&) override
        {
            std::vector<std::unique_ptr<IMediaProcessor>> processors;
            processors.push_back(std::make_unique<AudioTagProcessor>(9));
            processors.push_back(std::make_unique<AudioTagProcessor>(10));
            return processors;
        }
    };

    class RoutingSinkFactory final : public IOutputSinkFactory
    {
    public:
        std::unique_ptr<IOutputSink> CreateSink(const OutputPlan&) override
        {
            auto sink = std::make_unique<NullOutputSink>();
            nullSink = sink.get();
            return sink;
        }

        NullOutputSink* nullSink{ nullptr };
    };

    CapturePipelineSession CreateSession(
        ManualTimeProvider& timeProvider,
        RoutingSourceFactory& sourceFactory,
        IMediaProcessorFactory& processorFactory,
        RoutingSinkFactory& sinkFactory)
    {
        return CapturePipelineSession(
            CreateRoutingConfig(),
            sourceFactory,
            processorFactory,
            sinkFactory,
            std::make_unique<RecordingClock>(timeProvider));
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2CoreCapturePipelineSessionRoutingTests)
    {
    public:
        TEST_METHOD(Session_RoutesFakeVideoAndAudioSamplesToSink)
        {
            ManualTimeProvider timeProvider;
            RoutingSourceFactory sourceFactory;
            RoutingProcessorFactory processorFactory;
            RoutingSinkFactory sinkFactory;
            CapturePipelineSession session = CreateSession(timeProvider, sourceFactory, processorFactory, sinkFactory);

            Assert::IsTrue(session.Start().IsSuccess());

            timeProvider.Advance(MediaDuration::FromSeconds(1));
            Assert::IsTrue(sourceFactory.videoSource->Emit(SampleBuilder::Video(MediaTime::FromTicks(999))).IsSuccess());

            timeProvider.Advance(MediaDuration::FromSeconds(1));
            Assert::IsTrue(sourceFactory.audioSource->Emit(SampleBuilder::Audio(MediaTime::FromTicks(999))).IsSuccess());

            Assert::AreEqual(static_cast<size_t>(2), sinkFactory.nullSink->ReceivedSamples().size());
            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(sinkFactory.nullSink->ReceivedSamples()[0].Kind()));
            Assert::AreEqual(static_cast<int>(MediaKind::Audio), static_cast<int>(sinkFactory.nullSink->ReceivedSamples()[1].Kind()));
            Assert::AreEqual(MediaDuration::FromSeconds(1).ticks100ns, sinkFactory.nullSink->ReceivedSamples()[0].Timestamp().ticks100ns);
            Assert::AreEqual(MediaDuration::FromSeconds(2).ticks100ns, sinkFactory.nullSink->ReceivedSamples()[1].Timestamp().ticks100ns);
        }

        TEST_METHOD(Session_RoutesSamplesThroughAllMatchingProcessorsInOrder)
        {
            ManualTimeProvider timeProvider;
            RoutingSourceFactory sourceFactory;
            ChainedAudioProcessorFactory processorFactory;
            RoutingSinkFactory sinkFactory;
            CapturePipelineSession session = CreateSession(timeProvider, sourceFactory, processorFactory, sinkFactory);

            Assert::IsTrue(session.Start().IsSuccess());
            Assert::IsTrue(sourceFactory.audioSource->Emit(SampleBuilder::Audio()).IsSuccess());

            Assert::AreEqual(static_cast<size_t>(1), sinkFactory.nullSink->ReceivedSamples().size());
            const AudioSample& audio = std::get<AudioSample>(sinkFactory.nullSink->ReceivedSamples()[0].data);
            Assert::AreEqual(static_cast<size_t>(6), audio.pcmData.size());
            Assert::AreEqual(5, static_cast<int>(audio.pcmData[0]));
            Assert::AreEqual(6, static_cast<int>(audio.pcmData[1]));
            Assert::AreEqual(7, static_cast<int>(audio.pcmData[2]));
            Assert::AreEqual(8, static_cast<int>(audio.pcmData[3]));
            Assert::AreEqual(9, static_cast<int>(audio.pcmData[4]));
            Assert::AreEqual(10, static_cast<int>(audio.pcmData[5]));
        }

        TEST_METHOD(Session_DropsSamplesWhilePausedAndResumesClockContinuity)
        {
            ManualTimeProvider timeProvider;
            RoutingSourceFactory sourceFactory;
            RoutingProcessorFactory processorFactory;
            RoutingSinkFactory sinkFactory;
            CapturePipelineSession session = CreateSession(timeProvider, sourceFactory, processorFactory, sinkFactory);

            Assert::IsTrue(session.Start().IsSuccess());

            timeProvider.Advance(MediaDuration::FromSeconds(1));
            Assert::IsTrue(sourceFactory.videoSource->Emit(SampleBuilder::Video()).IsSuccess());

            Assert::IsTrue(session.Pause().IsSuccess());
            timeProvider.Advance(MediaDuration::FromSeconds(5));
            Assert::IsTrue(sourceFactory.videoSource->Emit(SampleBuilder::Video()).IsSuccess());

            Assert::IsTrue(session.Resume().IsSuccess());
            timeProvider.Advance(MediaDuration::FromSeconds(2));
            Assert::IsTrue(sourceFactory.videoSource->Emit(SampleBuilder::Video()).IsSuccess());

            Assert::AreEqual(static_cast<size_t>(2), sinkFactory.nullSink->ReceivedSamples().size());
            Assert::AreEqual(MediaDuration::FromSeconds(1).ticks100ns, sinkFactory.nullSink->ReceivedSamples()[0].Timestamp().ticks100ns);
            Assert::AreEqual(MediaDuration::FromSeconds(3).ticks100ns, sinkFactory.nullSink->ReceivedSamples()[1].Timestamp().ticks100ns);
        }

        TEST_METHOD(Session_PauseResumeInvalidTransitions_ReturnInvalidState)
        {
            ManualTimeProvider timeProvider;
            RoutingSourceFactory sourceFactory;
            RoutingProcessorFactory processorFactory;
            RoutingSinkFactory sinkFactory;
            CapturePipelineSession session = CreateSession(timeProvider, sourceFactory, processorFactory, sinkFactory);

            const OperationResult pauseBeforeStart = session.Pause();
            Assert::IsTrue(pauseBeforeStart.IsFailure());
            Assert::AreEqual("Pause", pauseBeforeStart.diagnostic->operation.c_str());

            Assert::IsTrue(session.Start().IsSuccess());

            const OperationResult resumeBeforePause = session.Resume();
            Assert::IsTrue(resumeBeforePause.IsFailure());
            Assert::AreEqual("Resume", resumeBeforePause.diagnostic->operation.c_str());
        }
    };
}
