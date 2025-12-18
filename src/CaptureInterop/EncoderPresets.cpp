#include "pch.h"
#include "EncoderPresets.h"

namespace CaptureInterop
{
    // Video preset factory
    VideoEncoderConfig EncoderPresets::CreateVideoPreset(EncoderPreset preset, uint32_t width, uint32_t height, uint32_t fps)
    {
        switch (preset)
        {
        case EncoderPreset::Fast:
            return CreateFastVideoPreset(width, height, fps);
        case EncoderPreset::Balanced:
            return CreateBalancedVideoPreset(width, height, fps);
        case EncoderPreset::Quality:
            return CreateQualityVideoPreset(width, height, fps);
        case EncoderPreset::Lossless:
            return CreateLosslessVideoPreset(width, height, fps);
        default:
            return CreateBalancedVideoPreset(width, height, fps);
        }
    }

    VideoEncoderConfig EncoderPresets::CreateFastVideoPreset(uint32_t width, uint32_t height, uint32_t fps)
    {
        VideoEncoderConfig config;
        config.codec = VideoCodec::H264;
        config.preset = EncoderPreset::Fast;
        config.width = width;
        config.height = height;
        config.frameRateNum = fps;
        config.frameRateDen = 1;
        config.bitrate = CalculateVideoBitrate(EncoderPreset::Fast, width, height, fps);
        config.hardwareAcceleration = true;
        return config;
    }

    VideoEncoderConfig EncoderPresets::CreateBalancedVideoPreset(uint32_t width, uint32_t height, uint32_t fps)
    {
        VideoEncoderConfig config;
        config.codec = VideoCodec::H264;
        config.preset = EncoderPreset::Balanced;
        config.width = width;
        config.height = height;
        config.frameRateNum = fps;
        config.frameRateDen = 1;
        config.bitrate = CalculateVideoBitrate(EncoderPreset::Balanced, width, height, fps);
        config.hardwareAcceleration = true;
        return config;
    }

    VideoEncoderConfig EncoderPresets::CreateQualityVideoPreset(uint32_t width, uint32_t height, uint32_t fps)
    {
        VideoEncoderConfig config;
        config.codec = VideoCodec::H264;
        config.preset = EncoderPreset::Quality;
        config.width = width;
        config.height = height;
        config.frameRateNum = fps;
        config.frameRateDen = 1;
        config.bitrate = CalculateVideoBitrate(EncoderPreset::Quality, width, height, fps);
        config.hardwareAcceleration = true;
        return config;
    }

    VideoEncoderConfig EncoderPresets::CreateLosslessVideoPreset(uint32_t width, uint32_t height, uint32_t fps)
    {
        VideoEncoderConfig config;
        config.codec = VideoCodec::H264;
        config.preset = EncoderPreset::Lossless;
        config.width = width;
        config.height = height;
        config.frameRateNum = fps;
        config.frameRateDen = 1;
        config.bitrate = 0; // Use lossless mode (QP=0 or similar)
        config.hardwareAcceleration = true;
        return config;
    }

    // Audio preset factory
    AudioEncoderConfig EncoderPresets::CreateAudioPreset(AudioQuality quality, uint32_t sampleRate, uint32_t channels)
    {
        switch (quality)
        {
        case AudioQuality::Low:
            return CreateLowAudioPreset(sampleRate, channels);
        case AudioQuality::Medium:
            return CreateMediumAudioPreset(sampleRate, channels);
        case AudioQuality::High:
            return CreateHighAudioPreset(sampleRate, channels);
        case AudioQuality::VeryHigh:
            return CreateVeryHighAudioPreset(sampleRate, channels);
        default:
            return CreateMediumAudioPreset(sampleRate, channels);
        }
    }

    AudioEncoderConfig EncoderPresets::CreateLowAudioPreset(uint32_t sampleRate, uint32_t channels)
    {
        AudioEncoderConfig config;
        config.codec = AudioCodec::AAC;
        config.quality = AudioQuality::Low;
        config.sampleRate = sampleRate;
        config.channels = channels;
        config.bitsPerSample = 16;
        config.bitrate = CalculateAudioBitrate(AudioQuality::Low, channels);
        return config;
    }

    AudioEncoderConfig EncoderPresets::CreateMediumAudioPreset(uint32_t sampleRate, uint32_t channels)
    {
        AudioEncoderConfig config;
        config.codec = AudioCodec::AAC;
        config.quality = AudioQuality::Medium;
        config.sampleRate = sampleRate;
        config.channels = channels;
        config.bitsPerSample = 16;
        config.bitrate = CalculateAudioBitrate(AudioQuality::Medium, channels);
        return config;
    }

    AudioEncoderConfig EncoderPresets::CreateHighAudioPreset(uint32_t sampleRate, uint32_t channels)
    {
        AudioEncoderConfig config;
        config.codec = AudioCodec::AAC;
        config.quality = AudioQuality::High;
        config.sampleRate = sampleRate;
        config.channels = channels;
        config.bitsPerSample = 16;
        config.bitrate = CalculateAudioBitrate(AudioQuality::High, channels);
        return config;
    }

    AudioEncoderConfig EncoderPresets::CreateVeryHighAudioPreset(uint32_t sampleRate, uint32_t channels)
    {
        AudioEncoderConfig config;
        config.codec = AudioCodec::AAC;
        config.quality = AudioQuality::VeryHigh;
        config.sampleRate = sampleRate;
        config.channels = channels;
        config.bitsPerSample = 16;
        config.bitrate = CalculateAudioBitrate(AudioQuality::VeryHigh, channels);
        return config;
    }

    // Bitrate calculation helpers
    uint32_t EncoderPresets::CalculateVideoBitrate(EncoderPreset preset, uint32_t width, uint32_t height, uint32_t fps)
    {
        // Calculate pixels per second
        uint64_t pixelsPerSecond = static_cast<uint64_t>(width) * height * fps;
        
        // Bits per pixel based on preset
        double bitsPerPixel = 0.0;
        switch (preset)
        {
        case EncoderPreset::Fast:
            bitsPerPixel = 0.05;  // Low quality, fast encoding
            break;
        case EncoderPreset::Balanced:
            bitsPerPixel = 0.1;   // Good balance
            break;
        case EncoderPreset::Quality:
            bitsPerPixel = 0.2;   // High quality
            break;
        case EncoderPreset::Lossless:
            return 0;              // Let encoder decide (lossless mode)
        default:
            bitsPerPixel = 0.1;
            break;
        }
        
        // Calculate bitrate
        return static_cast<uint32_t>(pixelsPerSecond * bitsPerPixel);
    }

    uint32_t EncoderPresets::CalculateAudioBitrate(AudioQuality quality, uint32_t channels)
    {
        // Base bitrate per channel
        uint32_t bitratePerChannel = 0;
        switch (quality)
        {
        case AudioQuality::Low:
            bitratePerChannel = 48000;   // 48 kbps per channel
            break;
        case AudioQuality::Medium:
            bitratePerChannel = 64000;   // 64 kbps per channel
            break;
        case AudioQuality::High:
            bitratePerChannel = 96000;   // 96 kbps per channel
            break;
        case AudioQuality::VeryHigh:
            bitratePerChannel = 128000;  // 128 kbps per channel
            break;
        default:
            bitratePerChannel = 64000;
            break;
        }
        
        return bitratePerChannel * channels;
    }
}
