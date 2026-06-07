#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Core/CapturePipelineConfigValidator.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;

namespace
{
    CapturePipelineConfig CreateValidVideoOnlyMp4Config()
    {
        CapturePipelineConfig config;

        DesktopSourceConfig desktop;
        desktop.id = SourceId::FromValue(1);
        desktop.name = "Primary monitor";
        desktop.frameRate = Rational::From(60, 1);
        config.sources.push_back(SourceConfig::Desktop(desktop));

        config.output.container = ContainerFormat::Mp4;
        config.output.outputPath = L"C:\\Temp\\capture.mp4";
        config.output.video = VideoEncodingSettings{ VideoCodec::H264, 8'000'000, Rational::From(60, 1), 120, true };

        return config;
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2CoreCapturePipelineConfigValidatorTests)
    {
    public:
        TEST_METHOD(Validate_VideoOnlyMp4_Succeeds)
        {
            const CapturePipelineConfig config = CreateValidVideoOnlyMp4Config();
            const CapturePipelineConfigValidator validator;

            const ValidationResult result = validator.Validate(config);

            Assert::IsTrue(result.IsValid());
            Assert::AreEqual(static_cast<size_t>(0), result.errors.size());
        }

        TEST_METHOD(Validate_Mp4WithArmedAudio_Succeeds)
        {
            CapturePipelineConfig config = CreateValidVideoOnlyMp4Config();

            SystemAudioSourceConfig audio;
            audio.id = SourceId::FromValue(2);
            audio.name = "System audio";
            audio.armed = true;
            audio.controls.initiallyMuted = true;
            audio.controls.initialGain.gainDb = -6.0f;
            config.sources.push_back(SourceConfig::SystemAudio(audio));
            config.output.audio = AudioEncodingSettings{ AudioCodec::Aac, 192000, 48000, 2 };

            const CapturePipelineConfigValidator validator;
            const ValidationResult result = validator.Validate(config);

            Assert::IsTrue(result.IsValid());
        }

        TEST_METHOD(Validate_DuplicateSourceIds_Fails)
        {
            CapturePipelineConfig config = CreateValidVideoOnlyMp4Config();

            SystemAudioSourceConfig audio;
            audio.id = SourceId::FromValue(1);
            audio.armed = true;
            config.sources.push_back(SourceConfig::SystemAudio(audio));

            const CapturePipelineConfigValidator validator;
            const ValidationResult result = validator.Validate(config);

            Assert::IsFalse(result.IsValid());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::ValidationFailure),
                static_cast<uint32_t>(result.errors[0].code));
            Assert::AreEqual("Duplicate source id", result.errors[0].message.c_str());
        }

        TEST_METHOD(Validate_InvalidSourceId_Fails)
        {
            CapturePipelineConfig config = CreateValidVideoOnlyMp4Config();
            config.sources.clear();

            DesktopSourceConfig desktop;
            desktop.id = SourceId::Invalid();
            desktop.frameRate = Rational::From(60, 1);
            config.sources.push_back(SourceConfig::Desktop(desktop));

            const CapturePipelineConfigValidator validator;
            const ValidationResult result = validator.Validate(config);

            Assert::IsFalse(result.IsValid());
            Assert::AreEqual("Source id is required", result.errors[0].message.c_str());
        }

        TEST_METHOD(Validate_MissingOutputPath_Fails)
        {
            CapturePipelineConfig config = CreateValidVideoOnlyMp4Config();
            config.output.outputPath.clear();

            const CapturePipelineConfigValidator validator;
            const ValidationResult result = validator.Validate(config);

            Assert::IsFalse(result.IsValid());
            Assert::AreEqual("Output path is required", result.errors[0].message.c_str());
        }

        TEST_METHOD(Validate_MissingOutputStreams_Fails)
        {
            CapturePipelineConfig config = CreateValidVideoOnlyMp4Config();
            config.output.video.reset();

            const CapturePipelineConfigValidator validator;
            const ValidationResult result = validator.Validate(config);

            Assert::IsFalse(result.IsValid());
            Assert::AreEqual("At least one output stream is required", result.errors[0].message.c_str());
        }

        TEST_METHOD(Validate_InvalidOutputContainer_Fails)
        {
            CapturePipelineConfig config = CreateValidVideoOnlyMp4Config();
            config.output.container = static_cast<ContainerFormat>(999);

            const CapturePipelineConfigValidator validator;
            const ValidationResult result = validator.Validate(config);

            Assert::IsFalse(result.IsValid());
            Assert::AreEqual("Output container is not supported", result.errors[0].message.c_str());
        }

        TEST_METHOD(Validate_InvalidVideoCodec_Fails)
        {
            CapturePipelineConfig config = CreateValidVideoOnlyMp4Config();
            config.output.video->codec = static_cast<VideoCodec>(999);

            const CapturePipelineConfigValidator validator;
            const ValidationResult result = validator.Validate(config);

            Assert::IsFalse(result.IsValid());
            Assert::AreEqual("Video codec is not supported", result.errors[0].message.c_str());
        }

        TEST_METHOD(Validate_InvalidAudioCodec_Fails)
        {
            CapturePipelineConfig config = CreateValidVideoOnlyMp4Config();
            config.output.audio = AudioEncodingSettings{ static_cast<AudioCodec>(999), 192000, 48000, 2 };

            const CapturePipelineConfigValidator validator;
            const ValidationResult result = validator.Validate(config);

            Assert::IsFalse(result.IsValid());
            Assert::AreEqual("Audio codec is not supported", result.errors[0].message.c_str());
        }

        TEST_METHOD(Validate_AudioGainBelowRange_FailsWithRangeError)
        {
            CapturePipelineConfig config = CreateValidVideoOnlyMp4Config();

            SystemAudioSourceConfig audio;
            audio.id = SourceId::FromValue(2);
            audio.armed = true;
            audio.controls.initialGain.gainDb = AudioGainSettings::MinimumGainDb - 0.1f;
            config.sources.push_back(SourceConfig::SystemAudio(audio));
            config.output.audio = AudioEncodingSettings{ AudioCodec::Aac, 192000, 48000, 2 };

            const CapturePipelineConfigValidator validator;
            const ValidationResult result = validator.Validate(config);

            Assert::IsFalse(result.IsValid());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::RangeError),
                static_cast<uint32_t>(result.errors[0].code));
            Assert::AreEqual("Audio gain is outside the supported range", result.errors[0].message.c_str());
        }

        TEST_METHOD(Validate_UnarmedInitiallyMutedAudio_FailsWithUnsupportedOperation)
        {
            CapturePipelineConfig config = CreateValidVideoOnlyMp4Config();

            SystemAudioSourceConfig audio;
            audio.id = SourceId::FromValue(2);
            audio.armed = false;
            audio.controls.initiallyMuted = true;
            config.sources.push_back(SourceConfig::SystemAudio(audio));

            const CapturePipelineConfigValidator validator;
            const ValidationResult result = validator.Validate(config);

            Assert::IsFalse(result.IsValid());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::UnsupportedOperation),
                static_cast<uint32_t>(result.errors[0].code));
            Assert::AreEqual("An unarmed audio source cannot be initially muted", result.errors[0].message.c_str());
        }

        TEST_METHOD(Validate_MicrophoneSource_ReturnsUnsupportedOperation)
        {
            CapturePipelineConfig config = CreateValidVideoOnlyMp4Config();

            MicrophoneSourceConfig microphone;
            microphone.id = SourceId::FromValue(3);
            microphone.audioStreamId = StreamId::FromValue(30);
            microphone.armed = true;
            config.sources.push_back(SourceConfig::Microphone(microphone));
            config.output.audio = AudioEncodingSettings{ AudioCodec::Aac, 192000, 48000, 2 };

            const CapturePipelineConfigValidator validator;
            const ValidationResult result = validator.Validate(config);

            Assert::IsFalse(result.IsValid());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::UnsupportedOperation),
                static_cast<uint32_t>(result.errors[0].code));
            Assert::AreEqual("Microphone capture is not implemented", result.errors[0].message.c_str());
        }

        TEST_METHOD(Validate_InvalidDesktopFrameRate_Fails)
        {
            CapturePipelineConfig config = CreateValidVideoOnlyMp4Config();
            DesktopSourceConfig* desktop = config.sources[0].AsDesktop();
            desktop->frameRate = Rational::Invalid();

            const CapturePipelineConfigValidator validator;
            const ValidationResult result = validator.Validate(config);

            Assert::IsFalse(result.IsValid());
            Assert::AreEqual("Desktop source frame rate is required", result.errors[0].message.c_str());
        }

        TEST_METHOD(Validate_InvalidCaptureArea_Fails)
        {
            CapturePipelineConfig config = CreateValidVideoOnlyMp4Config();
            DesktopSourceConfig* desktop = config.sources[0].AsDesktop();
            desktop->captureArea = CaptureRectangle{ 0, 0, 0, 720 };

            const CapturePipelineConfigValidator validator;
            const ValidationResult result = validator.Validate(config);

            Assert::IsFalse(result.IsValid());
            Assert::AreEqual("Desktop source capture area must have non-zero size", result.errors[0].message.c_str());
        }

        TEST_METHOD(Validate_MultipleFailures_AggregatesStructuredErrors)
        {
            CapturePipelineConfig config = CreateValidVideoOnlyMp4Config();
            config.output.outputPath.clear();
            config.output.video.reset();

            const CapturePipelineConfigValidator validator;
            const ValidationResult result = validator.Validate(config);

            Assert::IsFalse(result.IsValid());
            Assert::AreEqual(static_cast<size_t>(2), result.errors.size());
            Assert::AreEqual("CapturePipelineConfigValidator", result.errors[0].component.c_str());
            Assert::AreEqual("Validate", result.errors[0].operation.c_str());
        }
    };
}
