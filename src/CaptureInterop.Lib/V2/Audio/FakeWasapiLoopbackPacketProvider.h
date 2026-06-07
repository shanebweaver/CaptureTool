#pragma once

#include "IWasapiLoopbackPacketProvider.h"

#include <condition_variable>
#include <mutex>
#include <queue>

namespace CaptureInterop::V2::Audio
{
    class FakeWasapiLoopbackPacketProvider final : public IWasapiLoopbackPacketProvider
    {
    public:
        void SimulateInitializeFailure()
        {
            m_failInitialize = true;
        }

        [[nodiscard]] int InitializeCount() const noexcept
        {
            return m_initializeCount;
        }

        [[nodiscard]] int StartCount() const noexcept
        {
            return m_startCount;
        }

        [[nodiscard]] int StopCount() const noexcept
        {
            return m_stopCount;
        }

        [[nodiscard]] bool IsStarted() const noexcept
        {
            return m_started;
        }

        void EnqueuePacket(
            AudioSample sample,
            bool silent = false,
            bool discontinuity = false)
        {
            std::lock_guard lock(m_mutex);
            m_packets.push(Packet{ std::move(sample), silent, discontinuity });
        }

        [[nodiscard]] OperationResult Initialize(
            const WasapiLoopbackAudioSourceConfig&) override
        {
            ++m_initializeCount;
            if (m_failInitialize)
            {
                return OperationResult::Failure(
                    CoreResultCode::NativeFailure,
                    "FakeWasapiLoopbackPacketProvider",
                    "Initialize",
                    "Simulated packet provider initialization failure");
            }

            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult Start() override
        {
            ++m_startCount;
            m_started = true;
            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult Stop() noexcept override
        {
            std::lock_guard lock(m_mutex);
            ++m_stopCount;
            m_started = false;
            m_diagnostics.releaseEvents.push_back("fake-packet-provider-stopped");
            return OperationResult::Success();
        }

        [[nodiscard]] std::optional<AudioSample> TryReadPacket() override
        {
            std::lock_guard lock(m_mutex);
            if (m_packets.empty())
            {
                return std::nullopt;
            }

            Packet packet = std::move(m_packets.front());
            m_packets.pop();
            ++m_diagnostics.packetsRead;
            m_diagnostics.framesRead += packet.sample.frameCount;
            if (packet.silent)
            {
                ++m_diagnostics.silentPackets;
            }
            if (packet.discontinuity)
            {
                ++m_diagnostics.discontinuities;
            }

            return packet.sample;
        }

        [[nodiscard]] WasapiLoopbackPacketProviderDiagnostics Diagnostics() const override
        {
            std::lock_guard lock(m_mutex);
            return m_diagnostics;
        }

    private:
        struct Packet
        {
            AudioSample sample;
            bool silent{ false };
            bool discontinuity{ false };
        };

        mutable std::mutex m_mutex;
        std::queue<Packet> m_packets;
        WasapiLoopbackPacketProviderDiagnostics m_diagnostics;
        int m_initializeCount{ 0 };
        int m_startCount{ 0 };
        int m_stopCount{ 0 };
        bool m_failInitialize{ false };
        bool m_started{ false };
    };
}
