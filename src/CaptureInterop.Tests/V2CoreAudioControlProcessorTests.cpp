#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Core/AudioControlProcessors.h"
#include "V2/Core/CapturePipelineSession.h"
#include "V2CoreTestComponents.h"

#include <memory>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;
using namespace CaptureInterop::V2::Testing;

namespace
{
    class TestAudioGainProcessor final : public IMediaProcessor, public IAudioGainProcessor
    {
    public:
        explicit TestAudioGainProcessor(SourceId sourceId) noexcept
            : m_sourceId(sourceId)
        {
        }

        MediaKind Kind() const noexcept override { return MediaKind::Audio; }
        SourceId ControlledSource() const noexcept override { return m_sourceId; }
        float GainDb() const noexcept override { return m_gainDb; }

        OperationResult SetGainDb(float gainDb) noexcept override
        {
            AudioGainSettings settings;
            settings.gainDb = gainDb;
            if (!settings.IsInSupportedRange())
            {
                return OperationResult::Failure(
                    CoreResultCode::RangeError,
                    "TestAudioGainProcessor",
                    "SetGainDb",
                    "Audio gain is outside the supported range");
            }

            m_gainDb = gainDb;
            return OperationResult::Success();
        }

        OperationResult Configure(const MediaType&, const MediaType&) noexcept override
        {
            return OperationResult::Success();
        }

