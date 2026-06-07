#pragma once

#include "WasapiLoopbackAudioSourceConfig.h"

#include <optional>
#include <string>
#include <vector>

namespace CaptureInterop::V2::Audio
{
    struct WasapiLoopbackPacketProviderDiagnostics
    {
        uint64_t packetsRead{ 0 };
        uint64_t framesRead{ 0 };
        uint64_t silentPackets{ 0 };
        uint64_t discontinuities{ 0 };
        bool eventDrivenCapture{ false };
        bool pollingFallbackUsed{ false };
        AudioTimestampSource lastTimestampSource{ AudioTimestampSource::Unknown };
        std::wstring endpointId;
        std::wstring endpointName;
        std::vector<std::string> releaseEvents;
    };

    class IWasapiLoopbackPacketProvider
    {
    public:
        virtual ~IWasapiLoopbackPacketProvider() = default;

        [[nodiscard]] virtual OperationResult Initialize(
            const WasapiLoopbackAudioSourceConfig& config) = 0;
        [[nodiscard]] virtual OperationResult Start() = 0;
        [[nodiscard]] virtual OperationResult Stop() noexcept = 0;
        [[nodiscard]] virtual std::optional<AudioSample> TryReadPacket() = 0;
        [[nodiscard]] virtual WasapiLoopbackPacketProviderDiagnostics Diagnostics() const = 0;
    };
}
