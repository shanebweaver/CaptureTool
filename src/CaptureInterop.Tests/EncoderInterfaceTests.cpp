#include "pch.h"
#include "CppUnitTest.h"
#include "../CaptureInterop/EncoderPresets.h"
#include "../CaptureInterop/IVideoEncoder.h"
#include "../CaptureInterop/IAudioEncoder.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop;

namespace CaptureInteropTests
{
    // Sub-Task 1: Encoder Interface Tests
    // Tests the foundational encoder interfaces and configuration system

    TEST_CLASS(EncoderPresetsTests)
    {
    public:
        
        // 1.1 EncoderPresets Tests
        
        TEST_METHOD(PresetFactory_CreatesFastPreset)
        {
            // Arrange
            uint32_t width = 1920;
            uint32_t height = 1080;
            uint32_t fps = 30;

            // Act
            VideoEncoderConfig config = EncoderPresets::CreateFastVideoPreset(width, height, fps);

            // Assert
            Assert::AreEqual(static_cast<int>(VideoCodec::H264), static_cast<int>(config.codec));
            Assert::AreEqual(static_cast<int>(EncoderPreset::Fast), static_cast<int>(config.preset));
            Assert::AreEqual(width, config.width);
            Assert::AreEqual(height, config.height);
            Assert::AreEqual(fps, config.frameRateNum);
            Assert::IsTrue(config.hardwareAcceleration);
            
            // Fast preset should have lower bitrate
            uint32_t expectedBitrate = EncoderPresets::CalculateVideoBitrate(EncoderPreset::Fast, width, height, fps);
            Assert::AreEqual(expectedBitrate, config.bitrate);
        }

        TEST_METHOD(PresetFactory_CreatesBalancedPreset)
        {
            // Arrange
            uint32_t width = 1920;
            uint32_t height = 1080;
            uint32_t fps = 30;

            // Act
            VideoEncoderConfig config = EncoderPresets::CreateBalancedVideoPreset(width, height, fps);

            // Assert
            Assert::AreEqual(static_cast<int>(VideoCodec::H264), static_cast<int>(config.codec));
            Assert::AreEqual(static_cast<int>(EncoderPreset::Balanced), static_cast<int>(config.preset));
            Assert::AreEqual(width, config.width);
            Assert::AreEqual(height, config.height);
            Assert::AreEqual(fps, config.frameRateNum);
            
            // Balanced preset should have moderate bitrate
            uint32_t expectedBitrate = EncoderPresets::CalculateVideoBitrate(EncoderPreset::Balanced, width, height, fps);
            Assert::AreEqual(expectedBitrate, config.bitrate);
        }

        TEST_METHOD(PresetFactory_CreatesQualityPreset)
        {
            // Arrange
            uint32_t width = 1920;
            uint32_t height = 1080;
            uint32_t fps = 30;

            // Act
            VideoEncoderConfig config = EncoderPresets::CreateQualityVideoPreset(width, height, fps);

            // Assert
            Assert::AreEqual(static_cast<int>(VideoCodec::H264), static_cast<int>(config.codec));
            Assert::AreEqual(static_cast<int>(EncoderPreset::Quality), static_cast<int>(config.preset));
            Assert::AreEqual(width, config.width);
            Assert::AreEqual(height, config.height);
            Assert::AreEqual(fps, config.frameRateNum);
            
            // Quality preset should have higher bitrate
            uint32_t expectedBitrate = EncoderPresets::CalculateVideoBitrate(EncoderPreset::Quality, width, height, fps);
            Assert::AreEqual(expectedBitrate, config.bitrate);
        }

        TEST_METHOD(PresetFactory_CreatesLosslessPreset)
        {
            // Arrange
            uint32_t width = 1920;
            uint32_t height = 1080;
            uint32_t fps = 30;

            // Act
            VideoEncoderConfig config = EncoderPresets::CreateLosslessVideoPreset(width, height, fps);

            // Assert
            Assert::AreEqual(static_cast<int>(VideoCodec::H264), static_cast<int>(config.codec));
            Assert::AreEqual(static_cast<int>(EncoderPreset::Lossless), static_cast<int>(config.preset));
            Assert::AreEqual(width, config.width);
            Assert::AreEqual(height, config.height);
            Assert::AreEqual(fps, config.frameRateNum);
            
            // Lossless preset should have 0 bitrate (encoder decides)
            Assert::AreEqual(0u, config.bitrate);
        }

        TEST_METHOD(PresetFactory_InvalidPresetReturnsBalanced)
        {
            // Arrange
            uint32_t width = 1920;
            uint32_t height = 1080;
            uint32_t fps = 30;
            EncoderPreset invalidPreset = static_cast<EncoderPreset>(999); // Invalid enum value

            // Act
            VideoEncoderConfig config = EncoderPresets::CreateVideoPreset(invalidPreset, width, height, fps);

            // Assert - should default to Balanced
            Assert::AreEqual(static_cast<int>(EncoderPreset::Balanced), static_cast<int>(config.preset));
        }

        // 1.2 VideoEncoderConfig Tests

        TEST_METHOD(VideoEncoderConfig_DefaultValues)
        {
            // Act
            VideoEncoderConfig config = EncoderPresets::CreateBalancedVideoPreset(1920, 1080, 30);

            // Assert - verify sensible defaults
            Assert::AreEqual(1920u, config.width);
            Assert::AreEqual(1080u, config.height);
            Assert::AreEqual(30u, config.frameRateNum);
            Assert::AreEqual(1u, config.frameRateDen);
            Assert::AreEqual(static_cast<int>(EncoderPreset::Balanced), static_cast<int>(config.preset));
            Assert::AreEqual(static_cast<int>(VideoCodec::H264), static_cast<int>(config.codec));
        }

        TEST_METHOD(VideoEncoderConfig_BitrateCalculation_720p)
        {
            // Arrange
            uint32_t width = 1280;
            uint32_t height = 720;
            uint32_t fps = 30;

            // Act
            VideoEncoderConfig config = EncoderPresets::CreateBalancedVideoPreset(width, height, fps);

            // Assert
            uint32_t expectedBitrate = EncoderPresets::CalculateVideoBitrate(EncoderPreset::Balanced, width, height, fps);
            Assert::AreEqual(expectedBitrate, config.bitrate);
            Assert::IsTrue(config.bitrate > 0);
        }

        TEST_METHOD(VideoEncoderConfig_BitrateCalculation_1080p)
        {
            // Arrange
            uint32_t width = 1920;
            uint32_t height = 1080;
            uint32_t fps = 30;

            // Act
            VideoEncoderConfig config = EncoderPresets::CreateBalancedVideoPreset(width, height, fps);

            // Assert
            uint32_t expectedBitrate = EncoderPresets::CalculateVideoBitrate(EncoderPreset::Balanced, width, height, fps);
            Assert::AreEqual(expectedBitrate, config.bitrate);
            
            // 1080p should have higher bitrate than 720p
            uint32_t bitrate720p = EncoderPresets::CalculateVideoBitrate(EncoderPreset::Balanced, 1280, 720, fps);
            Assert::IsTrue(config.bitrate > bitrate720p);
        }

        TEST_METHOD(VideoEncoderConfig_BitrateCalculation_1440p)
        {
            // Arrange
            uint32_t width = 2560;
            uint32_t height = 1440;
            uint32_t fps = 30;

            // Act
            VideoEncoderConfig config = EncoderPresets::CreateBalancedVideoPreset(width, height, fps);

            // Assert
            uint32_t expectedBitrate = EncoderPresets::CalculateVideoBitrate(EncoderPreset::Balanced, width, height, fps);
            Assert::AreEqual(expectedBitrate, config.bitrate);
            
            // 1440p should have higher bitrate than 1080p
            uint32_t bitrate1080p = EncoderPresets::CalculateVideoBitrate(EncoderPreset::Balanced, 1920, 1080, fps);
            Assert::IsTrue(config.bitrate > bitrate1080p);
        }

        TEST_METHOD(VideoEncoderConfig_BitrateCalculation_4K)
        {
            // Arrange
            uint32_t width = 3840;
            uint32_t height = 2160;
            uint32_t fps = 30;

            // Act
            VideoEncoderConfig config = EncoderPresets::CreateBalancedVideoPreset(width, height, fps);

            // Assert
            uint32_t expectedBitrate = EncoderPresets::CalculateVideoBitrate(EncoderPreset::Balanced, width, height, fps);
            Assert::AreEqual(expectedBitrate, config.bitrate);
            
            // 4K should have much higher bitrate than 1080p
            uint32_t bitrate1080p = EncoderPresets::CalculateVideoBitrate(EncoderPreset::Balanced, 1920, 1080, fps);
            Assert::IsTrue(config.bitrate > bitrate1080p * 2); // At least 2x more
        }

        TEST_METHOD(VideoEncoderConfig_CodecSupport)
        {
            // Act
            VideoEncoderConfig config = EncoderPresets::CreateBalancedVideoPreset(1920, 1080, 30);

            // Assert - H.264 should be set correctly
            Assert::AreEqual(static_cast<int>(VideoCodec::H264), static_cast<int>(config.codec));
        }

        // 1.3 AudioEncoderConfig Tests

        TEST_METHOD(AudioEncoderConfig_DefaultValues)
        {
            // Act
            AudioEncoderConfig config = EncoderPresets::CreateHighAudioPreset(48000, 2);

            // Assert - verify defaults
            Assert::AreEqual(48000u, config.sampleRate);
            Assert::AreEqual(2u, config.channels);
            Assert::AreEqual(16u, config.bitsPerSample);
            Assert::AreEqual(static_cast<int>(AudioQuality::High), static_cast<int>(config.quality));
            Assert::AreEqual(static_cast<int>(AudioCodec::AAC), static_cast<int>(config.codec));
        }

        TEST_METHOD(AudioEncoderConfig_BitrateCalculation_Low)
        {
            // Arrange
            uint32_t channels = 2;

            // Act
            AudioEncoderConfig config = EncoderPresets::CreateLowAudioPreset(48000, channels);

            // Assert - Low quality: 48 kbps per channel
            uint32_t expectedBitrate = 48000 * channels;
            Assert::AreEqual(expectedBitrate, config.bitrate);
        }

        TEST_METHOD(AudioEncoderConfig_BitrateCalculation_Medium)
        {
            // Arrange
            uint32_t channels = 2;

            // Act
            AudioEncoderConfig config = EncoderPresets::CreateMediumAudioPreset(48000, channels);

            // Assert - Medium quality: 64 kbps per channel
            uint32_t expectedBitrate = 64000 * channels;
            Assert::AreEqual(expectedBitrate, config.bitrate);
        }

        TEST_METHOD(AudioEncoderConfig_BitrateCalculation_High)
        {
            // Arrange
            uint32_t channels = 2;

            // Act
            AudioEncoderConfig config = EncoderPresets::CreateHighAudioPreset(48000, channels);

            // Assert - High quality: 96 kbps per channel
            uint32_t expectedBitrate = 96000 * channels;
            Assert::AreEqual(expectedBitrate, config.bitrate);
        }

        TEST_METHOD(AudioEncoderConfig_BitrateCalculation_VeryHigh)
        {
            // Arrange
            uint32_t channels = 2;

            // Act
            AudioEncoderConfig config = EncoderPresets::CreateVeryHighAudioPreset(48000, channels);

            // Assert - VeryHigh quality: 128 kbps per channel
            uint32_t expectedBitrate = 128000 * channels;
            Assert::AreEqual(expectedBitrate, config.bitrate);
        }

        TEST_METHOD(AudioEncoderConfig_MultiChannelSupport_Mono)
        {
            // Act
            AudioEncoderConfig config = EncoderPresets::CreateHighAudioPreset(48000, 1);

            // Assert
            Assert::AreEqual(1u, config.channels);
            uint32_t expectedBitrate = 96000 * 1; // High quality = 96 kbps per channel
            Assert::AreEqual(expectedBitrate, config.bitrate);
        }

        TEST_METHOD(AudioEncoderConfig_MultiChannelSupport_Stereo)
        {
            // Act
            AudioEncoderConfig config = EncoderPresets::CreateHighAudioPreset(48000, 2);

            // Assert
            Assert::AreEqual(2u, config.channels);
            uint32_t expectedBitrate = 96000 * 2; // High quality = 96 kbps per channel
            Assert::AreEqual(expectedBitrate, config.bitrate);
        }

        TEST_METHOD(AudioEncoderConfig_MultiChannelSupport_5Point1)
        {
            // Act
            AudioEncoderConfig config = EncoderPresets::CreateHighAudioPreset(48000, 6);

            // Assert
            Assert::AreEqual(6u, config.channels);
            uint32_t expectedBitrate = 96000 * 6; // High quality = 96 kbps per channel
            Assert::AreEqual(expectedBitrate, config.bitrate);
        }

        TEST_METHOD(AudioEncoderConfig_MultiChannelSupport_7Point1)
        {
            // Act
            AudioEncoderConfig config = EncoderPresets::CreateHighAudioPreset(48000, 8);

            // Assert
            Assert::AreEqual(8u, config.channels);
            uint32_t expectedBitrate = 96000 * 8; // High quality = 96 kbps per channel
            Assert::AreEqual(expectedBitrate, config.bitrate);
        }
    };
}
