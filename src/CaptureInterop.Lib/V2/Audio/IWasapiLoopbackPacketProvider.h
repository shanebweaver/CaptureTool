#pragma once

#include "WasapiLoopbackAudioSourceConfig.h"

#include <optional>

namespace CaptureInterop::V2::Audio
{
    class IWasapiLoopbackPacketProvider
    {
    public:
        virtual ~IWasapiLoopbackPacketProvider() = default;

        [[nodiscard]] virtual OperationResult Initialize(
            const WasapiLoopbackAudioSourceConfig& config) = 0;
        [[nodiscard]] virtual OperationResult Start() = 0;
        [[nodiscard]] virtual OperationResult Stop() noexcept = 0;
        [[nodiscard]] virtual std::optional<AudioSample> TryReadPacket() = 0;
    };
}
