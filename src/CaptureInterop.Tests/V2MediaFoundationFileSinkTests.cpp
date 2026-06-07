#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Output/MediaFoundationFileSink.h"

#include <initializer_list>
#include <optional>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;
using namespace CaptureInterop::V2::Output;

namespace
{
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
            MediaFoundationFileSink sink;
            const OutputPlan plan = CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() });

            const OperationResult result = sink.Open(plan);

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(
                static_cast<int>(MediaFoundationFileSinkState::Opened),
                static_cast<int>(sink.State()));
            const std::vector<MediaFoundationSinkStreamMapping> mappings = sink.StreamMappings();
            Assert::AreEqual(static_cast<size_t>(1), mappings.size());
            Assert::AreEqual(1u, mappings[0].streamId.value);
            Assert::AreEqual(0u, mappings[0].sinkStreamIndex);
            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(mappings[0].kind));
        }

        TEST_METHOD(Open_Mp4VideoPlusAudioPlan_MapsBothStreamsInOrder)
        {
            MediaFoundationFileSink sink;
            const OutputPlan plan = CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(7), CreateAudioStream(8) });

            const OperationResult result = sink.Open(plan);

            Assert::IsTrue(result.IsSuccess());
            const auto video = sink.FindStream(StreamId::FromValue(7));
            const auto audio = sink.FindStream(StreamId::FromValue(8));
            Assert::IsTrue(video.has_value());
            Assert::IsTrue(audio.has_value());
            Assert::AreEqual(0u, video->sinkStreamIndex);
            Assert::AreEqual(1u, audio->sinkStreamIndex);
            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(video->kind));
            Assert::AreEqual(static_cast<int>(MediaKind::Audio), static_cast<int>(audio->kind));
        }

        TEST_METHOD(Open_Mp3PlanWithVideoStream_FailsDefensively)
        {
            MediaFoundationFileSink sink;
            const OutputPlan plan = CreatePlan(
                ContainerFormat::Mp3,
                { CreateVideoStream(), CreateAudioStream(2, AudioCodec::Mp3) });

            const OperationResult result = sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::UnsupportedOperation),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual(
                static_cast<int>(MediaFoundationFileSinkState::Failed),
                static_cast<int>(sink.State()));
        }

        TEST_METHOD(Open_DuplicateStreamIds_Fails)
        {
            MediaFoundationFileSink sink;
            const OutputPlan plan = CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(4), CreateAudioStream(4) });

            const OperationResult result = sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual("Duplicate output stream id", result.diagnostic->message.c_str());
        }

        TEST_METHOD(Open_MissingVideoFields_Fails)
        {
            MediaFoundationFileSink sink;
            const OutputPlan plan = CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(1, VideoCodec::H264, Rational::Invalid(), 0) });

            const OperationResult result = sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                "Video output stream is missing required media fields",
                result.diagnostic->message.c_str());
        }

        TEST_METHOD(Open_UnknownStreamKind_Fails)
        {
            OutputStreamPlan unknown = CreateVideoStream();
            unknown.kind = MediaKind::Unknown;

            MediaFoundationFileSink sink;
            const OutputPlan plan = CreatePlan(ContainerFormat::Mp4, { unknown });

            const OperationResult result = sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual("Output stream kind is not supported", result.diagnostic->message.c_str());
        }

        TEST_METHOD(FindStream_UnknownStream_ReturnsEmpty)
        {
            MediaFoundationFileSink sink;
            Assert::IsTrue(sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            const std::optional<MediaFoundationSinkStreamMapping> mapping =
                sink.FindStream(StreamId::FromValue(99));

            Assert::IsFalse(mapping.has_value());
        }

        TEST_METHOD(WriteSample_BeforeOpen_ReturnsNotReady)
        {
            MediaFoundationFileSink sink;

            const OperationResult result = sink.WriteSample(MediaSample{ CreateVideoSample() });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::InvalidState),
                static_cast<uint32_t>(result.code));
        }

        TEST_METHOD(WriteSample_ForMappedStream_ReturnsStableNotImplemented)
        {
            MediaFoundationFileSink sink;
            Assert::IsTrue(sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            const OperationResult result = sink.WriteSample(MediaSample{ CreateVideoSample() });

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
            MediaFoundationFileSink sink;
            Assert::IsTrue(sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            const OperationResult result =
                sink.WriteSample(MediaSample{ CreateVideoSample(StreamId::FromValue(99)) });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::NotFound),
                static_cast<uint32_t>(result.code));
        }

        TEST_METHOD(WriteSample_MismatchedKind_ReturnsValidationFailure)
        {
            MediaFoundationFileSink sink;
            Assert::IsTrue(sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            const OperationResult result =
                sink.WriteSample(MediaSample{ CreateAudioSample(StreamId::FromValue(1)) });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::ValidationFailure),
                static_cast<uint32_t>(result.code));
        }

        TEST_METHOD(Finalize_AfterOpen_MovesToFinalized)
        {
            MediaFoundationFileSink sink;
            Assert::IsTrue(sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            const OperationResult result = sink.Finalize();

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(
                static_cast<int>(MediaFoundationFileSinkState::Finalized),
                static_cast<int>(sink.State()));
            Assert::IsTrue(sink.Finalize().IsSuccess());
        }
    };
}
