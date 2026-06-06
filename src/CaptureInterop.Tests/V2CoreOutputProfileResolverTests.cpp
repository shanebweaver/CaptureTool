#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Core/OutputProfileResolver.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;

namespace
{
    DesktopSourceConfig CreateDesktopSource(uint32_t id)
    {
        DesktopSourceConfig desktop;
        desktop.id = SourceId::FromValue(id);
        desktop.name = "Desktop";
        desktop.frameRate = Rational::From(60, 1);
        return desktop;
    }

    SystemAudioSourceConfig CreateArmedAudioSource(uint32_t id)
    {
        SystemAudioSourceConfig audio;
        audio.id = SourceId::FromValue(id);
        audio.name = "System audio";
        audio.armed = true;
        return audio;
    }

    CapturePipelineConfig CreateMp4VideoConfig()
    {
        CapturePipelineConfig config;
        config.sources.push_back(SourceConfig::Desktop(CreateDesktopSource(1)));
        config.output.container = ContainerFormat::Mp4;
        config.output.outputPath = L"C:\\Temp\\capture.mp4";
        config.output.video = VideoEncodingSettings{ VideoCodec::H264, 8'000'000, Rational::From(60, 1), 120, true };
        return config;
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2CoreOutputProfileResolverTests)
    {
    public:
        TEST_METHOD(Resolve_Mp4VideoOnly_PlansH264VideoStream)
        {
            const CapturePipelineConfig config = CreateMp4VideoConfig();
            OutputProfileResolver resolver;

            const OutputProfileResolutionResult result = resolver.Resolve(config);

            Assert::IsTrue(result.IsSuccess());
            Assert::IsTrue(result.plan.has_value());
            Assert::AreEqual(static_cast<size_t>(1), result.plan->streams.size());
            Assert::IsTrue(result.plan->HasVideoStream());
            Assert::IsFalse(result.plan->HasAudioStream());
            Assert::AreEqual(1u, result.plan->streams[0].sourceId.value);
            Assert::AreEqual(1u, result.plan->streams[0].streamId.value);
            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(result.plan->streams[0].kind));
            Assert::AreEqual(static_cast<int>(VideoCodec::H264), static_cast<int>(result.plan->streams[0].video->codec));
        }

        TEST_METHOD(Resolve_Mp4VideoAndAudio_PlansH264AndAacStreams)
        {
            CapturePipelineConfig config = CreateMp4VideoConfig();
            config.sources.push_back(SourceConfig::SystemAudio(CreateArmedAudioSource(2)));
            config.output.audio = AudioEncodingSettings{ AudioCodec::Aac, 192000, 48000, 2 };

            OutputProfileResolver resolver;
            const OutputProfileResolutionResult result = resolver.Resolve(config);

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(static_cast<size_t>(2), result.plan->streams.size());
            Assert::IsTrue(result.plan->HasVideoStream());
            Assert::IsTrue(result.plan->HasAudioStream());
            Assert::AreEqual(1u, result.plan->streams[0].sourceId.value);
            Assert::AreEqual(2u, result.plan->streams[1].sourceId.value);
            Assert::AreEqual(static_cast<int>(AudioCodec::Aac), static_cast<int>(result.plan->streams[1].audio->codec));
        }

        TEST_METHOD(Resolve_Mp3AudioOnly_PlansAudioOnlyStream)
        {
            CapturePipelineConfig config;
            config.sources.push_back(SourceConfig::SystemAudio(CreateArmedAudioSource(2)));
            config.output.container = ContainerFormat::Mp3;
            config.output.outputPath = L"C:\\Temp\\capture.mp3";
            config.output.audio = AudioEncodingSettings{ AudioCodec::Mp3, 192000, 48000, 2 };

            OutputProfileResolver resolver;
            const OutputProfileResolutionResult result = resolver.Resolve(config);

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(static_cast<size_t>(1), result.plan->streams.size());
            Assert::IsFalse(result.plan->HasVideoStream());
            Assert::IsTrue(result.plan->HasAudioStream());
            Assert::AreEqual(static_cast<int>(ContainerFormat::Mp3), static_cast<int>(result.plan->container));
            Assert::AreEqual(static_cast<int>(AudioCodec::Mp3), static_cast<int>(result.plan->streams[0].audio->codec));
        }

