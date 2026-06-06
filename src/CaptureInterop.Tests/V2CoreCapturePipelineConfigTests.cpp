#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Core/CapturePipelineConfig.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;

namespace CaptureInteropTests
{
    TEST_CLASS(V2CoreCapturePipelineConfigTests)
    {
    public:
        TEST_METHOD(CaptureRectangle_Default_IsInvalid)
        {
            const CaptureRectangle area;

            Assert::AreEqual(0, area.x);
            Assert::AreEqual(0, area.y);
            Assert::AreEqual(0u, area.width);
            Assert::AreEqual(0u, area.height);
            Assert::IsFalse(area.IsValid());
        }

        TEST_METHOD(CaptureRectangle_WithSize_IsValid)
        {
            const CaptureRectangle area{ 10, 20, 1280, 720 };

            Assert::IsTrue(area.IsValid());
        }

        TEST_METHOD(SourceConfig_Desktop_PreservesKindAndId)
        {
            DesktopSourceConfig desktop;
            desktop.id = SourceId::FromValue(1);
            desktop.name = "Primary monitor";
            desktop.displayId = "display-1";
            desktop.frameRate = Rational::From(60, 1);

            const SourceConfig source = SourceConfig::Desktop(desktop);

            Assert::AreEqual(
                static_cast<int>(SourceKind::Desktop),
                static_cast<int>(source.Kind()));
            Assert::IsTrue(source.IsVideo());
            Assert::IsFalse(source.IsAudio());
            Assert::AreEqual(1u, source.Id().value);
            Assert::IsNotNull(source.AsDesktop());
            Assert::IsNull(source.AsSystemAudio());
        }

        TEST_METHOD(SourceConfig_SystemAudio_PreservesKindIdAndArmedState)
        {
            SystemAudioSourceConfig audio;
            audio.id = SourceId::FromValue(2);
            audio.name = "Default render endpoint";
            audio.armed = true;
            audio.controls.initiallyMuted = true;
            audio.controls.initialGain.gainDb = -6.0f;

            const SourceConfig source = SourceConfig::SystemAudio(audio);

            Assert::AreEqual(
                static_cast<int>(SourceKind::SystemAudio),
                static_cast<int>(source.Kind()));
            Assert::IsFalse(source.IsVideo());
            Assert::IsTrue(source.IsAudio());
            Assert::AreEqual(2u, source.Id().value);
            Assert::IsNull(source.AsDesktop());
            Assert::IsNotNull(source.AsSystemAudio());
            Assert::IsTrue(source.AsSystemAudio()->armed);
            Assert::IsTrue(source.AsSystemAudio()->controls.initiallyMuted);
            Assert::AreEqual(-6.0f, source.AsSystemAudio()->controls.initialGain.gainDb);
        }

        TEST_METHOD(CapturePipelineConfig_Default_HasNoSourcesOrOutputStreams)
        {
            const CapturePipelineConfig config;

            Assert::AreEqual(static_cast<size_t>(0), config.sources.size());
            Assert::IsFalse(config.output.HasRequestedStreams());
            Assert::IsTrue(config.controls.pauseResumeEnabled);
            Assert::IsTrue(config.controls.runtimeAudioMuteEnabled);
            Assert::IsTrue(config.controls.runtimeAudioGainEnabled);
            Assert::IsTrue(config.diagnostics.collectCounters);
            Assert::IsFalse(config.diagnostics.verboseLogging);
        }

        TEST_METHOD(CapturePipelineConfig_CanExpressVideoOnlyMp4)
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

            Assert::AreEqual(static_cast<size_t>(1), config.sources.size());
            Assert::IsTrue(config.output.RequestsVideo());
            Assert::IsFalse(config.output.RequestsAudio());
            Assert::IsTrue(config.HasSource(SourceId::FromValue(1)));
        }

