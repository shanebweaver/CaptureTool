#pragma once
#include "Result.h"
#include <wil/com.h>
#include <cstdint>
#include <mmreg.h>

// Forward declarations
struct IMFMediaType;

/// <summary>
/// Creates Media Foundation media types for H.264 video and AAC audio streams.
/// </summary>
class StreamConfigurationBuilder
{
public:
    struct VideoConfig
    {
        uint32_t width;
        uint32_t height;
        uint32_t frameRate;
        uint32_t bitrate;
        
        static constexpr uint32_t DEFAULT_FRAME_RATE = 30;
        static constexpr uint32_t DEFAULT_VIDEO_BITRATE = 5000000;  // 5 Mbps
        
        static VideoConfig Default(uint32_t w, uint32_t h)
        {
            return VideoConfig{ w, h, DEFAULT_FRAME_RATE, DEFAULT_VIDEO_BITRATE };
        }
    };

    struct AudioConfig
    {
        uint32_t sampleRate;
        uint32_t channels;
        uint32_t bitsPerSample;
        uint32_t bitrate;  // BYTES per second for MF_MT_AUDIO_AVG_BYTES_PER_SECOND
        bool isFloatFormat;
        
        static constexpr uint32_t DEFAULT_AAC_BITRATE = 20000;  // 20000 bytes/s = 160 kbps
        
        static AudioConfig FromWaveFormat(const WAVEFORMATEX& format);
    };

    StreamConfigurationBuilder() = default;
    ~StreamConfigurationBuilder() = default;

    StreamConfigurationBuilder(const StreamConfigurationBuilder&) = delete;
    StreamConfigurationBuilder& operator=(const StreamConfigurationBuilder&) = delete;

    Result<wil::com_ptr<IMFMediaType>> CreateVideoOutputType(const VideoConfig& config) const;
    Result<wil::com_ptr<IMFMediaType>> CreateVideoInputType(const VideoConfig& config) const;
    Result<wil::com_ptr<IMFMediaType>> CreateAudioOutputType(const AudioConfig& config) const;
    Result<wil::com_ptr<IMFMediaType>> CreateAudioInputType(const AudioConfig& config) const;

private:
    static constexpr uint32_t BYTES_PER_PIXEL_RGB32 = 4;
    static constexpr uint32_t AAC_OUTPUT_BITS_PER_SAMPLE = 16;
};
