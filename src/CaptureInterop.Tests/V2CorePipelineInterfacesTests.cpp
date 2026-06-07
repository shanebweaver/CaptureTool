#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Core/PipelineInterfaces.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;

namespace
{
    class TestCallbackRegistration final : public ICallbackRegistration
    {
    };

    class TestVideoSource final : public IVideoCaptureSource
    {
    public:
        SourceDescriptor Describe() const override
        {
            return SourceDescriptor{ SourceId::FromValue(1), SourceKind::Desktop, "Desktop" };
        }

        std::vector<StreamDescriptor> Streams() const override
        {
            return { StreamDescriptor{ StreamId::FromValue(1), SourceId::FromValue(1), MediaKind::Video, "Video" } };
        }

        OperationResult Start() noexcept override
        {
            started = true;
            return OperationResult::Success();
        }

        OperationResult Stop() noexcept override
        {
            stopped = true;
            return OperationResult::Success();
        }

        CallbackRegistrationToken RegisterFrameArrivedHandler(VideoSampleHandler handler) override
        {
            frameHandler = std::move(handler);
            return std::make_unique<TestCallbackRegistration>();
        }

        bool started{ false };
        bool stopped{ false };
        VideoSampleHandler frameHandler;
    };

    class TestAudioSource final : public IAudioCaptureSource
    {
    public:
        SourceDescriptor Describe() const override
        {
            return SourceDescriptor{ SourceId::FromValue(2), SourceKind::SystemAudio, "System audio" };
        }

        std::vector<StreamDescriptor> Streams() const override
        {
            return { StreamDescriptor{ StreamId::FromValue(2), SourceId::FromValue(2), MediaKind::Audio, "Audio" } };
        }

        OperationResult Start() noexcept override
        {
            started = true;
            return OperationResult::Success();
        }

        OperationResult Stop() noexcept override
        {
            stopped = true;
            return OperationResult::Success();
        }

        CallbackRegistrationToken RegisterSampleArrivedHandler(AudioSampleHandler handler) override
        {
            sampleHandler = std::move(handler);
            return std::make_unique<TestCallbackRegistration>();
        }

        bool started{ false };
        bool stopped{ false };
        AudioSampleHandler sampleHandler;
    };

    class TestProcessor final : public IMediaProcessor
    {
    public:
        MediaKind Kind() const noexcept override
        {
            return MediaKind::Video;
        }

        OperationResult Configure(const MediaType&, const MediaType&) noexcept override
        {
            configured = true;
            return OperationResult::Success();
        }

        OperationResult Process(const MediaSample& sample) noexcept override
        {
            processedKind = sample.Kind();
            if (outputHandler)
            {
                outputHandler(sample);
            }

            return OperationResult::Success();
        }

        CallbackRegistrationToken RegisterOutputHandler(MediaSampleHandler handler) override
        {
            outputHandler = std::move(handler);
            return std::make_unique<TestCallbackRegistration>();
        }

        bool configured{ false };
        MediaKind processedKind{ MediaKind::Unknown };
        MediaSampleHandler outputHandler;
    };

    class TestSink final : public IOutputSink
    {
    public:
        OperationResult Open(const OutputPlan& plan) noexcept override
        {
            openedContainer = plan.container;
            return OperationResult::Success();
        }

        OperationResult WriteSample(const MediaSample& sample) noexcept override
        {
            lastSampleKind = sample.Kind();
            return OperationResult::Success();
        }

        OperationResult Finalize() noexcept override
        {
            finalized = true;
            return OperationResult::Success();
        }

        ContainerFormat openedContainer{ ContainerFormat::Mp4 };
        MediaKind lastSampleKind{ MediaKind::Unknown };
        bool finalized{ false };
    };

    VideoSample CreateVideoSample()
    {
        VideoMediaType mediaType;
        mediaType.width = 2;
        mediaType.height = 2;
        mediaType.frameRate = Rational::From(60, 1);
        mediaType.pixelFormat = VideoPixelFormat::Bgra8;

        return VideoSample{
            SourceId::FromValue(1),
            StreamId::FromValue(1),
            MediaTime::FromTicks(100),
            MediaDuration::FromMilliseconds(16),
            mediaType,
            std::vector<uint8_t>{ 1, 2, 3, 4 },
            12,
            VideoFrameDimensions{ 2, 2 }
        };
    }

