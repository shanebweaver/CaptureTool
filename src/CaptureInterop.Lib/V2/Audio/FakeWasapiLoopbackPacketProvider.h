#pragma once

#include "IWasapiLoopbackPacketProvider.h"
#include "OwnedAudioSampleBuilder.h"

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

        void EnqueueSynthesizedSilence(
            uint32_t requestedFrameCount,
            MediaTime timestamp = MediaTime{})
        {
            std::lock_guard lock(m_mutex);
            AudioSample sample = BuildBoundedSynthesizedSilentAudioSample(
                m_config.sourceId,
                m_config.audioStreamId,
                timestamp,
                m_config.mediaType,
                requestedFrameCount);
            m_packets.push(Packet{ std::move(sample), false, false });
        }

        [[nodiscard]] OperationResult Initialize(
            const WasapiLoopbackAudioSourceConfig& config) override
        {
            ++m_initializeCount;
            m_config = config;
            m_diagnostics.mediaType = config.mediaType;
            if (m_failInitialize)
            {
                ++m_diagnostics.providerFailures;
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
            if (packet.silent)
            {
                packet.sample = BuildOwnedSilentAudioSample(
                    packet.sample.sourceId,
                    packet.sample.streamId,
                    packet.sample.timestamp,
                    packet.sample.mediaType,
                    packet.sample.frameCount,
                    packet.sample.sourceTiming);
                ++m_diagnostics.silentPackets;
            }
            if (packet.discontinuity)
            {
                ++m_diagnostics.discontinuities;
            }
            if (packet.sample.sourceTiming.synthesizedSilence)
            {
                ++m_diagnostics.synthesizedSilencePackets;
                m_diagnostics.synthesizedSilenceFrames += packet.sample.frameCount;
            }

            ++m_diagnostics.packetsRead;
            m_diagnostics.framesRead += packet.sample.frameCount;
            m_diagnostics.lastTimestampSource = packet.sample.sourceTiming.timestampSource;

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
        WasapiLoopbackAudioSourceConfig m_config;
        WasapiLoopbackPacketProviderDiagnostics m_diagnostics;
        int m_initializeCount{ 0 };
        int m_startCount{ 0 };
        int m_stopCount{ 0 };
        bool m_failInitialize{ false };
        bool m_started{ false };
    };
}
