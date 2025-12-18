#pragma once
#include "IMediaSource.h"
#include <cstdint>

// Forward declarations
struct ID3D11Texture2D;
struct IMFSample;

namespace CaptureInterop
{
    // Video codec types
    enum class VideoCodec
    {
        H264,           // H.264/AVC (widely supported)
        H265,           // H.265/HEVC (future)
        VP9,            // VP9 (future)
        AV1             // AV1 (future)
    };

    // Encoder preset levels
    enum class EncoderPreset
    {
        Fast,           // Low CPU usage, acceptable quality (streaming)
        Balanced,       // Default - good balance (recording)
        Quality,        // High quality, higher CPU (archival)
        Lossless        // Maximum quality (lossless/near-lossless)
    };

    // Video encoder configuration
    struct VideoEncoderConfig
    {
        VideoCodec codec;           // Codec to use
        EncoderPreset preset;       // Quality preset
        uint32_t width;             // Frame width
        uint32_t height;            // Frame height
        uint32_t frameRateNum;      // Frame rate numerator (e.g., 30)
        uint32_t frameRateDen;      // Frame rate denominator (e.g., 1)
        uint32_t bitrate;           // Target bitrate in bits/sec (0 = auto)
        bool hardwareAcceleration;  // Use hardware encoder if available
        
        VideoEncoderConfig()
            : codec(VideoCodec::H264)
            , preset(EncoderPreset::Balanced)
            , width(1920)
            , height(1080)
            , frameRateNum(30)
            , frameRateDen(1)
            , bitrate(0)
            , hardwareAcceleration(true)
        {}
    };

    // Video encoder capabilities
    struct VideoEncoderCapabilities
    {
        bool supportsH264;
        bool supportsH265;
        bool supportsVP9;
        bool supportsAV1;
        bool supportsHardwareAcceleration;
        bool supportsLossless;
        uint32_t maxWidth;
        uint32_t maxHeight;
        
        VideoEncoderCapabilities()
            : supportsH264(false)
            , supportsH265(false)
            , supportsVP9(false)
            , supportsAV1(false)
            , supportsHardwareAcceleration(false)
            , supportsLossless(false)
            , maxWidth(0)
            , maxHeight(0)
        {}
    };

    // Video encoder interface
    class IVideoEncoder : public IMediaSource
    {
    public:
        // Configuration
        virtual HRESULT Configure(const VideoEncoderConfig& config) = 0;
        virtual HRESULT GetConfiguration(VideoEncoderConfig* pConfig) const = 0;
        
        // Capabilities
        virtual HRESULT GetCapabilities(VideoEncoderCapabilities* pCapabilities) const = 0;
        virtual bool SupportsCodec(VideoCodec codec) const = 0;
        
        // Encoding
        virtual HRESULT EncodeFrame(ID3D11Texture2D* pTexture, int64_t timestamp, IMFSample** ppSample) = 0;
        virtual HRESULT Flush(IMFSample** ppSample) = 0;
        
        // Stats
        virtual uint64_t GetEncodedFrameCount() const = 0;
        virtual uint64_t GetDroppedFrameCount() const = 0;
        virtual double GetAverageEncodingTimeMs() const = 0;
        
    protected:
        virtual ~IVideoEncoder() = default;
    };
}
