#pragma once

#include "MediaPrimitives.h"

#include <optional>
#include <string>

namespace CaptureInterop::V2
{
    enum class VideoPixelFormat
    {
        Unknown = 0,
        Bgra8,
        Rgba16Float,
        Nv12,
        P010
    };

    enum class ColorPrimaries
    {
        Unknown = 0,
        Srgb,
        Rec709,
        Rec2020
    };

    enum class TransferFunction
    {
        Unknown = 0,
        Srgb,
        Gamma22,
        St2084Pq,
        Hlg
    };

    enum class ColorRange
    {
        Unknown = 0,
        Full,
        Limited
    };

    enum class AudioSampleFormat
    {
        Unknown = 0,
        Pcm16,
        Pcm24,
        Pcm32,
        Float32
    };

    enum class VideoCodec
    {
        None = 0,
        H264,
        Hevc,
        Av1
    };

    enum class AudioCodec
    {
        None = 0,
        Aac,
        Mp3,
        Pcm
    };

    enum class ContainerFormat
    {
        Mp4 = 0,
        Mp3,
        Wav
    };

    enum class HdrPolicy
    {
        Auto = 0,
        Preserve,
        MapToSdr,
        MatchDisplay,
        ForceSdr
    };

    enum class CursorCapturePolicy
    {
        Included = 0,
        Excluded
    };

    struct VideoMediaType
    {
        uint32_t width{ 0 };
        uint32_t height{ 0 };
        Rational frameRate;
        VideoPixelFormat pixelFormat{ VideoPixelFormat::Unknown };
        ColorPrimaries colorPrimaries{ ColorPrimaries::Unknown };
        TransferFunction transferFunction{ TransferFunction::Unknown };
        ColorRange range{ ColorRange::Unknown };

        [[nodiscard]] bool IsValid() const noexcept
        {
            return width != 0
                && height != 0
                && frameRate.IsValid()
                && pixelFormat != VideoPixelFormat::Unknown;
        }

        friend bool operator==(const VideoMediaType& left, const VideoMediaType& right) = default;
    };

    struct AudioMediaType
    {
        uint32_t sampleRate{ 0 };
        uint16_t channels{ 0 };
        uint16_t bitsPerSample{ 0 };
        uint16_t blockAlign{ 0 };
        AudioSampleFormat sampleFormat{ AudioSampleFormat::Unknown };

        [[nodiscard]] bool IsValid() const noexcept
        {
            return sampleRate != 0
                && channels != 0
                && bitsPerSample != 0
                && blockAlign != 0
                && sampleFormat != AudioSampleFormat::Unknown;
        }

        friend bool operator==(const AudioMediaType& left, const AudioMediaType& right) = default;
    };

    struct VideoEncodingSettings
    {
        VideoCodec codec{ VideoCodec::None };
        uint32_t bitrate{ 0 };
        Rational frameRate;
        uint32_t gopLength{ 0 };
        bool hardwareAccelerationPreferred{ true };

        [[nodiscard]] bool IsEnabled() const noexcept
        {
            return codec != VideoCodec::None;
        }

        friend bool operator==(const VideoEncodingSettings& left, const VideoEncodingSettings& right) = default;
    };

    struct AudioEncodingSettings
    {
        AudioCodec codec{ AudioCodec::None };
        uint32_t bitrate{ 0 };
        uint32_t sampleRate{ 0 };
        uint16_t channels{ 0 };

        [[nodiscard]] bool IsEnabled() const noexcept
        {
            return codec != AudioCodec::None;
        }

        friend bool operator==(const AudioEncodingSettings& left, const AudioEncodingSettings& right) = default;
    };

    struct OutputSettings
    {
        ContainerFormat container{ ContainerFormat::Mp4 };
        std::wstring outputPath;
        std::optional<VideoEncodingSettings> video;
        std::optional<AudioEncodingSettings> audio;

        [[nodiscard]] bool HasRequestedStreams() const noexcept
        {
            return video.has_value() || audio.has_value();
        }

        [[nodiscard]] bool RequestsVideo() const noexcept
        {
            return video.has_value() && video->IsEnabled();
        }

        [[nodiscard]] bool RequestsAudio() const noexcept
        {
            return audio.has_value() && audio->IsEnabled();
        }
    };

    struct ToneMappingSettings
    {
        HdrPolicy policy{ HdrPolicy::Auto };
        float targetNits{ 203.0f };
        bool preserveMetadataWhenPossible{ true };

        static ToneMappingSettings Auto() noexcept
        {
            return {};
        }
    };

    struct AudioGainSettings
    {
        static constexpr float MinimumGainDb = -60.0f;
        static constexpr float MaximumGainDb = 12.0f;
        static constexpr float DefaultGainDb = 0.0f;

        float gainDb{ DefaultGainDb };
        float minGainDb{ MinimumGainDb };
        float maxGainDb{ MaximumGainDb };

        [[nodiscard]] bool IsInSupportedRange() const noexcept
        {
            return gainDb >= minGainDb && gainDb <= maxGainDb;
        }
    };
}
