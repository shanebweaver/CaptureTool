#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Output/MediaFoundationFileSink.h"

#include <initializer_list>
#include <memory>
#include <optional>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;
using namespace CaptureInterop::V2::Output;

namespace
{
    class FakeMediaFoundationRuntimeApi final : public IMediaFoundationRuntimeApi
    {
    public:
        explicit FakeMediaFoundationRuntimeApi(long startupResult = S_OK) noexcept
            : m_startupResult(startupResult)
        {
        }

        [[nodiscard]] long Startup() noexcept override
        {
            ++startupCalls;
            return m_startupResult;
        }

        void Shutdown() noexcept override
        {
            ++shutdownCalls;
        }

        int startupCalls{ 0 };
        int shutdownCalls{ 0 };

    private:
        long m_startupResult{ S_OK };
    };

    class FakeSinkWriterFactory final : public IMediaFoundationSinkWriterFactory
    {
    public:
        [[nodiscard]] MediaFoundationSinkWriterCreationResult CreateFileSinkWriter(
            const std::wstring& outputPath) noexcept override
        {
            ++createCalls;
            lastOutputPath = outputPath;
            if (nextResult.IsFailure())
            {
                return MediaFoundationSinkWriterCreationResult{
                    nextResult,
                    {},
                    attributesConfigured,
                    false
                };
            }

            return MediaFoundationSinkWriterCreationResult{
                OperationResult::Success(),
                {},
                attributesConfigured,
                writerCreated
            };
        }

        int createCalls{ 0 };
        std::wstring lastOutputPath;
        bool attributesConfigured{ true };
        bool writerCreated{ true };
        OperationResult nextResult{ OperationResult::Success() };
    };

    struct SinkHarness
    {
        explicit SinkHarness(long startupResult = S_OK)
            : runtimeApi(std::make_shared<FakeMediaFoundationRuntimeApi>(startupResult)),
              runtime(std::make_shared<MediaFoundationRuntime>(runtimeApi)),
              sinkWriterFactory(std::make_shared<FakeSinkWriterFactory>()),
              sink(MediaFoundationSinkProfileValidator{}, runtime, sinkWriterFactory)
        {
        }

        std::shared_ptr<FakeMediaFoundationRuntimeApi> runtimeApi;
        std::shared_ptr<MediaFoundationRuntime> runtime;
        std::shared_ptr<FakeSinkWriterFactory> sinkWriterFactory;
        MediaFoundationFileSink sink;
    };

    OutputStreamPlan CreateVideoStream(
        uint32_t streamId = 1,
        VideoCodec codec = VideoCodec::H264,
        Rational frameRate = Rational::From(60, 1),
        uint32_t bitrate = 8'000'000)
    {
        return OutputStreamPlan{
            StreamId::FromValue(streamId),
            SourceId::FromValue(10),
            MediaKind::Video,
            VideoEncodingSettings{ codec, bitrate, frameRate, 120, true },
            std::nullopt
        };
    }

    OutputStreamPlan CreateAudioStream(
        uint32_t streamId = 2,
        AudioCodec codec = AudioCodec::Aac,
        uint32_t sampleRate = 48000,
        uint16_t channels = 2,
        uint32_t bitrate = 192000)
    {
        return OutputStreamPlan{
            StreamId::FromValue(streamId),
            SourceId::FromValue(20),
            MediaKind::Audio,
            std::nullopt,
            AudioEncodingSettings{ codec, bitrate, sampleRate, channels }
        };
    }

    OutputPlan CreatePlan(ContainerFormat container, std::initializer_list<OutputStreamPlan> streams)
    {
        OutputPlan plan;
        plan.container = container;
        plan.outputPath = container == ContainerFormat::Mp3
            ? L"C:\\Temp\\capture.mp3"
            : L"C:\\Temp\\capture.mp4";
        plan.streams.assign(streams.begin(), streams.end());
        return plan;
    }

    VideoSample CreateVideoSample(StreamId streamId = StreamId::FromValue(1))
    {
        VideoMediaType mediaType;
        mediaType.width = 2;
        mediaType.height = 2;
        mediaType.frameRate = Rational::From(60, 1);
        mediaType.pixelFormat = VideoPixelFormat::Bgra8;

        return VideoSample{
            SourceId::FromValue(10),
            streamId,
            MediaTime::Zero(),
            MediaDuration::FromMilliseconds(16),
            mediaType,
            std::vector<uint8_t>{ 1, 2, 3, 4 },
            1,
            VideoFrameDimensions{ 2, 2 }
        };
    }

