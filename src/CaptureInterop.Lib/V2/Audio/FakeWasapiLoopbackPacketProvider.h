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

        void EnqueuePacket(AudioSample sample)
        {
            std::lock_guard lock(m_mutex);
            m_packets.push(std::move(sample));
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
            ++m_stopCount;
            m_started = false;
            return OperationResult::Success();
        }

        [[nodiscard]] std::optional<AudioSample> TryReadPacket() override
        {
            std::lock_guard lock(m_mutex);
            if (m_packets.empty())
            {
                return std::nullopt;
            }

            AudioSample sample = std::move(m_packets.front());
            m_packets.pop();
            return sample;
        }

    private:
        mutable std::mutex m_mutex;
        std::queue<AudioSample> m_packets;
        int m_initializeCount{ 0 };
        int m_startCount{ 0 };
        int m_stopCount{ 0 };
        bool m_failInitialize{ false };
        bool m_started{ false };
    };
}