        TEST_METHOD(CapturePipelineConfig_CanExpressMp4WithArmedSystemAudio)
        {
            CapturePipelineConfig config;

            DesktopSourceConfig desktop;
            desktop.id = SourceId::FromValue(1);
            desktop.name = "Primary monitor";
            desktop.frameRate = Rational::From(60, 1);
            config.sources.push_back(SourceConfig::Desktop(desktop));

            SystemAudioSourceConfig audio;
            audio.id = SourceId::FromValue(2);
            audio.name = "System audio";
            audio.armed = true;
            audio.controls.initiallyMuted = false;
            audio.controls.initialGain.gainDb = 0.0f;
            config.sources.push_back(SourceConfig::SystemAudio(audio));

            config.output.container = ContainerFormat::Mp4;
            config.output.outputPath = L"C:\\Temp\\capture-with-audio.mp4";
            config.output.video = VideoEncodingSettings{ VideoCodec::H264, 8'000'000, Rational::From(60, 1), 120, true };
            config.output.audio = AudioEncodingSettings{ AudioCodec::Aac, 192000, 48000, 2 };

            const SystemAudioSourceConfig* foundAudio = config.FindSystemAudioSource(SourceId::FromValue(2));

            Assert::AreEqual(static_cast<size_t>(2), config.sources.size());
            Assert::IsTrue(config.output.RequestsVideo());
            Assert::IsTrue(config.output.RequestsAudio());
            Assert::IsNotNull(foundAudio);
            Assert::IsTrue(foundAudio->armed);
            Assert::IsFalse(foundAudio->controls.initiallyMuted);
            Assert::AreEqual(0.0f, foundAudio->controls.initialGain.gainDb);
        }

        TEST_METHOD(CapturePipelineConfig_CanExpressMp3WithAudioAndIncidentalVideo)
        {
            CapturePipelineConfig config;

            DesktopSourceConfig desktop;
            desktop.id = SourceId::FromValue(1);
            desktop.name = "Incidental monitor source";
            desktop.frameRate = Rational::From(30, 1);
            config.sources.push_back(SourceConfig::Desktop(desktop));

            SystemAudioSourceConfig audio;
            audio.id = SourceId::FromValue(2);
            audio.name = "System audio";
            audio.armed = true;
            audio.controls.initiallyMuted = false;
            config.sources.push_back(SourceConfig::SystemAudio(audio));

            config.output.container = ContainerFormat::Mp3;
            config.output.outputPath = L"C:\\Temp\\capture.mp3";
            config.output.audio = AudioEncodingSettings{ AudioCodec::Mp3, 192000, 48000, 2 };

            Assert::AreEqual(static_cast<size_t>(2), config.sources.size());
            Assert::IsTrue(config.sources[0].IsVideo());
            Assert::IsTrue(config.sources[1].IsAudio());
            Assert::IsFalse(config.output.RequestsVideo());
            Assert::IsTrue(config.output.RequestsAudio());
        }

        TEST_METHOD(CapturePipelineConfig_DistinguishesArmedFromInitiallyMuted)
        {
            SystemAudioSourceConfig unarmedButMuted;
            unarmedButMuted.id = SourceId::FromValue(2);
            unarmedButMuted.armed = false;
            unarmedButMuted.controls.initiallyMuted = true;

            SystemAudioSourceConfig armedAndMuted;
            armedAndMuted.id = SourceId::FromValue(3);
            armedAndMuted.armed = true;
            armedAndMuted.controls.initiallyMuted = true;

            Assert::IsFalse(unarmedButMuted.armed);
            Assert::IsTrue(unarmedButMuted.controls.initiallyMuted);
            Assert::IsTrue(armedAndMuted.armed);
            Assert::IsTrue(armedAndMuted.controls.initiallyMuted);
        }

        TEST_METHOD(CapturePipelineConfig_FindsAudioControlsBySourceId)
        {
            CapturePipelineConfig config;

            SystemAudioSourceConfig audio;
            audio.id = SourceId::FromValue(42);
            audio.armed = true;
            audio.controls.initiallyMuted = true;
            audio.controls.initialGain.gainDb = -12.0f;
            config.sources.push_back(SourceConfig::SystemAudio(audio));

            const SystemAudioSourceConfig* foundAudio = config.FindSystemAudioSource(SourceId::FromValue(42));
            const SystemAudioSourceConfig* missingAudio = config.FindSystemAudioSource(SourceId::FromValue(99));

            Assert::IsNotNull(foundAudio);
            Assert::IsTrue(foundAudio->controls.initiallyMuted);
            Assert::AreEqual(-12.0f, foundAudio->controls.initialGain.gainDb);
            Assert::IsNull(missingAudio);
        }
    };
}
