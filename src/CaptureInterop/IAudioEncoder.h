#pragma once
#include "IMediaSource.h"
#include <cstdint>

// Forward declarations
struct IMFSample;

namespace CaptureInterop
{
    // Audio codec types
    enum class AudioCodec
    {
        AAC,            // AAC-LC (widely supported)
        FLAC,           // FLAC lossless (future)
        Opus,           // Opus (future)
        PCM             // Uncompressed PCM (future)
    };

    // Audio quality presets
    enum class AudioQuality
    {
        Low,            // ~96 kbps (voice)
        Medium,         // ~128 kbps (default)
        High,           // ~192 kbps (music)
        VeryHigh        // ~256 kbps (archival)
    };

    // Audio encoder configuration
    struct AudioEncoderConfig
    {
        AudioCodec codec;           // Codec to use
        AudioQuality quality;       // Quality preset
        uint32_t sampleRate;        // Sample rate (Hz)
        uint32_t channels;          // Number of channels
        uint32_t bitsPerSample;     // Bits per sample (16, 24, 32)
        uint32_t bitrate;           // Target bitrate in bits/sec (0 = auto)
        
        AudioEncoderConfig()
            : codec(AudioCodec::AAC)
            , quality(AudioQuality::Medium)
            , sampleRate(48000)
            , channels(2)
            , bitsPerSample(16)
            , bitrate(0)
        {}
    };

    // Audio encoder capabilities
    struct AudioEncoderCapabilities
    {
        bool supportsAAC;
        bool supportsFLAC;
        bool supportsOpus;
        bool supportsPCM;
        uint32_t maxSampleRate;
        uint32_t maxChannels;
        
        AudioEncoderCapabilities()
            : supportsAAC(false)
            , supportsFLAC(false)
            , supportsOpus(false)
            , supportsPCM(false)
            , maxSampleRate(0)
            , maxChannels(0)
        {}
    };

    // Audio encoder interface
    class IAudioEncoder : public IMediaSource
    {
    public:
        // Configuration
        virtual HRESULT Configure(const AudioEncoderConfig& config) = 0;
        virtual HRESULT GetConfiguration(AudioEncoderConfig* pConfig) const = 0;
        
        // Capabilities
        virtual HRESULT GetCapabilities(AudioEncoderCapabilities* pCapabilities) const = 0;
        virtual bool SupportsCodec(AudioCodec codec) const = 0;
        
        // Encoding
        virtual HRESULT EncodeAudio(const uint8_t* pData, uint32_t dataSize, int64_t timestamp, IMFSample** ppSample) = 0;
        virtual HRESULT Flush(IMFSample** ppSample) = 0;
        
        // Stats
        virtual uint64_t GetEncodedSampleCount() const = 0;
        virtual double GetAverageEncodingTimeMs() const = 0;
        
    protected:
        virtual ~IAudioEncoder() = default;
    };
}
