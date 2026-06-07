#pragma once

#include "PipelineInterfaces.h"

namespace CaptureInterop::V2
{
    class IAudioSourceControlProcessor
    {
    public:
        virtual ~IAudioSourceControlProcessor() = default;

        [[nodiscard]] virtual SourceId ControlledSource() const noexcept = 0;
    };

    class IAudioGainProcessor : public IAudioSourceControlProcessor
    {
    public:
        [[nodiscard]] virtual float GainDb() const noexcept = 0;
        [[nodiscard]] virtual OperationResult SetGainDb(float gainDb) noexcept = 0;
    };

    class IAudioMuteProcessor : public IAudioSourceControlProcessor
    {
    public:
        [[nodiscard]] virtual bool IsMuted() const noexcept = 0;
        [[nodiscard]] virtual OperationResult SetMuted(bool muted) noexcept = 0;
    };

    struct AudioSilenceGenerator
    {
        [[nodiscard]] static AudioSample CreateSilenceLike(const AudioSample& sample)
        {
            AudioSample silence = sample;
            for (uint8_t& value : silence.pcmData)
            {
                value = 0;
            }

            return silence;
        }
    };
}