    AudioSample CreateAudioSample(StreamId streamId = StreamId::FromValue(2))
    {
        AudioMediaType mediaType;
        mediaType.sampleRate = 48000;
        mediaType.channels = 2;
        mediaType.bitsPerSample = 16;
        mediaType.blockAlign = 4;
        mediaType.sampleFormat = AudioSampleFormat::Pcm16;

        return AudioSample{
            SourceId::FromValue(20),
            streamId,
            MediaTime::Zero(),
            MediaDuration::FromMilliseconds(10),
            mediaType,
            std::vector<uint8_t>{ 1, 2, 3, 4 }
        };
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2MediaFoundationFileSinkTests)
    {
    public:
        TEST_METHOD(Open_Mp4VideoOnlyPlan_BuildsStreamMap)
        {
            SinkHarness harness;
            const OutputPlan plan = CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(
                static_cast<int>(MediaFoundationFileSinkState::Opened),
                static_cast<int>(harness.sink.State()));
            const std::vector<MediaFoundationSinkStreamMapping> mappings = harness.sink.StreamMappings();
            Assert::AreEqual(static_cast<size_t>(1), mappings.size());
            Assert::AreEqual(1u, mappings[0].streamId.value);
            Assert::AreEqual(0u, mappings[0].sinkStreamIndex);
            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(mappings[0].kind));
            Assert::IsTrue(harness.sink.HasSinkWriter());
            Assert::AreEqual(1, harness.runtimeApi->startupCalls);
            Assert::AreEqual(1, harness.sinkWriterFactory->createCalls);
        }

