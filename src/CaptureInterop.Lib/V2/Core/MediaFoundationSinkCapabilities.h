#pragma once

#include "OutputProfileResolver.h"

#include <algorithm>
#include <utility>
#include <vector>

namespace CaptureInterop::V2
{
    enum class OutputStreamRequirement
    {
        Disallowed = 0,
        Optional,
        Required
    };

    struct MediaFoundationContainerCapability
    {
        ContainerFormat container{ ContainerFormat::Mp4 };
        std::vector<VideoCodec> videoCodecs;
        std::vector<AudioCodec> audioCodecs;
        OutputStreamRequirement videoRequirement{ OutputStreamRequirement::Optional };
        OutputStreamRequirement audioRequirement{ OutputStreamRequirement::Optional };
        uint32_t maxVideoStreams{ 0 };
        uint32_t maxAudioStreams{ 0 };

        [[nodiscard]] bool SupportsVideoCodec(VideoCodec codec) const noexcept
        {
            return std::find(videoCodecs.begin(), videoCodecs.end(), codec) != videoCodecs.end();
        }

        [[nodiscard]] bool SupportsAudioCodec(AudioCodec codec) const noexcept
        {
            return std::find(audioCodecs.begin(), audioCodecs.end(), codec) != audioCodecs.end();
        }
    };

    struct MediaFoundationSinkCapabilities
    {
        std::vector<MediaFoundationContainerCapability> containers;

        [[nodiscard]] const MediaFoundationContainerCapability* Find(ContainerFormat container) const noexcept
        {
            for (const MediaFoundationContainerCapability& capability : containers)
            {
                if (capability.container == container)
                {
                    return &capability;
                }
            }

            return nullptr;
        }

        [[nodiscard]] static MediaFoundationSinkCapabilities Default()
        {
            MediaFoundationSinkCapabilities capabilities;
            capabilities.containers.push_back(MediaFoundationContainerCapability{
                ContainerFormat::Mp4,
                { VideoCodec::H264 },
                { AudioCodec::Aac },
                OutputStreamRequirement::Optional,
                OutputStreamRequirement::Optional,
                1,
                1
            });
            capabilities.containers.push_back(MediaFoundationContainerCapability{
                ContainerFormat::Mp3,
                {},
                { AudioCodec::Mp3 },
                OutputStreamRequirement::Disallowed,
                OutputStreamRequirement::Required,
                0,
                1
            });
            return capabilities;
        }
    };

    struct MediaFoundationProfileValidationResult
    {
        ValidationResult diagnostics;

        [[nodiscard]] bool IsSuccess() const noexcept
        {
            return diagnostics.IsValid();
        }
    };

    class MediaFoundationSinkProfileValidator
    {
    public:
        explicit MediaFoundationSinkProfileValidator(
            MediaFoundationSinkCapabilities capabilities = MediaFoundationSinkCapabilities::Default())
            : m_capabilities(std::move(capabilities))
        {
        }

        [[nodiscard]] const MediaFoundationSinkCapabilities& Capabilities() const noexcept
        {
            return m_capabilities;
        }

        [[nodiscard]] MediaFoundationProfileValidationResult Validate(const OutputPlan& plan) const
        {
            MediaFoundationProfileValidationResult result;
            const MediaFoundationContainerCapability* capability = m_capabilities.Find(plan.container);
            if (capability == nullptr)
            {
                result.diagnostics.AddError(
                    CoreResultCode::UnsupportedOperation,
                    Component,
                    Operation,
                    "Media Foundation output container is not supported");
                return result;
            }

            uint32_t videoStreams = 0;
            uint32_t audioStreams = 0;
            std::vector<uint32_t> streamIds;
            for (const OutputStreamPlan& stream : plan.streams)
            {
                if (std::find(streamIds.begin(), streamIds.end(), stream.streamId.value) != streamIds.end())
                {
                    result.diagnostics.AddError(
                        CoreResultCode::ValidationFailure,
                        Component,
                        Operation,
                        "Duplicate output stream id");
                    continue;
                }

                streamIds.push_back(stream.streamId.value);
                ValidateStream(*capability, stream, videoStreams, audioStreams, result);
            }

            ValidateRequirement(
                capability->videoRequirement,
                videoStreams,
                "Video stream is required by output profile",
                "Video stream is disallowed by output profile",
                result);
            ValidateRequirement(
                capability->audioRequirement,
                audioStreams,
                "Audio stream is required by output profile",
                "Audio stream is disallowed by output profile",
                result);

            if (videoStreams > capability->maxVideoStreams)
            {
                result.diagnostics.AddError(
                    CoreResultCode::UnsupportedOperation,
                    Component,
                    Operation,
                    "Output profile accepts too many video streams");
            }

            if (audioStreams > capability->maxAudioStreams)
            {
                result.diagnostics.AddError(
                    CoreResultCode::UnsupportedOperation,
                    Component,
                    Operation,
                    "Output profile accepts too many audio streams");
            }

            return result;
        }

    private:
        static constexpr const char* Component = "MediaFoundationSinkProfileValidator";
        static constexpr const char* Operation = "Validate";

        MediaFoundationSinkCapabilities m_capabilities;

        static void ValidateStream(
            const MediaFoundationContainerCapability& capability,
            const OutputStreamPlan& stream,
            uint32_t& videoStreams,
            uint32_t& audioStreams,
            MediaFoundationProfileValidationResult& result)
        {
            if (stream.IsVideo())
            {
                ++videoStreams;
                if (capability.videoRequirement == OutputStreamRequirement::Disallowed)
                {
                    return;
                }

                if (!stream.video.has_value() || !capability.SupportsVideoCodec(stream.video->codec))
                {
                    result.diagnostics.AddError(
                        CoreResultCode::UnsupportedOperation,
                        Component,
                        Operation,
                        "Video codec is not supported by output profile");
                }
                return;
            }

            if (stream.IsAudio())
            {
                ++audioStreams;
                if (capability.audioRequirement == OutputStreamRequirement::Disallowed)
                {
                    return;
                }

                if (!stream.audio.has_value() || !capability.SupportsAudioCodec(stream.audio->codec))
                {
                    result.diagnostics.AddError(
                        CoreResultCode::UnsupportedOperation,
                        Component,
                        Operation,
                        "Audio codec is not supported by output profile");
                }
                return;
            }

            result.diagnostics.AddError(
                CoreResultCode::UnsupportedOperation,
                Component,
                Operation,
                "Output stream kind is not supported");
        }

        static void ValidateRequirement(
            OutputStreamRequirement requirement,
            uint32_t streamCount,
            const char* requiredMessage,
            const char* disallowedMessage,
            MediaFoundationProfileValidationResult& result)
        {
            if (requirement == OutputStreamRequirement::Required && streamCount == 0)
            {
                result.diagnostics.AddError(
                    CoreResultCode::UnsupportedOperation,
                    Component,
                    Operation,
                    requiredMessage);
            }

            if (requirement == OutputStreamRequirement::Disallowed && streamCount != 0)
            {
                result.diagnostics.AddError(
                    CoreResultCode::UnsupportedOperation,
                    Component,
                    Operation,
                    disallowedMessage);
            }
        }
    };
}
