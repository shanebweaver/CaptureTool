#pragma once

#include "CapturePipelineConfigValidator.h"

#include <optional>
#include <utility>
#include <vector>

namespace CaptureInterop::V2
{
    struct OutputStreamPlan
    {
        StreamId streamId;
        SourceId sourceId;
        MediaKind kind{ MediaKind::Unknown };
        std::optional<VideoEncodingSettings> video;
        std::optional<AudioEncodingSettings> audio;
        std::optional<VideoMediaType> videoMediaType;

        [[nodiscard]] bool IsVideo() const noexcept
        {
            return kind == MediaKind::Video;
        }

        [[nodiscard]] bool IsAudio() const noexcept
        {
            return kind == MediaKind::Audio;
        }
    };

    struct OutputPlan
    {
        ContainerFormat container{ ContainerFormat::Mp4 };
        std::wstring outputPath;
        std::vector<OutputStreamPlan> streams;

        [[nodiscard]] bool HasVideoStream() const noexcept
        {
            for (const OutputStreamPlan& stream : streams)
            {
                if (stream.IsVideo())
                {
                    return true;
                }
            }

            return false;
        }

        [[nodiscard]] bool HasAudioStream() const noexcept
        {
            for (const OutputStreamPlan& stream : streams)
            {
                if (stream.IsAudio())
                {
                    return true;
                }
            }

            return false;
        }
    };

    struct OutputProfileResolutionResult
    {
        std::optional<OutputPlan> plan;
        ValidationResult diagnostics;

        [[nodiscard]] bool IsSuccess() const noexcept
        {
            return plan.has_value() && diagnostics.IsValid();
        }
    };

    class OutputProfileResolver
    {
    public:
        [[nodiscard]] OutputProfileResolutionResult Resolve(const CapturePipelineConfig& config) const
        {
            OutputProfileResolutionResult result;
            result.diagnostics = m_validator.Validate(config);
            if (!result.diagnostics.IsValid())
            {
                return result;
            }

            switch (config.output.container)
            {
            case ContainerFormat::Mp4:
                ResolveMp4(config, result);
                break;
            case ContainerFormat::Mp3:
                ResolveMp3(config, result);
                break;
            case ContainerFormat::Wav:
                ResolveWav(config, result);
                break;
            default:
                result.diagnostics.AddError(
                    CoreResultCode::ValidationFailure,
                    Component,
                    Operation,
                    "Output container is not supported");
                break;
            }

            return result;
        }

    private:
        static constexpr const char* Component = "OutputProfileResolver";
        static constexpr const char* Operation = "Resolve";

        CapturePipelineConfigValidator m_validator;

        static void ResolveMp4(
            const CapturePipelineConfig& config,
            OutputProfileResolutionResult& result)
        {
            OutputPlan plan;
            plan.container = ContainerFormat::Mp4;
            plan.outputPath = config.output.outputPath;

            uint32_t nextStreamId = 1;

            if (config.output.RequestsVideo())
            {
                if (config.output.video->codec != VideoCodec::H264)
                {
                    result.diagnostics.AddError(
                        CoreResultCode::UnsupportedOperation,
                        Component,
                        Operation,
                        "MP4 output supports H.264 video in the initial profile");
                    return;
                }

                const DesktopSourceConfig* desktop = FindFirstDesktopSource(config);
                if (desktop == nullptr)
                {
                    result.diagnostics.AddError(
                        CoreResultCode::NotFound,
                        Component,
                        Operation,
                        "MP4 video output requires a desktop source");
                    return;
                }

                plan.streams.push_back(OutputStreamPlan{
                    StreamId::FromValue(nextStreamId++),
                    desktop->id,
                    MediaKind::Video,
                    config.output.video,
                    std::nullopt,
                    BuildDesktopVideoMediaType(*desktop, *config.output.video)
                });
            }

            if (config.output.RequestsAudio())
            {
                if (config.output.audio->codec != AudioCodec::Aac)
                {
                    result.diagnostics.AddError(
                        CoreResultCode::UnsupportedOperation,
                        Component,
                        Operation,
                        "MP4 output supports AAC audio in the initial profile");
                    return;
                }

                const SystemAudioSourceConfig* audio = FindFirstArmedSystemAudioSource(config);
                if (audio == nullptr)
                {
                    result.diagnostics.AddError(
                        CoreResultCode::NotFound,
                        Component,
                        Operation,
                        "MP4 audio output requires an armed system audio source");
                    return;
                }

                plan.streams.push_back(OutputStreamPlan{
                    StreamId::FromValue(nextStreamId++),
                    audio->id,
                    MediaKind::Audio,
                    std::nullopt,
                    config.output.audio,
                    std::nullopt
                });
            }

            result.plan = std::move(plan);
        }

