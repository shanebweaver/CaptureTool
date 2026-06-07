#pragma once

#include "V2/Core/CapturePipelineConfig.h"
#include "V2/Core/MediaSamples.h"

#include <optional>
#include <string>
#include <utility>
#include <vector>

namespace CaptureInterop::V2::Desktop
{
    struct DesktopMonitorTarget
    {
        uintptr_t monitorHandle{ 0 };
        std::string displayId;
        std::string deviceName;

        [[nodiscard]] bool HasIdentity() const noexcept
        {
            return monitorHandle != 0 || !displayId.empty() || !deviceName.empty();
        }
    };

    struct DesktopVideoSourceConfig
    {
        SourceId sourceId;
        StreamId streamId;
        std::string sourceName;
        std::string streamName;
        DesktopMonitorTarget monitor;
        std::optional<CaptureRectangle> region;
        CursorCapturePolicy cursorPolicy{ CursorCapturePolicy::Included };
        Rational requestedFrameRate;

        [[nodiscard]] bool CapturesRegion() const noexcept
        {
            return region.has_value();
        }

        [[nodiscard]] SourceDescriptor SourceDescriptor() const
        {
            return CaptureInterop::V2::SourceDescriptor{
                sourceId,
                SourceKind::Desktop,
                sourceName
            };
        }

        [[nodiscard]] StreamDescriptor StreamDescriptor() const
        {
            return CaptureInterop::V2::StreamDescriptor{
                streamId,
                sourceId,
                MediaKind::Video,
                streamName
            };
        }
    };

    [[nodiscard]] inline StreamId ResolveDesktopVideoStreamId(SourceId sourceId, StreamId configuredStreamId) noexcept
    {
        if (configuredStreamId.IsValid())
        {
            return configuredStreamId;
        }

        return sourceId.IsValid() ? StreamId::FromValue(sourceId.value) : StreamId::Invalid();
    }

    [[nodiscard]] inline std::string ResolveDesktopSourceName(const DesktopSourceConfig& config)
    {
        return config.name.empty() ? "Desktop source" : config.name;
    }

    [[nodiscard]] inline std::string ResolveDesktopStreamName(const DesktopSourceConfig& config)
    {
        return config.name.empty() ? "Desktop video stream" : config.name + " video";
    }

    [[nodiscard]] inline DesktopVideoSourceConfig MapDesktopVideoSourceConfig(DesktopSourceConfig config)
    {
        DesktopVideoSourceConfig mapped;
        mapped.sourceId = config.id;
        mapped.streamId = ResolveDesktopVideoStreamId(config.id, config.videoStreamId);
        mapped.sourceName = ResolveDesktopSourceName(config);
        mapped.streamName = ResolveDesktopStreamName(config);
        mapped.monitor = DesktopMonitorTarget{
            config.monitorHandle,
            std::move(config.displayId),
            std::move(config.monitorDeviceName)
        };
        mapped.region = std::move(config.captureArea);
        mapped.cursorPolicy = config.cursorPolicy;
        mapped.requestedFrameRate = config.frameRate;
        return mapped;
    }

    [[nodiscard]] inline std::vector<StreamDescriptor> BuildDesktopVideoStreams(
        const DesktopVideoSourceConfig& config)
    {
        return { config.StreamDescriptor() };
    }
}
