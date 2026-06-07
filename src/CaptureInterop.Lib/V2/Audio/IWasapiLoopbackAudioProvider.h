#pragma once

#include "WasapiLoopbackAudioSourceConfig.h"

namespace CaptureInterop::V2::Audio
{
    class IWasapiLoopbackAudioProvider
    {
    public:
        virtual ~IWasapiLoopbackAudioProvider() = default;

        [[nodiscard]] virtual std::string ProviderName() const = 0;
        [[nodiscard]] virtual AudioMediaType CurrentMediaType() const = 0;
    };
}
