#pragma once

#include "MediaTypes.h"

#include <cstdint>
#include <optional>
#include <string>
#include <variant>
#include <vector>

namespace CaptureInterop::V2
{
    struct CaptureRectangle
    {
        int32_t x{ 0 };
        int32_t y{ 0 };
        uint32_t width{ 0 };
        uint32_t height{ 0 };

        [[nodiscard]] bool IsValid() const noexcept
        {
            return width != 0 && height != 0;
        }

        friend bool operator==(const CaptureRectangle& left, const CaptureRectangle& right) = default;
    };

    struct DesktopSourceConfig
    {
        SourceId id;
        StreamId videoStreamId;
        std::string name;
        std::string displayId;
        std::string monitorDeviceName;
        uintptr_t monitorHandle{ 0 };
        std::optional<CaptureRectangle> captureArea;
        std::optional<VideoMediaType> resolvedVideoMediaType;
        CursorCapturePolicy cursorPolicy{ CursorCapturePolicy::Included };
        Rational frameRate;

        [[nodiscard]] bool HasCaptureArea() const noexcept
        {
            return captureArea.has_value();
        }
    };

    struct AudioSourceControlConfig
    {
        bool initiallyMuted{ false };
        AudioGainSettings initialGain;
    };

    struct SystemAudioSourceConfig
    {
        SourceId id;
        std::string name;
        std::string deviceId;
        bool useDefaultDevice{ true };
        bool armed{ false };
        AudioSourceControlConfig controls;
    };

    struct MicrophoneSourceConfig
    {
        SourceId id;
        StreamId audioStreamId;
        std::string name;
        std::string deviceId;
        bool useDefaultDevice{ true };
        bool armed{ false };
        AudioSourceControlConfig controls;

        // Future microphone hardware gain, endpoint mute, echo cancellation, and
        // monitoring are intentionally separate from first-version recording gain.
    };

    struct AudioMixerInputContract
    {
        uint32_t normalizedSampleRate{ 48000 };
        uint16_t normalizedChannels{ 2 };
        AudioSampleFormat normalizedSampleFormat{ AudioSampleFormat::Float32 };
        bool sourceSpecificControlsBeforeMix{ true };
        bool firstImplementationWritesSingleAudioTrack{ true };
    };

    class SourceConfig
    {
    public:
        using Data = std::variant<DesktopSourceConfig, SystemAudioSourceConfig, MicrophoneSourceConfig>;

        static SourceConfig Desktop(DesktopSourceConfig config)
        {
            return SourceConfig(std::move(config));
        }

        static SourceConfig SystemAudio(SystemAudioSourceConfig config)
        {
            return SourceConfig(std::move(config));
        }

        static SourceConfig Microphone(MicrophoneSourceConfig config)
        {
            return SourceConfig(std::move(config));
        }

        [[nodiscard]] SourceKind Kind() const noexcept
        {
            if (std::holds_alternative<DesktopSourceConfig>(m_data))
            {
                return SourceKind::Desktop;
            }

            if (std::holds_alternative<SystemAudioSourceConfig>(m_data))
            {
                return SourceKind::SystemAudio;
            }

            return SourceKind::Microphone;
        }

        [[nodiscard]] SourceId Id() const noexcept
        {
            if (const auto* desktop = AsDesktop())
            {
                return desktop->id;
            }

            if (const auto* systemAudio = AsSystemAudio())
            {
                return systemAudio->id;
            }

            return AsMicrophone()->id;
        }

        [[nodiscard]] bool IsVideo() const noexcept
        {
            return Kind() == SourceKind::Desktop;
        }

        [[nodiscard]] bool IsAudio() const noexcept
        {
            const SourceKind kind = Kind();
            return kind == SourceKind::SystemAudio || kind == SourceKind::Microphone;
        }

        [[nodiscard]] const DesktopSourceConfig* AsDesktop() const noexcept
        {
            return std::get_if<DesktopSourceConfig>(&m_data);
        }

        [[nodiscard]] DesktopSourceConfig* AsDesktop() noexcept
        {
            return std::get_if<DesktopSourceConfig>(&m_data);
        }

        [[nodiscard]] const SystemAudioSourceConfig* AsSystemAudio() const noexcept
        {
            return std::get_if<SystemAudioSourceConfig>(&m_data);
        }

        [[nodiscard]] SystemAudioSourceConfig* AsSystemAudio() noexcept
        {
            return std::get_if<SystemAudioSourceConfig>(&m_data);
        }

        [[nodiscard]] const MicrophoneSourceConfig* AsMicrophone() const noexcept
        {
            return std::get_if<MicrophoneSourceConfig>(&m_data);
        }

        [[nodiscard]] MicrophoneSourceConfig* AsMicrophone() noexcept
        {
            return std::get_if<MicrophoneSourceConfig>(&m_data);
        }

    private:
        explicit SourceConfig(DesktopSourceConfig config)
            : m_data(std::move(config))
        {
        }

        explicit SourceConfig(SystemAudioSourceConfig config)
            : m_data(std::move(config))
        {
        }

        explicit SourceConfig(MicrophoneSourceConfig config)
            : m_data(std::move(config))
        {
        }

        Data m_data;
    };

    struct RecordingControlSettings
    {
        bool pauseResumeEnabled{ true };
        bool runtimeAudioMuteEnabled{ true };
        bool runtimeAudioGainEnabled{ true };
    };

    struct DiagnosticsSettings
    {
        bool collectCounters{ true };
        bool verboseLogging{ false };
    };

    struct CapturePipelineConfig
    {
        std::vector<SourceConfig> sources;
        OutputSettings output;
        RecordingControlSettings controls;
        ToneMappingSettings toneMapping;
        DiagnosticsSettings diagnostics;
        AudioMixerInputContract audioMixer;

        [[nodiscard]] const SourceConfig* FindSource(SourceId id) const noexcept
        {
            for (const SourceConfig& source : sources)
            {
                if (source.Id() == id)
                {
                    return &source;
                }
            }

            return nullptr;
        }

        [[nodiscard]] SourceConfig* FindSource(SourceId id) noexcept
        {
            for (SourceConfig& source : sources)
            {
                if (source.Id() == id)
                {
                    return &source;
                }
            }

            return nullptr;
        }

        [[nodiscard]] const SystemAudioSourceConfig* FindSystemAudioSource(SourceId id) const noexcept
        {
            const SourceConfig* source = FindSource(id);
            return source == nullptr ? nullptr : source->AsSystemAudio();
        }

        [[nodiscard]] SystemAudioSourceConfig* FindSystemAudioSource(SourceId id) noexcept
        {
            SourceConfig* source = FindSource(id);
            return source == nullptr ? nullptr : source->AsSystemAudio();
        }

        [[nodiscard]] bool HasSource(SourceId id) const noexcept
        {
            return FindSource(id) != nullptr;
        }
    };
}
