#pragma once
#include "IVideoEncoder.h"
#include "IAudioEncoder.h"

namespace CaptureInterop
{
    // Encoder preset factory
    class EncoderPresets
    {
    public:
        // Video presets
        static VideoEncoderConfig CreateVideoPreset(EncoderPreset preset, uint32_t width, uint32_t height, uint32_t fps);
        static VideoEncoderConfig CreateFastVideoPreset(uint32_t width, uint32_t height, uint32_t fps);
        static VideoEncoderConfig CreateBalancedVideoPreset(uint32_t width, uint32_t height, uint32_t fps);
        static VideoEncoderConfig CreateQualityVideoPreset(uint32_t width, uint32_t height, uint32_t fps);
        static VideoEncoderConfig CreateLosslessVideoPreset(uint32_t width, uint32_t height, uint32_t fps);
        
        // Audio presets
        static AudioEncoderConfig CreateAudioPreset(AudioQuality quality, uint32_t sampleRate = 48000, uint32_t channels = 2);
        static AudioEncoderConfig CreateLowAudioPreset(uint32_t sampleRate = 48000, uint32_t channels = 2);
        static AudioEncoderConfig CreateMediumAudioPreset(uint32_t sampleRate = 48000, uint32_t channels = 2);
        static AudioEncoderConfig CreateHighAudioPreset(uint32_t sampleRate = 48000, uint32_t channels = 2);
        static AudioEncoderConfig CreateVeryHighAudioPreset(uint32_t sampleRate = 48000, uint32_t channels = 2);
        
        // Bitrate calculation helpers
        static uint32_t CalculateVideoBitrate(EncoderPreset preset, uint32_t width, uint32_t height, uint32_t fps);
        static uint32_t CalculateAudioBitrate(AudioQuality quality, uint32_t channels);
        
    private:
        EncoderPresets() = delete;
    };
}
