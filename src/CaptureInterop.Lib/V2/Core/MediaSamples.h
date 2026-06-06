#pragma once

#include "MediaTypes.h"

#include <cstdint>
#include <string>
#include <variant>
#include <vector>

namespace CaptureInterop::V2
{
    using MediaType = std::variant<VideoMediaType, AudioMediaType>;

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

    // CPU sample buffers are owned by the sample object. Callback receivers that need
    // to retain data beyond the callback must copy the sample or move it through a queue.
    struct VideoSample
    {
        SourceId sourceId;
        StreamId streamId;
        MediaTime timestamp;
        MediaDuration duration;
        VideoMediaType mediaType;
        std::vector<uint8_t> pixelData;
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
