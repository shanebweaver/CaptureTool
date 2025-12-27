#pragma once
#include "Result.h"
#include <wil/com.h>
#include <cstdint>
#include <mmreg.h>

// Forward declarations
struct IMFMediaType;

/// <summary>
/// Interface for creating Media Foundation media types for video and audio streams.
/// Provides abstraction for stream configuration to enable dependency injection and testing.
/// </summary>
class IStreamConfigurationBuilder
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

    virtual ~IStreamConfigurationBuilder() = default;

    /// <summary>
    /// Create a Media Foundation output media type for H.264 video encoding.
    /// </summary>
    virtual Result<wil::com_ptr<IMFMediaType>> CreateVideoOutputType(const VideoConfig& config) const = 0;

    /// <summary>
    /// Create a Media Foundation input media type for RGB32 video.
    /// </summary>
    virtual Result<wil::com_ptr<IMFMediaType>> CreateVideoInputType(const VideoConfig& config) const = 0;

    /// <summary>
    /// Create a Media Foundation output media type for AAC audio encoding.
    /// </summary>
    virtual Result<wil::com_ptr<IMFMediaType>> CreateAudioOutputType(const AudioConfig& config) const = 0;

    /// <summary>
    /// Create a Media Foundation input media type for PCM or float audio.
    /// </summary>
    virtual Result<wil::com_ptr<IMFMediaType>> CreateAudioInputType(const AudioConfig& config) const = 0;
};