        static void ResolveMp3(
            const CapturePipelineConfig& config,
            OutputProfileResolutionResult& result)
        {
            if (config.output.RequestsVideo())
            {
                result.diagnostics.AddError(
                    CoreResultCode::UnsupportedOperation,
                    Component,
                    Operation,
                    "MP3 output does not support video streams");
                return;
            }

            if (!config.output.RequestsAudio() || config.output.audio->codec != AudioCodec::Mp3)
            {
                result.diagnostics.AddError(
                    CoreResultCode::UnsupportedOperation,
                    Component,
                    Operation,
                    "MP3 output requires MP3 audio settings");
                return;
            }

            const SystemAudioSourceConfig* audio = FindFirstArmedSystemAudioSource(config);
            if (audio == nullptr)
            {
                result.diagnostics.AddError(
                    CoreResultCode::NotFound,
                    Component,
                    Operation,
                    "MP3 output requires an armed system audio source");
                return;
            }

            if (HasDesktopSource(config))
            {
                result.diagnostics.AddWarning(
                    CoreResultCode::UnsupportedOperation,
                    Component,
                    Operation,
                    "Incidental video sources are pruned from MP3 output");
            }

            OutputPlan plan;
            plan.container = ContainerFormat::Mp3;
            plan.outputPath = config.output.outputPath;
            plan.streams.push_back(OutputStreamPlan{
                StreamId::FromValue(1),
                audio->id,
                MediaKind::Audio,
                std::nullopt,
                config.output.audio,
                std::nullopt
            });

            result.plan = std::move(plan);
        }

        static void ResolveWav(
            const CapturePipelineConfig&,
            OutputProfileResolutionResult& result)
        {
            result.diagnostics.AddError(
                CoreResultCode::UnsupportedOperation,
                Component,
                Operation,
                "WAV output planning is not part of the initial core profile resolver");
        }

        [[nodiscard]] static std::optional<VideoMediaType> BuildDesktopVideoMediaType(
            const DesktopSourceConfig& desktop,
            const VideoEncodingSettings& video) noexcept
        {
            if (!desktop.captureArea.has_value())
            {
                return std::nullopt;
            }

            VideoMediaType mediaType;
            mediaType.width = desktop.captureArea->width;
            mediaType.height = desktop.captureArea->height;
            mediaType.frameRate = video.frameRate.IsValid() ? video.frameRate : desktop.frameRate;
            mediaType.pixelFormat = VideoPixelFormat::Bgra8;
            return mediaType.IsValid() ? std::optional<VideoMediaType>{ mediaType } : std::nullopt;
        }

        static const DesktopSourceConfig* FindFirstDesktopSource(const CapturePipelineConfig& config) noexcept
        {
            for (const SourceConfig& source : config.sources)
            {
                if (const DesktopSourceConfig* desktop = source.AsDesktop())
                {
                    return desktop;
                }
            }

            return nullptr;
        }

        static const SystemAudioSourceConfig* FindFirstArmedSystemAudioSource(const CapturePipelineConfig& config) noexcept
        {
            for (const SourceConfig& source : config.sources)
            {
                const SystemAudioSourceConfig* audio = source.AsSystemAudio();
                if (audio != nullptr && audio->armed)
                {
                    return audio;
                }
            }

            return nullptr;
        }

        static bool HasDesktopSource(const CapturePipelineConfig& config) noexcept
        {
            return FindFirstDesktopSource(config) != nullptr;
        }
    };
}