        TEST_METHOD(Open_Mp4VideoPlusAudioPlan_MapsBothStreamsInOrder)
        {
            SinkHarness harness;
            const OutputPlan plan = CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(7), CreateAudioStream(8) });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsSuccess());
            const auto video = harness.sink.FindStream(StreamId::FromValue(7));
            const auto audio = harness.sink.FindStream(StreamId::FromValue(8));
            Assert::IsTrue(video.has_value());
            Assert::IsTrue(audio.has_value());
            Assert::AreEqual(0u, video->sinkStreamIndex);
            Assert::AreEqual(1u, audio->sinkStreamIndex);
            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(video->kind));
            Assert::AreEqual(static_cast<int>(MediaKind::Audio), static_cast<int>(audio->kind));
        }

        TEST_METHOD(Open_Mp3PlanWithVideoStream_FailsDefensively)
        {
            SinkHarness harness;
            const OutputPlan plan = CreatePlan(
                ContainerFormat::Mp3,
                { CreateVideoStream(), CreateAudioStream(2, AudioCodec::Mp3) });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::UnsupportedOperation),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual(
                static_cast<int>(MediaFoundationFileSinkState::Failed),
                static_cast<int>(harness.sink.State()));
            Assert::AreEqual(0, harness.runtimeApi->startupCalls);
            Assert::AreEqual(0, harness.sinkWriterFactory->createCalls);
        }

        TEST_METHOD(Open_DuplicateStreamIds_Fails)
        {
            SinkHarness harness;
            const OutputPlan plan = CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(4), CreateAudioStream(4) });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual("Duplicate output stream id", result.diagnostic->message.c_str());
            Assert::AreEqual(0, harness.runtimeApi->startupCalls);
            Assert::AreEqual(0, harness.sinkWriterFactory->createCalls);
        }

        TEST_METHOD(Open_MissingVideoFields_Fails)
        {
            SinkHarness harness;
            const OutputPlan plan = CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(1, VideoCodec::H264, Rational::Invalid(), 0) });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                "Video output stream is missing required media fields",
                result.diagnostic->message.c_str());
        }

        TEST_METHOD(Open_UnknownStreamKind_Fails)
        {
            OutputStreamPlan unknown = CreateVideoStream();
            unknown.kind = MediaKind::Unknown;

            SinkHarness harness;
            const OutputPlan plan = CreatePlan(ContainerFormat::Mp4, { unknown });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual("Output stream kind is not supported", result.diagnostic->message.c_str());
        }

        TEST_METHOD(FindStream_UnknownStream_ReturnsEmpty)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            const std::optional<MediaFoundationSinkStreamMapping> mapping =
                harness.sink.FindStream(StreamId::FromValue(99));

            Assert::IsFalse(mapping.has_value());
        }

        TEST_METHOD(WriteSample_BeforeOpen_ReturnsNotReady)
        {
            SinkHarness harness;

            const OperationResult result = harness.sink.WriteSample(MediaSample{ CreateVideoSample() });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::InvalidState),
                static_cast<uint32_t>(result.code));
        }

        TEST_METHOD(WriteSample_ForMappedStream_ReturnsStableNotImplemented)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            const OperationResult result = harness.sink.WriteSample(MediaSample{ CreateVideoSample() });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::UnsupportedOperation),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual(
                "Media Foundation sample writing is not implemented in this PRD slice",
                result.diagnostic->message.c_str());
        }

        TEST_METHOD(WriteSample_UnknownStream_ReturnsNotFound)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            const OperationResult result =
                harness.sink.WriteSample(MediaSample{ CreateVideoSample(StreamId::FromValue(99)) });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::NotFound),
                static_cast<uint32_t>(result.code));
        }

        TEST_METHOD(WriteSample_MismatchedKind_ReturnsValidationFailure)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            const OperationResult result =
                harness.sink.WriteSample(MediaSample{ CreateAudioSample(StreamId::FromValue(1)) });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::ValidationFailure),
                static_cast<uint32_t>(result.code));
        }

        TEST_METHOD(Finalize_AfterOpen_MovesToFinalized)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());
            Assert::AreEqual(1u, harness.runtime->ActiveLeaseCount());

            const OperationResult result = harness.sink.Finalize();

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(
                static_cast<int>(MediaFoundationFileSinkState::Finalized),
                static_cast<int>(harness.sink.State()));
            Assert::IsFalse(harness.sink.HasSinkWriter());
            Assert::AreEqual(0u, harness.runtime->ActiveLeaseCount());
            Assert::AreEqual(1, harness.runtimeApi->shutdownCalls);
            Assert::IsTrue(harness.sink.Finalize().IsSuccess());
        }

        TEST_METHOD(Open_EmptyOutputPath_FailsBeforeRuntimeLease)
        {
            SinkHarness harness;
            OutputPlan plan = CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() });
            plan.outputPath.clear();

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual("Output path is required", result.diagnostic->message.c_str());
            Assert::AreEqual(0, harness.runtimeApi->startupCalls);
            Assert::AreEqual(0, harness.sinkWriterFactory->createCalls);
        }

        TEST_METHOD(Open_RuntimeStartupFailure_DoesNotCreateSinkWriter)
        {
            SinkHarness harness(E_FAIL);

            const OperationResult result =
                harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() }));

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::NativeFailure),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual(1, harness.runtimeApi->startupCalls);
            Assert::AreEqual(0, harness.sinkWriterFactory->createCalls);
            Assert::AreEqual(0u, harness.runtime->ActiveLeaseCount());
        }

        TEST_METHOD(Open_SinkWriterCreationFailure_ReleasesRuntimeLease)
        {
            SinkHarness harness;
            harness.sinkWriterFactory->nextResult = OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "MediaFoundationFileSink",
                "CreateFileSinkWriter",
                "simulated writer creation failure",
                E_FAIL);

            const OperationResult result =
                harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() }));

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(1, harness.runtimeApi->startupCalls);
            Assert::AreEqual(1, harness.sinkWriterFactory->createCalls);
            Assert::AreEqual(0u, harness.runtime->ActiveLeaseCount());
            Assert::AreEqual(1, harness.runtimeApi->shutdownCalls);
            Assert::IsFalse(harness.sink.HasSinkWriter());
        }

        TEST_METHOD(Open_SinkWriterFactorySuccessWithoutWriter_FailsAndReleasesRuntimeLease)
        {
            SinkHarness harness;
            harness.sinkWriterFactory->writerCreated = false;

            const OperationResult result =
                harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() }));

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::InvalidState),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual(1, harness.sinkWriterFactory->createCalls);
            Assert::AreEqual(0u, harness.runtime->ActiveLeaseCount());
            Assert::AreEqual(1, harness.runtimeApi->shutdownCalls);
        }
    };
}
