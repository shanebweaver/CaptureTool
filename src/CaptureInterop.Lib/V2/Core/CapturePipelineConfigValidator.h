#pragma once

#include "CapturePipelineConfig.h"
#include "ResultTypes.h"

#include <algorithm>
#include <vector>

namespace CaptureInterop::V2
{
    class CapturePipelineConfigValidator
    {
    public:
        [[nodiscard]] ValidationResult Validate(const CapturePipelineConfig& config) const
        {
            ValidationResult result;

            ValidateSources(config, result);
            ValidateOutput(config.output, result);

            return result;
        }

    private:
        static constexpr const char* Component = "CapturePipelineConfigValidator";
        static constexpr const char* Operation = "Validate";

        static void ValidateSources(const CapturePipelineConfig& config, ValidationResult& result)
        {
            std::vector<uint32_t> seenSourceIds;

            for (const SourceConfig& source : config.sources)
            {
                const SourceId sourceId = source.Id();
                if (!sourceId.IsValid())
                {
                    result.AddError(
                        CoreResultCode::ValidationFailure,
                        Component,
                        Operation,
                        "Source id is required");
                    continue;
                }

                if (std::find(seenSourceIds.begin(), seenSourceIds.end(), sourceId.value) != seenSourceIds.end())
                {
                    result.AddError(
                        CoreResultCode::ValidationFailure,
                        Component,
                        Operation,
                        "Duplicate source id");
                }
                else
                {
                    seenSourceIds.push_back(sourceId.value);
                }

                if (const DesktopSourceConfig* desktop = source.AsDesktop())
                {
                    ValidateDesktopSource(*desktop, result);
                }
                else if (const SystemAudioSourceConfig* audio = source.AsSystemAudio())
                {
                    ValidateSystemAudioSource(*audio, result);
                }
                else if (const MicrophoneSourceConfig* microphone = source.AsMicrophone())
                {
                    ValidateMicrophoneSource(*microphone, result);
                }
            }
        }

        static void ValidateDesktopSource(const DesktopSourceConfig& source, ValidationResult& result)
        {
            if (!source.frameRate.IsValid())
            {
                result.AddError(
                    CoreResultCode::ValidationFailure,
                    Component,
                    Operation,
                    "Desktop source frame rate is required");
            }

            if (source.captureArea.has_value() && !source.captureArea->IsValid())
            {
                result.AddError(
                    CoreResultCode::ValidationFailure,
                    Component,
                    Operation,
                    "Desktop source capture area must have non-zero size");
            }
        }

        static void ValidateSystemAudioSource(const SystemAudioSourceConfig& source, ValidationResult& result)
        {
            if (source.controls.initiallyMuted && !source.armed)
            {
                result.AddError(
                    CoreResultCode::UnsupportedOperation,
                    Component,
                    Operation,
                    "An unarmed audio source cannot be initially muted");
            }

            if (!source.controls.initialGain.IsInSupportedRange())
            {
                result.AddError(
                    CoreResultCode::RangeError,
                    Component,
                    Operation,
                    "Audio gain is outside the supported range");
            }
        }

        static void ValidateMicrophoneSource(const MicrophoneSourceConfig&, ValidationResult& result)
        {
            result.AddError(
                CoreResultCode::UnsupportedOperation,
                Component,
                Operation,
                "Microphone capture is not implemented");
        }

        static void ValidateOutput(const OutputSettings& output, ValidationResult& result)
        {
            if (output.outputPath.empty())
            {
                result.AddError(
                    CoreResultCode::ValidationFailure,
                    Component,
                    Operation,
                    "Output path is required");
            }

            if (!IsKnownContainerFormat(output.container))
            {
                result.AddError(
                    CoreResultCode::ValidationFailure,
                    Component,
                    Operation,
                    "Output container is not supported");
            }

            if (!output.HasRequestedStreams() || (!output.RequestsVideo() && !output.RequestsAudio()))
            {
                result.AddError(
                    CoreResultCode::ValidationFailure,
                    Component,
                    Operation,
                    "At least one output stream is required");
            }

            if (output.video.has_value() && !IsKnownVideoCodec(output.video->codec))
            {
                result.AddError(
                    CoreResultCode::ValidationFailure,
                    Component,
                    Operation,
                    "Video codec is not supported");
            }

            if (output.audio.has_value() && !IsKnownAudioCodec(output.audio->codec))
            {
                result.AddError(
                    CoreResultCode::ValidationFailure,
                    Component,
                    Operation,
                    "Audio codec is not supported");
            }
        }

        static bool IsKnownContainerFormat(ContainerFormat format) noexcept
        {
            switch (format)
            {
            case ContainerFormat::Mp4:
            case ContainerFormat::Mp3:
            case ContainerFormat::Wav:
                return true;
            default:
                return false;
            }
        }

        static bool IsKnownVideoCodec(VideoCodec codec) noexcept
        {
            switch (codec)
            {
            case VideoCodec::None:
            case VideoCodec::H264:
            case VideoCodec::Hevc:
            case VideoCodec::Av1:
                return true;
            default:
                return false;
            }
        }

        static bool IsKnownAudioCodec(AudioCodec codec) noexcept
        {
            switch (codec)
            {
            case AudioCodec::None:
            case AudioCodec::Aac:
            case AudioCodec::Mp3:
            case AudioCodec::Pcm:
                return true;
            default:
                return false;
            }
        }
    };
}