        TEST_METHOD(Resolve_Mp3WithIncidentalVideo_PrunesVideoAndWarns)
        {
            CapturePipelineConfig config;
            config.sources.push_back(SourceConfig::Desktop(CreateDesktopSource(1)));
            config.sources.push_back(SourceConfig::SystemAudio(CreateArmedAudioSource(2)));
            config.output.container = ContainerFormat::Mp3;
            config.output.outputPath = L"C:\\Temp\\capture.mp3";
            config.output.audio = AudioEncodingSettings{ AudioCodec::Mp3, 192000, 48000, 2 };

            OutputProfileResolver resolver;
            const OutputProfileResolutionResult result = resolver.Resolve(config);

            Assert::IsTrue(result.IsSuccess());
            Assert::IsTrue(result.diagnostics.HasWarnings());
            Assert::AreEqual(static_cast<size_t>(1), result.plan->streams.size());
            Assert::IsFalse(result.plan->HasVideoStream());
            Assert::AreEqual("Incidental video sources are pruned from MP3 output", result.diagnostics.warnings[0].message.c_str());
        }

        TEST_METHOD(Resolve_Mp3WithExplicitVideoRequirement_Fails)
        {
            CapturePipelineConfig config;
            config.sources.push_back(SourceConfig::Desktop(CreateDesktopSource(1)));
            config.sources.push_back(SourceConfig::SystemAudio(CreateArmedAudioSource(2)));
            config.output.container = ContainerFormat::Mp3;
            config.output.outputPath = L"C:\\Temp\\capture.mp3";
            config.output.video = VideoEncodingSettings{ VideoCodec::H264, 8'000'000, Rational::From(60, 1), 120, true };
            config.output.audio = AudioEncodingSettings{ AudioCodec::Mp3, 192000, 48000, 2 };

            OutputProfileResolver resolver;
            const OutputProfileResolutionResult result = resolver.Resolve(config);

            Assert::IsFalse(result.IsSuccess());
            Assert::IsFalse(result.plan.has_value());
            Assert::AreEqual(static_cast<size_t>(1), result.diagnostics.errors.size());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::UnsupportedOperation),
                static_cast<uint32_t>(result.diagnostics.errors[0].code));
            Assert::AreEqual("MP3 output does not support video streams", result.diagnostics.errors[0].message.c_str());
        }

        TEST_METHOD(Resolve_Mp3WithVideoOnlySources_Fails)
        {
            CapturePipelineConfig config;
            config.sources.push_back(SourceConfig::Desktop(CreateDesktopSource(1)));
            config.output.container = ContainerFormat::Mp3;
            config.output.outputPath = L"C:\\Temp\\capture.mp3";
            config.output.audio = AudioEncodingSettings{ AudioCodec::Mp3, 192000, 48000, 2 };

            OutputProfileResolver resolver;
            const OutputProfileResolutionResult result = resolver.Resolve(config);

            Assert::IsFalse(result.IsSuccess());
            Assert::IsFalse(result.plan.has_value());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::NotFound),
                static_cast<uint32_t>(result.diagnostics.errors[0].code));
            Assert::AreEqual("MP3 output requires an armed system audio source", result.diagnostics.errors[0].message.c_str());
        }

        TEST_METHOD(Resolve_Mp4WithUnsupportedVideoCodec_Fails)
        {
            CapturePipelineConfig config = CreateMp4VideoConfig();
            config.output.video->codec = VideoCodec::Hevc;

            OutputProfileResolver resolver;
            const OutputProfileResolutionResult result = resolver.Resolve(config);

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual("MP4 output supports H.264 video in the initial profile", result.diagnostics.errors[0].message.c_str());
        }

        TEST_METHOD(Resolve_Mp4WithUnsupportedAudioCodec_Fails)
        {
            CapturePipelineConfig config = CreateMp4VideoConfig();
            config.sources.push_back(SourceConfig::SystemAudio(CreateArmedAudioSource(2)));
            config.output.audio = AudioEncodingSettings{ AudioCodec::Mp3, 192000, 48000, 2 };

            OutputProfileResolver resolver;
            const OutputProfileResolutionResult result = resolver.Resolve(config);

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual("MP4 output supports AAC audio in the initial profile", result.diagnostics.errors[0].message.c_str());
        }

        TEST_METHOD(Resolve_WavProfile_IsExplicitlyUnsupportedInInitialResolver)
        {
            CapturePipelineConfig config;
            config.sources.push_back(SourceConfig::SystemAudio(CreateArmedAudioSource(2)));
            config.output.container = ContainerFormat::Wav;
            config.output.outputPath = L"C:\\Temp\\capture.wav";
            config.output.audio = AudioEncodingSettings{ AudioCodec::Pcm, 0, 48000, 2 };

            OutputProfileResolver resolver;
            const OutputProfileResolutionResult result = resolver.Resolve(config);

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::UnsupportedOperation),
                static_cast<uint32_t>(result.diagnostics.errors[0].code));
            Assert::AreEqual("WAV output planning is not part of the initial core profile resolver", result.diagnostics.errors[0].message.c_str());
        }
    };
}