        OperationResult Process(const MediaSample& sample) noexcept override
        {
            m_appliedGains.push_back(m_gainDb);
            if (m_outputHandler)
            {
                m_outputHandler(sample);
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

        const std::vector<float>& AppliedGains() const noexcept
        {
            return m_appliedGains;
        }

    private:
        SourceId m_sourceId;
        float m_gainDb{ AudioGainSettings::DefaultGainDb };
        std::vector<float> m_appliedGains;
        MediaSampleHandler m_outputHandler;
    };

    class TestAudioMuteProcessor final : public IMediaProcessor, public IAudioMuteProcessor
    {
    public:
        explicit TestAudioMuteProcessor(SourceId sourceId) noexcept
            : m_sourceId(sourceId)
        {
        }

        MediaKind Kind() const noexcept override { return MediaKind::Audio; }
        SourceId ControlledSource() const noexcept override { return m_sourceId; }
        bool IsMuted() const noexcept override { return m_muted; }

        OperationResult SetMuted(bool muted) noexcept override
        {
            m_muted = muted;
            return OperationResult::Success();
        }

        OperationResult Configure(const MediaType&, const MediaType&) noexcept override
        {
            return OperationResult::Success();
        }

        OperationResult Process(const MediaSample& sample) noexcept override
        {
            if (!m_outputHandler)
            {
                return OperationResult::Success();
            }

            if (const auto* audio = std::get_if<AudioSample>(&sample.data))
            {
                m_outputHandler(MediaSample{ m_muted ? AudioSilenceGenerator::CreateSilenceLike(*audio) : *audio });
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
        SourceId m_sourceId;
        bool m_muted{ false };
        MediaSampleHandler m_outputHandler;
    };

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

    CapturePipelineConfig CreateAudioOnlyConfig()
    {
        CapturePipelineConfig config;

        SystemAudioSourceConfig audio;
        audio.id = SourceId::FromValue(2);
        audio.name = "Audio";
        audio.armed = true;
        config.sources.push_back(SourceConfig::SystemAudio(audio));

        config.output.container = ContainerFormat::Mp4;
        config.output.outputPath = L"C:\\Temp\\audio.mp4";
        config.output.audio = AudioEncodingSettings{ AudioCodec::Aac, 192000, 48000, 2 };

        return config;
    }

    class AudioOnlySourceFactory final : public IMediaSourceFactory
    {
    public:
        std::vector<std::unique_ptr<IMediaSource>> CreateSources(const CapturePipelineConfig&) override
        {
            std::vector<std::unique_ptr<IMediaSource>> sources;
            sources.push_back(std::make_unique<FakeAudioSource>());
            return sources;
        }
    };

    class AudioControlProcessorFactory final : public IMediaProcessorFactory
    {
    public:
        std::vector<std::unique_ptr<IMediaProcessor>> CreateProcessors(const OutputPlan&) override
        {
            auto gain = std::make_unique<TestAudioGainProcessor>(SourceId::FromValue(2));
            gainProcessor = gain.get();

            auto mute = std::make_unique<TestAudioMuteProcessor>(SourceId::FromValue(2));
            muteProcessor = mute.get();

            std::vector<std::unique_ptr<IMediaProcessor>> processors;
            processors.push_back(std::move(gain));
            processors.push_back(std::move(mute));
            return processors;
        }

        TestAudioGainProcessor* gainProcessor{ nullptr };
        TestAudioMuteProcessor* muteProcessor{ nullptr };
    };

    class AudioOnlySinkFactory final : public IOutputSinkFactory
    {
    public:
        std::unique_ptr<IOutputSink> CreateSink(const OutputPlan&) override
        {
            return std::make_unique<NullOutputSink>();
        }
    };
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2CoreAudioControlProcessorTests)
    {
    public:
        TEST_METHOD(AudioSilenceGenerator_PreservesMetadataAndZerosPayload)
        {
            AudioSample sample = SampleBuilder::Audio(MediaTime::FromTicks(500), MediaDuration::FromMilliseconds(20));
            sample.pcmData = { 1, 2, 3, 4, 5 };

            const AudioSample silence = AudioSilenceGenerator::CreateSilenceLike(sample);

            Assert::AreEqual(sample.sourceId.value, silence.sourceId.value);
            Assert::AreEqual(sample.streamId.value, silence.streamId.value);
            Assert::AreEqual(sample.timestamp.ticks100ns, silence.timestamp.ticks100ns);
            Assert::AreEqual(sample.duration.ticks100ns, silence.duration.ticks100ns);
            Assert::AreEqual(sample.pcmData.size(), silence.pcmData.size());
            for (uint8_t value : silence.pcmData)
            {
                Assert::AreEqual(static_cast<uint8_t>(0), value);
            }
        }

        TEST_METHOD(MuteProcessor_WhenMuted_EmitsSilence)
        {
            TestAudioMuteProcessor processor(SourceId::FromValue(2));
            MediaSample observed{ SampleBuilder::Audio() };
            CallbackRegistrationToken token = processor.RegisterOutputHandler(
                [&](const MediaSample& sample)
                {
                    observed = sample;
                });

            Assert::IsTrue(processor.SetMuted(true).IsSuccess());
            Assert::IsTrue(processor.Process(MediaSample{ SampleBuilder::Audio(MediaTime::FromTicks(42)) }).IsSuccess());

            const AudioSample& audio = std::get<AudioSample>(observed.data);
            Assert::IsNotNull(token.get());
            Assert::AreEqual(42LL, audio.timestamp.ticks100ns);
            for (uint8_t value : audio.pcmData)
            {
                Assert::AreEqual(static_cast<uint8_t>(0), value);
            }
        }

        TEST_METHOD(MuteProcessor_WhenUnmuted_PassesAudio)
        {
            TestAudioMuteProcessor processor(SourceId::FromValue(2));
            MediaSample observed{ SampleBuilder::Audio() };
            CallbackRegistrationToken token = processor.RegisterOutputHandler(
                [&](const MediaSample& sample)
                {
                    observed = sample;
                });

            Assert::IsTrue(processor.SetMuted(false).IsSuccess());
            Assert::IsTrue(processor.Process(MediaSample{ SampleBuilder::Audio() }).IsSuccess());

            const AudioSample& audio = std::get<AudioSample>(observed.data);
            Assert::IsNotNull(token.get());
            Assert::AreEqual(static_cast<uint8_t>(5), audio.pcmData[0]);
        }

        TEST_METHOD(GainProcessor_ChangesApplyToFutureSamplesOnly)
        {
            TestAudioGainProcessor processor(SourceId::FromValue(2));

            Assert::IsTrue(processor.Process(MediaSample{ SampleBuilder::Audio() }).IsSuccess());
            Assert::IsTrue(processor.SetGainDb(-12.0f).IsSuccess());
            Assert::IsTrue(processor.Process(MediaSample{ SampleBuilder::Audio() }).IsSuccess());

            Assert::AreEqual(static_cast<size_t>(2), processor.AppliedGains().size());
            Assert::AreEqual(0.0f, processor.AppliedGains()[0]);
            Assert::AreEqual(-12.0f, processor.AppliedGains()[1]);
        }

        TEST_METHOD(GainProcessor_RejectsOutOfRangeGain)
        {
            TestAudioGainProcessor processor(SourceId::FromValue(2));

            const OperationResult result = processor.SetGainDb(AudioGainSettings::MaximumGainDb + 1.0f);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::RangeError),
                static_cast<uint32_t>(result.code));
        }

        TEST_METHOD(Session_SetAudioMuted_RoutesBySourceId)
        {
            ManualTimeProvider timeProvider;
            AudioOnlySourceFactory sourceFactory;
            AudioControlProcessorFactory processorFactory;
            AudioOnlySinkFactory sinkFactory;
            CapturePipelineSession session(
                CreateAudioOnlyConfig(),
                sourceFactory,
                processorFactory,
                sinkFactory,
                std::make_unique<RecordingClock>(timeProvider));

            Assert::IsTrue(session.Start().IsSuccess());
            Assert::IsTrue(session.SetAudioMuted(SourceId::FromValue(2), true).IsSuccess());

            Assert::IsTrue(processorFactory.muteProcessor->IsMuted());
        }

        TEST_METHOD(Session_SetAudioMuted_MissingSourceReturnsNotFound)
        {
            ManualTimeProvider timeProvider;
            AudioOnlySourceFactory sourceFactory;
            AudioControlProcessorFactory processorFactory;
            AudioOnlySinkFactory sinkFactory;
            CapturePipelineSession session(
                CreateAudioOnlyConfig(),
                sourceFactory,
                processorFactory,
                sinkFactory,
                std::make_unique<RecordingClock>(timeProvider));

            Assert::IsTrue(session.Start().IsSuccess());
            const OperationResult result = session.SetAudioMuted(SourceId::FromValue(99), true);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::NotFound),
                static_cast<uint32_t>(result.code));
        }

