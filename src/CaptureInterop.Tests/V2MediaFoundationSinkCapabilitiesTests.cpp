#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Core/MediaFoundationSinkCapabilities.h"

#include <initializer_list>
#include <optional>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;

namespace
{
    OutputStreamPlan CreateVideoStream(VideoCodec codec = VideoCodec::H264)
    {
        return OutputStreamPlan{
            StreamId::FromValue(1),
            SourceId::FromValue(1),
            MediaKind::Video,
            VideoEncodingSettings{ codec, 8'000'000, Rational::From(60, 1), 120, true },
            std::nullopt
        };
    }

    OutputStreamPlan CreateAudioStream(AudioCodec codec, uint32_t streamId = 2)
    {
        return OutputStreamPlan{
            StreamId::FromValue(streamId),
            SourceId::FromValue(2),
            MediaKind::Audio,
            std::nullopt,
            AudioEncodingSettings{ codec, 192000, 48000, 2 }
        };
    }

    OutputPlan CreateMp4Plan(std::initializer_list<OutputStreamPlan> streams)
    {
        OutputPlan plan;
        plan.container = ContainerFormat::Mp4;
        plan.outputPath = L"C:\\Temp\\capture.mp4";
        plan.streams.assign(streams.begin(), streams.end());
        return plan;
    }

    OutputPlan CreateMp3Plan(std::initializer_list<OutputStreamPlan> streams)
    {
        OutputPlan plan;
        plan.container = ContainerFormat::Mp3;
        plan.outputPath = L"C:\\Temp\\capture.mp3";
        plan.streams.assign(streams.begin(), streams.end());
        return plan;
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2MediaFoundationSinkCapabilitiesTests)
    {
    public:
        TEST_METHOD(DefaultCapabilities_AdvertiseMp4H264AndOptionalAac)
        {
            const MediaFoundationSinkCapabilities capabilities = MediaFoundationSinkCapabilities::Default();
            const MediaFoundationContainerCapability* mp4 = capabilities.Find(ContainerFormat::Mp4);

            Assert::IsNotNull(mp4);
            Assert::IsTrue(mp4->SupportsVideoCodec(VideoCodec::H264));
            Assert::IsTrue(mp4->SupportsAudioCodec(AudioCodec::Aac));
            Assert::AreEqual(
                static_cast<int>(OutputStreamRequirement::Optional),
                static_cast<int>(mp4->videoRequirement));
            Assert::AreEqual(
                static_cast<int>(OutputStreamRequirement::Optional),
                static_cast<int>(mp4->audioRequirement));
            Assert::AreEqual(1u, mp4->maxVideoStreams);
            Assert::AreEqual(1u, mp4->maxAudioStreams);
        }

        TEST_METHOD(DefaultCapabilities_AdvertiseMp3AudioOnly)
        {
            const MediaFoundationSinkCapabilities capabilities = MediaFoundationSinkCapabilities::Default();
            const MediaFoundationContainerCapability* mp3 = capabilities.Find(ContainerFormat::Mp3);

            Assert::IsNotNull(mp3);
            Assert::IsFalse(mp3->SupportsVideoCodec(VideoCodec::H264));
            Assert::IsTrue(mp3->SupportsAudioCodec(AudioCodec::Mp3));
            Assert::AreEqual(
                static_cast<int>(OutputStreamRequirement::Disallowed),
                static_cast<int>(mp3->videoRequirement));
            Assert::AreEqual(
                static_cast<int>(OutputStreamRequirement::Required),
                static_cast<int>(mp3->audioRequirement));
        }

        TEST_METHOD(Validate_Mp4VideoOnlyPlan_IsAccepted)
        {
            const MediaFoundationSinkProfileValidator validator;
            const OutputPlan plan = CreateMp4Plan({ CreateVideoStream() });

            const MediaFoundationProfileValidationResult result = validator.Validate(plan);

            Assert::IsTrue(result.IsSuccess());
        }

        TEST_METHOD(Validate_Mp4VideoPlusAudioPlan_IsAccepted)
        {
            const MediaFoundationSinkProfileValidator validator;
            const OutputPlan plan = CreateMp4Plan({ CreateVideoStream(), CreateAudioStream(AudioCodec::Aac) });

            const MediaFoundationProfileValidationResult result = validator.Validate(plan);

            Assert::IsTrue(result.IsSuccess());
        }

        TEST_METHOD(Validate_Mp4UnsupportedCodec_Fails)
        {
            const MediaFoundationSinkProfileValidator validator;
            const OutputPlan plan = CreateMp4Plan({ CreateVideoStream(VideoCodec::Hevc) });

            const MediaFoundationProfileValidationResult result = validator.Validate(plan);

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual("Video codec is not supported by output profile", result.diagnostics.errors[0].message.c_str());
        }

        TEST_METHOD(Validate_Mp4RejectsSecondAudioStream)
        {
            const MediaFoundationSinkProfileValidator validator;
            const OutputPlan plan = CreateMp4Plan({
                CreateVideoStream(),
                CreateAudioStream(AudioCodec::Aac),
                CreateAudioStream(AudioCodec::Aac, 3)
            });

            const MediaFoundationProfileValidationResult result = validator.Validate(plan);

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual("Output profile accepts too many audio streams", result.diagnostics.errors[0].message.c_str());
        }

        TEST_METHOD(Validate_Mp3AudioOnlyPlan_IsAcceptedAtProfileLayer)
        {
            const MediaFoundationSinkProfileValidator validator;
            const OutputPlan plan = CreateMp3Plan({ CreateAudioStream(AudioCodec::Mp3, 1) });

            const MediaFoundationProfileValidationResult result = validator.Validate(plan);

            Assert::IsTrue(result.IsSuccess());
        }

        TEST_METHOD(Validate_Mp3VideoStream_Fails)
        {
            const MediaFoundationSinkProfileValidator validator;
            const OutputPlan plan = CreateMp3Plan({ CreateVideoStream(), CreateAudioStream(AudioCodec::Mp3, 2) });

            const MediaFoundationProfileValidationResult result = validator.Validate(plan);

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual("Video stream is disallowed by output profile", result.diagnostics.errors[0].message.c_str());
        }

        TEST_METHOD(Validate_Mp3NoAudioPlan_Fails)
        {
            const MediaFoundationSinkProfileValidator validator;
            const OutputPlan plan = CreateMp3Plan({});

            const MediaFoundationProfileValidationResult result = validator.Validate(plan);

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual("Audio stream is required by output profile", result.diagnostics.errors[0].message.c_str());
        }
    };
}
