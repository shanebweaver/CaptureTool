#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Core/MediaTypes.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;

namespace CaptureInteropTests
{
    TEST_CLASS(V2CoreMediaTypesTests)
    {
    public:
        TEST_METHOD(VideoMediaType_Default_IsInvalidAndPlatformNeutral)
        {
            const VideoMediaType mediaType;

            Assert::AreEqual(0u, mediaType.width);
            Assert::AreEqual(0u, mediaType.height);
            Assert::IsFalse(mediaType.frameRate.IsValid());
            Assert::AreEqual(
                static_cast<int>(VideoPixelFormat::Unknown),
                static_cast<int>(mediaType.pixelFormat));
            Assert::IsFalse(mediaType.IsValid());
        }

        TEST_METHOD(VideoMediaType_WithDimensionsFrameRateAndFormat_IsValid)
        {
            VideoMediaType mediaType;
            mediaType.width = 1920;
            mediaType.height = 1080;
            mediaType.frameRate = Rational::From(60, 1);
            mediaType.pixelFormat = VideoPixelFormat::Bgra8;
            mediaType.colorPrimaries = ColorPrimaries::Srgb;
            mediaType.transferFunction = TransferFunction::Srgb;
            mediaType.range = ColorRange::Full;

            Assert::IsTrue(mediaType.IsValid());
        }

        TEST_METHOD(AudioMediaType_Default_IsInvalidAndPlatformNeutral)
        {
            const AudioMediaType mediaType;

            Assert::AreEqual(0u, mediaType.sampleRate);
            Assert::AreEqual(static_cast<uint16_t>(0), mediaType.channels);
            Assert::AreEqual(static_cast<uint16_t>(0), mediaType.bitsPerSample);
            Assert::AreEqual(static_cast<uint16_t>(0), mediaType.blockAlign);
            Assert::AreEqual(
                static_cast<int>(AudioSampleFormat::Unknown),
                static_cast<int>(mediaType.sampleFormat));
            Assert::IsFalse(mediaType.IsValid());
        }

        TEST_METHOD(AudioMediaType_WithSampleRateChannelsAndFormat_IsValid)
        {
            AudioMediaType mediaType;
            mediaType.sampleRate = 48000;
            mediaType.channels = 2;
            mediaType.bitsPerSample = 32;
            mediaType.blockAlign = 8;
            mediaType.sampleFormat = AudioSampleFormat::Float32;

            Assert::IsTrue(mediaType.IsValid());
        }

        TEST_METHOD(ContainerFormats_RepresentInitialTargets)
        {
            Assert::AreEqual(0, static_cast<int>(ContainerFormat::Mp4));
            Assert::AreEqual(1, static_cast<int>(ContainerFormat::Mp3));
            Assert::AreEqual(2, static_cast<int>(ContainerFormat::Wav));
        }

        TEST_METHOD(Codecs_RepresentInitialTargets)
        {
            Assert::AreEqual(1, static_cast<int>(VideoCodec::H264));
            Assert::AreEqual(1, static_cast<int>(AudioCodec::Aac));
            Assert::AreEqual(2, static_cast<int>(AudioCodec::Mp3));
            Assert::AreEqual(3, static_cast<int>(AudioCodec::Pcm));
        }

        TEST_METHOD(VideoEncodingSettings_Default_IsDisabled)
        {
            const VideoEncodingSettings settings;

            Assert::AreEqual(
                static_cast<int>(VideoCodec::None),
                static_cast<int>(settings.codec));
            Assert::AreEqual(0u, settings.bitrate);
            Assert::IsFalse(settings.frameRate.IsValid());
            Assert::AreEqual(0u, settings.gopLength);
            Assert::IsTrue(settings.hardwareAccelerationPreferred);
            Assert::IsFalse(settings.IsEnabled());
        }

        TEST_METHOD(VideoEncodingSettings_WithCodec_IsEnabled)
        {
            VideoEncodingSettings settings;
            settings.codec = VideoCodec::H264;
            settings.bitrate = 8'000'000;
            settings.frameRate = Rational::From(60, 1);
            settings.gopLength = 120;

            Assert::IsTrue(settings.IsEnabled());
        }

        TEST_METHOD(AudioEncodingSettings_Default_IsDisabled)
        {
            const AudioEncodingSettings settings;

            Assert::AreEqual(
                static_cast<int>(AudioCodec::None),
                static_cast<int>(settings.codec));
            Assert::AreEqual(0u, settings.bitrate);
            Assert::AreEqual(0u, settings.sampleRate);
            Assert::AreEqual(static_cast<uint16_t>(0), settings.channels);
            Assert::IsFalse(settings.IsEnabled());
        }

        TEST_METHOD(AudioEncodingSettings_WithCodec_IsEnabled)
        {
            AudioEncodingSettings settings;
            settings.codec = AudioCodec::Aac;
            settings.bitrate = 192000;
            settings.sampleRate = 48000;
            settings.channels = 2;

            Assert::IsTrue(settings.IsEnabled());
        }

        TEST_METHOD(OutputSettings_Default_RequestsNoStreams)
        {
            const OutputSettings settings;

            Assert::AreEqual(
                static_cast<int>(ContainerFormat::Mp4),
                static_cast<int>(settings.container));
            Assert::IsTrue(settings.outputPath.empty());
            Assert::IsFalse(settings.HasRequestedStreams());
            Assert::IsFalse(settings.RequestsVideo());
            Assert::IsFalse(settings.RequestsAudio());
        }

        TEST_METHOD(OutputSettings_TracksRequestedStreams)
        {
            OutputSettings settings;
            settings.outputPath = L"C:\\Temp\\capture.mp4";
            settings.video = VideoEncodingSettings{ VideoCodec::H264, 8'000'000, Rational::From(60, 1), 120, true };
            settings.audio = AudioEncodingSettings{ AudioCodec::Aac, 192000, 48000, 2 };

            Assert::IsTrue(settings.HasRequestedStreams());
            Assert::IsTrue(settings.RequestsVideo());
            Assert::IsTrue(settings.RequestsAudio());
        }

        TEST_METHOD(ToneMappingSettings_Default_IsAutoPlaceholder)
        {
            const ToneMappingSettings settings;

            Assert::AreEqual(
                static_cast<int>(HdrPolicy::Auto),
                static_cast<int>(settings.policy));
            Assert::AreEqual(203.0f, settings.targetNits);
            Assert::IsTrue(settings.preserveMetadataWhenPossible);
        }

        TEST_METHOD(AudioGainSettings_Default_IsUnityAndInRange)
        {
            const AudioGainSettings settings;

            Assert::AreEqual(0.0f, settings.gainDb);
            Assert::AreEqual(-60.0f, settings.minGainDb);
            Assert::AreEqual(12.0f, settings.maxGainDb);
            Assert::IsTrue(settings.IsInSupportedRange());
        }

        TEST_METHOD(AudioGainSettings_RangeHelper_UsesSupportedBounds)
        {
            AudioGainSettings low;
            low.gainDb = AudioGainSettings::MinimumGainDb - 0.1f;

            AudioGainSettings high;
            high.gainDb = AudioGainSettings::MaximumGainDb + 0.1f;

            AudioGainSettings min;
            min.gainDb = AudioGainSettings::MinimumGainDb;

            AudioGainSettings max;
            max.gainDb = AudioGainSettings::MaximumGainDb;

            Assert::IsFalse(low.IsInSupportedRange());
            Assert::IsFalse(high.IsInSupportedRange());
            Assert::IsTrue(min.IsInSupportedRange());
            Assert::IsTrue(max.IsInSupportedRange());
        }
    };
}