        TEST_METHOD(Session_SetAudioGain_RoutesBySourceId)
        {
            ManualTimeProvider timeProvider;
            AudioOnlySourceFactory sourceFactory;
            AudioControlProcessorFactory processorFactory;
            AudioOnlySinkFactory sinkFactory;
            CapturePipelineSession session(
                CreateAudioOnlyConfig(),
                sourceFactory,
                processorFactory,
                sinkFactory,
                std::make_unique<RecordingClock>(timeProvider));

            Assert::IsTrue(session.Start().IsSuccess());
            Assert::IsTrue(session.SetAudioGain(SourceId::FromValue(2), -6.0f).IsSuccess());

            Assert::AreEqual(-6.0f, processorFactory.gainProcessor->GainDb());
        }

        TEST_METHOD(Session_SetAudioGain_RejectsOutOfRangeBeforeRouting)
        {
            ManualTimeProvider timeProvider;
            AudioOnlySourceFactory sourceFactory;
            AudioControlProcessorFactory processorFactory;
            AudioOnlySinkFactory sinkFactory;
            CapturePipelineSession session(
                CreateAudioOnlyConfig(),
                sourceFactory,
                processorFactory,
                sinkFactory,
                std::make_unique<RecordingClock>(timeProvider));

            Assert::IsTrue(session.Start().IsSuccess());
            const OperationResult result = session.SetAudioGain(
                SourceId::FromValue(2),
                AudioGainSettings::MinimumGainDb - 1.0f);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::RangeError),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual(0.0f, processorFactory.gainProcessor->GainDb());
        }

        TEST_METHOD(Session_AudioControlsAreAllowedWhilePaused)
        {
            ManualTimeProvider timeProvider;
            AudioOnlySourceFactory sourceFactory;
            AudioControlProcessorFactory processorFactory;
            AudioOnlySinkFactory sinkFactory;
            CapturePipelineSession session(
                CreateAudioOnlyConfig(),
                sourceFactory,
                processorFactory,
                sinkFactory,
                std::make_unique<RecordingClock>(timeProvider));

            Assert::IsTrue(session.Start().IsSuccess());
            Assert::IsTrue(session.Pause().IsSuccess());
            Assert::IsTrue(session.SetAudioMuted(SourceId::FromValue(2), true).IsSuccess());
            Assert::IsTrue(session.SetAudioGain(SourceId::FromValue(2), -3.0f).IsSuccess());

            Assert::IsTrue(processorFactory.muteProcessor->IsMuted());
            Assert::AreEqual(-3.0f, processorFactory.gainProcessor->GainDb());
        }
    };
}
