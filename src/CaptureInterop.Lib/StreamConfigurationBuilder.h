#pragma once
#include "Result.h"
#include <wil/com.h>
#include <cstdint>
#include <mmreg.h>

// Forward declarations
struct IMFMediaType;

/// <summary>
/// Builder for creating Media Foundation media type configurations for video and audio streams.
/// Encapsulates the complex process of configuring H.264 video and AAC audio encoding parameters.
/// 
/// Implements RUST Principles:
/// - Principle #1 (Ownership): Returns unique_ptr to transfer ownership
/// - Principle #4 (Explicit Error Handling): Uses Result<T> for error handling
/// - Principle #7 (Const Correctness): All parameters are const
/// </summary>
class StreamConfigurationBuilder
{
public:
    /// <summary>
    /// Configuration for H.264 video encoding.
    /// </summary>
    struct VideoConfig
    {
        uint32_t width;
        uint32_t height;
        uint32_t frameRate;         // Frames per second (e.g., 30)
        uint32_t bitrate;           // Bits per second (e.g., 5000000 = 5 Mbps)
        
        static constexpr uint32_t DEFAULT_FRAME_RATE = 30;
        static constexpr uint32_t DEFAULT_VIDEO_BITRATE = 5000000;  // 5 Mbps
        
        static VideoConfig Default(uint32_t w, uint32_t h)
        {
            return VideoConfig{ w, h, DEFAULT_FRAME_RATE, DEFAULT_VIDEO_BITRATE };
        }
    };

    /// <summary>
    /// Configuration for AAC audio encoding.
    /// </summary>
    struct AudioConfig
    {
        uint32_t sampleRate;        // Samples per second (e.g., 48000)
        uint32_t channels;          // Number of channels (e.g., 2 for stereo)
        uint32_t bitsPerSample;     // Bits per sample (e.g., 16)
        uint32_t bitrate;           // Bits per second (e.g., 20000 = 160 kbps)
        bool isFloatFormat;         // True for IEEE float, false for PCM
        
        static constexpr uint32_t DEFAULT_AAC_BITRATE = 20000;  // 160 kbps
        
        static AudioConfig FromWaveFormat(const WAVEFORMATEX& format);
    };

    StreamConfigurationBuilder() = default;
    ~StreamConfigurationBuilder() = default;

    // Delete copy operations
    StreamConfigurationBuilder(const StreamConfigurationBuilder&) = delete;
    StreamConfigurationBuilder& operator=(const StreamConfigurationBuilder&) = delete;

    /// <summary>
    /// Create H.264 output media type for the video stream.
    /// </summary>
    Result<wil::com_ptr<IMFMediaType>> CreateVideoOutputType(const VideoConfig& config) const;

    /// <summary>
    /// Create RGB32 input media type for the video stream.
    /// </summary>
    Result<wil::com_ptr<IMFMediaType>> CreateVideoInputType(const VideoConfig& config) const;

    /// <summary>
    /// Create AAC output media type for the audio stream.
    /// </summary>
    Result<wil::com_ptr<IMFMediaType>> CreateAudioOutputType(const AudioConfig& config) const;

    /// <summary>
    /// Create PCM/Float input media type for the audio stream.
    /// </summary>
    Result<wil::com_ptr<IMFMediaType>> CreateAudioInputType(const AudioConfig& config) const;
};
