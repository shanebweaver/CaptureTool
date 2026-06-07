#pragma once

#include "MediaTypes.h"

#include <cstdint>
#include <d3d11.h>
#include <memory>
#include <string>
#include <utility>
#include <variant>
#include <vector>
#include <wil/com.h>

namespace CaptureInterop::V2
{
    using MediaType = std::variant<VideoMediaType, AudioMediaType>;

    enum class AudioTimestampSource
    {
        Unknown = 0,
        WasapiPacketPosition,
        WasapiQpcPosition,
        GeneratedContinuity,
        ArrivalTime
    };

    struct AudioSourceTimingMetadata
    {
        AudioTimestampSource timestampSource{ AudioTimestampSource::Unknown };
        uint64_t packetPosition{ 0 };
        uint64_t qpcPosition{ 0 };
        bool discontinuity{ false };
        bool silent{ false };
        bool synthesizedSilence{ false };
    };

    struct SourceDescriptor
    {
        SourceId id;
        SourceKind kind{ SourceKind::Unknown };
        std::string name;
    };

    struct StreamDescriptor
    {
        StreamId id;
        SourceId sourceId;
        MediaKind kind{ MediaKind::Unknown };
        std::string name;
    };

    struct VideoFrameDimensions
    {
        uint32_t width{ 0 };
        uint32_t height{ 0 };

        [[nodiscard]] bool IsValid() const noexcept
        {
            return width > 0 && height > 0;
        }

        [[nodiscard]] static VideoFrameDimensions FromMediaType(const VideoMediaType& mediaType) noexcept
        {
            return VideoFrameDimensions{ mediaType.width, mediaType.height };
        }
    };

    class IVideoTextureReference
    {
    public:
        virtual ~IVideoTextureReference() = default;

        [[nodiscard]] virtual ID3D11Texture2D* Texture() const noexcept = 0;
    };

    class D3D11VideoTextureReference final : public IVideoTextureReference
    {
    public:
        explicit D3D11VideoTextureReference(wil::com_ptr<ID3D11Texture2D> texture)
            : m_texture(std::move(texture))
        {
        }

        [[nodiscard]] ID3D11Texture2D* Texture() const noexcept override
        {
            return m_texture.get();
        }

    private:
        wil::com_ptr<ID3D11Texture2D> m_texture;
    };

    // CPU buffers and optional D3D texture references are owned by the sample object.
    // Callback receivers that need to retain data beyond the callback must copy the
    // sample or move it through a queue.
    struct VideoSample
    {
        SourceId sourceId;
        StreamId streamId;
        MediaTime timestamp;
        MediaDuration duration;
        VideoMediaType mediaType;
        std::vector<uint8_t> pixelData;
        uint64_t sequenceNumber{ 0 };
        VideoFrameDimensions frameDimensions;
        std::shared_ptr<IVideoTextureReference> texture;

        [[nodiscard]] bool HasTexture() const noexcept
        {
            return texture != nullptr;
        }

        [[nodiscard]] VideoFrameDimensions Dimensions() const noexcept
        {
            return frameDimensions.IsValid() ? frameDimensions : VideoFrameDimensions::FromMediaType(mediaType);
        }
    };

    // Audio sample buffers are owned by the sample object. Source implementations must
    // copy borrowed API buffers before publishing samples through these core contracts.
    struct AudioSample
    {
        SourceId sourceId;
        StreamId streamId;
        MediaTime timestamp;
        MediaDuration duration;
        AudioMediaType mediaType;
        std::vector<uint8_t> pcmData;
        uint32_t frameCount{ 0 };
        AudioSourceTimingMetadata sourceTiming;
    };

    using MediaSampleData = std::variant<VideoSample, AudioSample>;

    struct MediaSample
    {
        MediaSampleData data;

        [[nodiscard]] MediaKind Kind() const noexcept
        {
            return std::holds_alternative<VideoSample>(data) ? MediaKind::Video : MediaKind::Audio;
        }

        [[nodiscard]] SourceId Source() const noexcept
        {
            if (const auto* video = std::get_if<VideoSample>(&data))
            {
                return video->sourceId;
            }

            return std::get<AudioSample>(data).sourceId;
        }

        [[nodiscard]] StreamId Stream() const noexcept
        {
            if (const auto* video = std::get_if<VideoSample>(&data))
            {
                return video->streamId;
            }

            return std::get<AudioSample>(data).streamId;
        }

        [[nodiscard]] MediaTime Timestamp() const noexcept
        {
            if (const auto* video = std::get_if<VideoSample>(&data))
            {
                return video->timestamp;
            }

            return std::get<AudioSample>(data).timestamp;
        }

        [[nodiscard]] MediaDuration Duration() const noexcept
        {
            if (const auto* video = std::get_if<VideoSample>(&data))
            {
                return video->duration;
            }

            return std::get<AudioSample>(data).duration;
        }
    };
}