    AudioSample CreateAudioSample()
    {
        AudioMediaType mediaType;
        mediaType.sampleRate = 48000;
        mediaType.channels = 2;
        mediaType.bitsPerSample = 16;
        mediaType.blockAlign = 4;
        mediaType.sampleFormat = AudioSampleFormat::Pcm16;

        return AudioSample{
            SourceId::FromValue(2),
            StreamId::FromValue(2),
            MediaTime::FromTicks(200),
            MediaDuration::FromMilliseconds(10),
            mediaType,
            std::vector<uint8_t>{ 1, 2, 3, 4 }
        };
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2CorePipelineInterfacesTests)
    {
    public:
        TEST_METHOD(MediaSample_Video_ExposesCommonFields)
        {
            MediaSample sample{ CreateVideoSample() };

            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(sample.Kind()));
            Assert::AreEqual(1u, sample.Source().value);
            Assert::AreEqual(1u, sample.Stream().value);
            Assert::AreEqual(100LL, sample.Timestamp().ticks100ns);
            Assert::AreEqual(MediaDuration::FromMilliseconds(16).ticks100ns, sample.Duration().ticks100ns);
            const VideoSample& video = std::get<VideoSample>(sample.data);
            Assert::AreEqual(12ull, video.sequenceNumber);
            Assert::AreEqual(2u, video.Dimensions().width);
            Assert::AreEqual(2u, video.Dimensions().height);
            Assert::IsFalse(video.HasTexture());
        }

        TEST_METHOD(MediaSample_Audio_ExposesCommonFields)
        {
            MediaSample sample{ CreateAudioSample() };

            Assert::AreEqual(static_cast<int>(MediaKind::Audio), static_cast<int>(sample.Kind()));
            Assert::AreEqual(2u, sample.Source().value);
            Assert::AreEqual(2u, sample.Stream().value);
            Assert::AreEqual(200LL, sample.Timestamp().ticks100ns);
            Assert::AreEqual(MediaDuration::FromMilliseconds(10).ticks100ns, sample.Duration().ticks100ns);
        }

        TEST_METHOD(VideoSource_Interface_CanBeImplementedByFake)
        {
            TestVideoSource source;

            const SourceDescriptor descriptor = source.Describe();
            const std::vector<StreamDescriptor> streams = source.Streams();
            CallbackRegistrationToken token = source.RegisterFrameArrivedHandler([](const VideoSample&) {});

            Assert::AreEqual(1u, descriptor.id.value);
            Assert::AreEqual(static_cast<int>(SourceKind::Desktop), static_cast<int>(descriptor.kind));
            Assert::AreEqual(static_cast<size_t>(1), streams.size());
            Assert::IsNotNull(token.get());
            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(source.Stop().IsSuccess());
            Assert::IsTrue(source.started);
            Assert::IsTrue(source.stopped);
        }

        TEST_METHOD(AudioSource_Interface_CanBeImplementedByFake)
        {
            TestAudioSource source;

            const SourceDescriptor descriptor = source.Describe();
            const std::vector<StreamDescriptor> streams = source.Streams();
            CallbackRegistrationToken token = source.RegisterSampleArrivedHandler([](const AudioSample&) {});

            Assert::AreEqual(2u, descriptor.id.value);
            Assert::AreEqual(static_cast<int>(SourceKind::SystemAudio), static_cast<int>(descriptor.kind));
            Assert::AreEqual(static_cast<size_t>(1), streams.size());
            Assert::IsNotNull(token.get());
            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(source.Stop().IsSuccess());
            Assert::IsTrue(source.started);
            Assert::IsTrue(source.stopped);
        }

        TEST_METHOD(Processor_Interface_CanPassSamplesToOutputHandler)
        {
            TestProcessor processor;
            MediaKind observedKind = MediaKind::Unknown;

            CallbackRegistrationToken token = processor.RegisterOutputHandler(
                [&](const MediaSample& sample)
                {
                    observedKind = sample.Kind();
                });

            const VideoMediaType videoType = CreateVideoSample().mediaType;
            Assert::IsTrue(processor.Configure(videoType, videoType).IsSuccess());
            Assert::IsTrue(processor.Process(MediaSample{ CreateVideoSample() }).IsSuccess());

            Assert::IsNotNull(token.get());
            Assert::IsTrue(processor.configured);
            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(processor.processedKind));
            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(observedKind));
        }

        TEST_METHOD(OutputSink_Interface_CanOpenWriteAndFinalize)
        {
            TestSink sink;
            OutputPlan plan;
            plan.container = ContainerFormat::Mp4;

            Assert::IsTrue(sink.Open(plan).IsSuccess());
            Assert::IsTrue(sink.WriteSample(MediaSample{ CreateAudioSample() }).IsSuccess());
            Assert::IsTrue(sink.Finalize().IsSuccess());

            Assert::AreEqual(static_cast<int>(ContainerFormat::Mp4), static_cast<int>(sink.openedContainer));
            Assert::AreEqual(static_cast<int>(MediaKind::Audio), static_cast<int>(sink.lastSampleKind));
            Assert::IsTrue(sink.finalized);
        }
    };
}
