#pragma once

#include "V2/Core/CapturePipelineConfig.h"
#include "V2/Core/MediaSamples.h"
#include "V2/Core/PipelineInterfaces.h"

#include <optional>
#include <string>

namespace CaptureInterop::V2::Audio
{
    enum class AudioDeviceSelection
    {
        DefaultRenderEndpoint = 0,
        EndpointId
    };

    struct WasapiLoopbackAudioSourceConfig
    {
        SourceId sourceId;
        StreamId audioStreamId;
        std::string name;
        AudioDeviceSelection deviceSelection{ AudioDeviceSelection::DefaultRenderEndpoint };
        std::string endpointId;
        bool armed{ false };
        AudioSourceControlConfig controls;
        AudioMediaType mediaType;

        [[nodiscard]] ::CaptureInterop::V2::SourceDescriptor SourceDescriptor() const
        {
            ::CaptureInterop::V2::SourceDescriptor descriptor;
            descriptor.id = sourceId;
            descriptor.kind = SourceKind::SystemAudio;
            descriptor.name = name.empty() ? "System audio" : name;
            return descriptor;
        }
    };

    [[nodiscard]] inline StreamDescriptor BuildWasapiLoopbackAudioStream(
        const WasapiLoopbackAudioSourceConfig& config)
    {
        return StreamDescriptor{
            config.audioStreamId,
            config.sourceId,
            MediaKind::Audio,
            config.name.empty() ? "System audio" : config.name + " stream"
        };
    }

    [[nodiscard]] inline WasapiLoopbackAudioSourceConfig MapWasapiLoopbackAudioSourceConfig(
        const SystemAudioSourceConfig& source,
        AudioMediaType mediaType,
        std::optional<StreamId> audioStreamId = std::nullopt)
    {
        WasapiLoopbackAudioSourceConfig config;
        config.sourceId = source.id;
        config.audioStreamId = audioStreamId.value_or(StreamId::FromValue(source.id.value));
        config.name = source.name.empty() ? "System audio" : source.name;
        config.endpointId = source.deviceId;
        config.deviceSelection = source.useDefaultDevice
            ? AudioDeviceSelection::DefaultRenderEndpoint
            : AudioDeviceSelection::EndpointId;
        config.armed = source.armed;
        config.controls = source.controls;
        config.mediaType = mediaType;
        return config;
    }
}
