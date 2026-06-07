#pragma once

#include "IWasapiLoopbackAudioProvider.h"
#include "IWasapiLoopbackPacketProvider.h"
#include "V2/Core/AudioControlProcessors.h"

#include <algorithm>
#include <atomic>
#include <chrono>
#include <condition_variable>
#include <cmath>
#include <cstring>
#include <functional>
#include <limits>
#include <memory>
#include <mutex>
#include <optional>
#include <thread>
#include <utility>
#include <vector>

namespace CaptureInterop::V2::Audio
{
    struct WasapiLoopbackAudioSourceDiagnostics
    {
        SourceId sourceId;
        StreamId streamId;
        uint64_t droppedPausedFrames{ 0 };
        uint64_t clippedSamples{ 0 };
        uint64_t packetsRead{ 0 };
        uint64_t framesRead{ 0 };
        uint64_t silentPackets{ 0 };
        uint64_t synthesizedSilencePackets{ 0 };
        uint64_t synthesizedSilenceFrames{ 0 };
        uint64_t discontinuities{ 0 };
        uint64_t packetGaps{ 0 };
        uint64_t providerFailures{ 0 };
        int64_t bufferDuration100ns{ 0 };
        bool eventDrivenCapture{ false };
        bool pollingFallbackUsed{ false };
        AudioTimestampSource lastTimestampSource{ AudioTimestampSource::Unknown };
        AudioMediaType mediaType;
        std::wstring endpointId;
        std::wstring endpointName;
        std::string lastFailureComponent;
        std::string lastFailureOperation;
        std::optional<int64_t> lastNativeStatus;
        std::string lastFailureMessage;
    };

    class WasapiLoopbackAudioSource final :
        public IAudioCaptureSource,
        public IAudioMuteProcessor,
        public IAudioGainProcessor,
        public ISourcePauseControl
    {
    public:
        WasapiLoopbackAudioSource(
            WasapiLoopbackAudioSourceConfig config,
            std::shared_ptr<IWasapiLoopbackAudioProvider> provider = nullptr,
            std::shared_ptr<IWasapiLoopbackPacketProvider> packetProvider = nullptr)
            : m_config(std::move(config)),
              m_provider(std::move(provider)),
              m_packetProvider(std::move(packetProvider))
        {
            m_muted.store(m_config.controls.initiallyMuted);
            m_gainDb.store(m_config.controls.initialGain.gainDb);
        }

        ~WasapiLoopbackAudioSource() override
        {
            [[maybe_unused]] OperationResult stopResult = Stop();
        }

        [[nodiscard]] SourceDescriptor Describe() const override
        {
            return m_config.SourceDescriptor();
        }

        [[nodiscard]] std::vector<StreamDescriptor> Streams() const override
        {
            return { BuildWasapiLoopbackAudioStream(m_config) };
        }

        [[nodiscard]] AudioMediaType CurrentMediaType() const
        {
            return m_provider ? m_provider->CurrentMediaType() : m_config.mediaType;
        }

        [[nodiscard]] std::string ProviderName() const
        {
            return m_provider ? m_provider->ProviderName() : std::string{};
        }

        [[nodiscard]] bool IsArmed() const noexcept
        {
            return m_config.armed;
        }

        [[nodiscard]] bool IsStarted() const noexcept
        {
            std::lock_guard lock(m_mutex);
            return m_started;
        }

        [[nodiscard]] bool IsMuted() const noexcept
        {
            return m_muted.load();
        }

        [[nodiscard]] float GainDb() const noexcept
        {
            return m_gainDb.load();
        }

        [[nodiscard]] bool IsPaused() const noexcept
        {
            return m_paused.load();
        }

        [[nodiscard]] SourceId ControlledSource() const noexcept override
        {
            return m_config.sourceId;
        }

        [[nodiscard]] WasapiLoopbackAudioSourceDiagnostics Diagnostics() const noexcept
        {
            WasapiLoopbackAudioSourceDiagnostics diagnostics;
            {
                std::lock_guard lock(m_mutex);
                diagnostics = m_diagnostics;
            }

            diagnostics.sourceId = m_config.sourceId;
            diagnostics.streamId = m_config.audioStreamId;
            diagnostics.mediaType = m_config.mediaType;
            if (m_packetProvider)
            {
                const WasapiLoopbackPacketProviderDiagnostics providerDiagnostics =
                    m_packetProvider->Diagnostics();
                diagnostics.packetsRead = providerDiagnostics.packetsRead;
                diagnostics.framesRead = providerDiagnostics.framesRead;
                diagnostics.silentPackets = providerDiagnostics.silentPackets;
                diagnostics.synthesizedSilencePackets = providerDiagnostics.synthesizedSilencePackets;
                diagnostics.synthesizedSilenceFrames = providerDiagnostics.synthesizedSilenceFrames;
                diagnostics.discontinuities = providerDiagnostics.discontinuities;
                diagnostics.packetGaps = providerDiagnostics.packetGaps;
                diagnostics.providerFailures += providerDiagnostics.providerFailures;
                diagnostics.bufferDuration100ns = providerDiagnostics.bufferDuration100ns;
                diagnostics.eventDrivenCapture = providerDiagnostics.eventDrivenCapture;
                diagnostics.pollingFallbackUsed = providerDiagnostics.pollingFallbackUsed;
                diagnostics.lastTimestampSource = providerDiagnostics.lastTimestampSource;
                diagnostics.mediaType = providerDiagnostics.mediaType.IsValid()
                    ? providerDiagnostics.mediaType
                    : diagnostics.mediaType;
                diagnostics.endpointId = providerDiagnostics.endpointId;
                diagnostics.endpointName = providerDiagnostics.endpointName;
            }

            return diagnostics;
        }

        [[nodiscard]] OperationResult Start() noexcept override
        {
            std::lock_guard lifecycleLock(m_lifecycleMutex);
            if (!m_config.armed)
            {
                return OperationResult::Failure(
                    CoreResultCode::UnsupportedOperation,
                    "WasapiLoopbackAudioSource",
                    "Start",
                    "System audio source is not armed");
            }

            {
                std::lock_guard lock(m_mutex);
                if (m_started)
                {
                    return OperationResult::Failure(
                        CoreResultCode::InvalidState,
                        "WasapiLoopbackAudioSource",
                        "Start",
                        "System audio source is already started");
                }

                m_started = true;
            }

            m_stopRequested.store(false);
            EnableCallbacks();
            if (m_packetProvider)
            {
                OperationResult initializeResult = m_packetProvider->Initialize(m_config);
                if (initializeResult.IsFailure())
                {
                    {
                        std::lock_guard lock(m_mutex);
                        m_started = false;
                    }
                    RecordProviderFailure(initializeResult);
                    DisableCallbacks();
                    return initializeResult;
                }

                OperationResult startResult = m_packetProvider->Start();
                if (startResult.IsFailure())
                {
                    {
                        std::lock_guard lock(m_mutex);
                        m_started = false;
                    }
                    RecordProviderFailure(startResult);
                    DisableCallbacks();
                    [[maybe_unused]] OperationResult stopResult = m_packetProvider->Stop();
                    return startResult;
                }

                m_worker = std::thread(
                    [this]
                    {
                        CaptureLoop();
                    });
            }

            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult Stop() noexcept override
        {
            std::lock_guard lifecycleLock(m_lifecycleMutex);
            DisableCallbacks();
            m_stopRequested.store(true);

            std::shared_ptr<IWasapiLoopbackPacketProvider> packetProvider;
            bool shouldStopProvider = false;
            {
                std::lock_guard lock(m_mutex);
                shouldStopProvider = m_started || m_worker.joinable();
                m_started = false;
                packetProvider = m_packetProvider;
            }

            if (m_worker.joinable())
            {
                m_worker.join();
            }

            WaitForCallbackDrain();

            if (packetProvider && shouldStopProvider)
            {
                return packetProvider->Stop();
            }

            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult SetMuted(SourceId sourceId, bool muted) noexcept
        {
            if (sourceId != m_config.sourceId)
            {
                return OperationResult::Failure(
                    CoreResultCode::NotFound,
                    "WasapiLoopbackAudioSource",
                    "SetMuted",
                    "System audio source id was not found");
            }

            if (!m_config.armed)
            {
                return OperationResult::Failure(
                    CoreResultCode::UnsupportedOperation,
                    "WasapiLoopbackAudioSource",
                    "SetMuted",
                    "System audio source is not armed");
            }

            m_muted.store(muted);
            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult SetMuted(bool muted) noexcept override
        {
            return SetMuted(m_config.sourceId, muted);
        }

        [[nodiscard]] OperationResult SetGainDb(SourceId sourceId, float gainDb) noexcept
        {
            if (sourceId != m_config.sourceId)
            {
                return OperationResult::Failure(
                    CoreResultCode::NotFound,
                    "WasapiLoopbackAudioSource",
                    "SetGainDb",
                    "System audio source id was not found");
            }

            if (!m_config.armed)
            {
                return OperationResult::Failure(
                    CoreResultCode::UnsupportedOperation,
                    "WasapiLoopbackAudioSource",
                    "SetGainDb",
                    "System audio source is not armed");
            }

            AudioGainSettings settings;
            settings.gainDb = gainDb;
            if (!settings.IsInSupportedRange())
            {
                return OperationResult::Failure(
                    CoreResultCode::RangeError,
                    "WasapiLoopbackAudioSource",
                    "SetGainDb",
                    "Audio gain is outside the supported range");
            }

            m_gainDb.store(gainDb);
            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult SetGainDb(float gainDb) noexcept override
        {
            return SetGainDb(m_config.sourceId, gainDb);
        }

        [[nodiscard]] OperationResult SetPaused(bool paused) noexcept override
        {
            m_paused.store(paused);
            return OperationResult::Success();
        }

        [[nodiscard]] CallbackRegistrationToken RegisterSampleArrivedHandler(AudioSampleHandler handler) override
        {
            if (!handler)
            {
                return nullptr;
            }

            uint64_t id = 0;
            {
                std::lock_guard lock(m_callbackState->mutex);
                id = m_callbackState->nextHandlerId++;
                m_callbackState->handlers.push_back(HandlerEntry{ id, std::move(handler) });
            }

            return std::make_unique<CallbackToken>(
                [state = std::weak_ptr<CallbackState>(m_callbackState), id]
                {
                    Unregister(state, id);
                });
        }

    private:
        class CallbackToken final : public ICallbackRegistration
        {
        public:
            explicit CallbackToken(std::function<void()> unregister)
                : m_unregister(std::move(unregister))
            {
            }

            ~CallbackToken() override
            {
                if (m_unregister)
                {
                    m_unregister();
                }
            }

        private:
            std::function<void()> m_unregister;
        };

        struct HandlerEntry
        {
            uint64_t id{ 0 };
            AudioSampleHandler handler;
        };

        struct CallbackState
        {
            std::mutex mutex;
            std::condition_variable idleCondition;
            std::vector<HandlerEntry> handlers;
            uint64_t nextHandlerId{ 1 };
            size_t activeDispatches{ 0 };
            bool acceptingCallbacks{ false };
        };

        void EnableCallbacks()
        {
            std::lock_guard lock(m_callbackState->mutex);
            m_callbackState->acceptingCallbacks = true;
        }

        void DisableCallbacks()
        {
            std::lock_guard lock(m_callbackState->mutex);
            m_callbackState->acceptingCallbacks = false;
        }

        void WaitForCallbackDrain()
        {
            std::unique_lock lock(m_callbackState->mutex);
            m_callbackState->idleCondition.wait(
                lock,
                [&]
                {
                    return m_callbackState->activeDispatches == 0;
                });
        }

        void RecordProviderFailure(const OperationResult& result)
        {
            std::lock_guard lock(m_mutex);
            if (result.diagnostic.has_value())
            {
                m_diagnostics.lastFailureComponent = result.diagnostic->component;
                m_diagnostics.lastFailureOperation = result.diagnostic->operation;
                m_diagnostics.lastNativeStatus = result.diagnostic->nativeStatus;
                m_diagnostics.lastFailureMessage = result.diagnostic->message;
            }
        }

        void CaptureLoop()
        {
            while (!m_stopRequested.load())
            {
                std::optional<AudioSample> packet = m_packetProvider->TryReadPacket();
                if (packet.has_value())
                {
                    PublishSample(packet.value());
                    continue;
                }

                std::this_thread::sleep_for(std::chrono::milliseconds(1));
            }
        }

        void PublishSample(const AudioSample& sample)
        {
            const bool paused = m_paused.load();
            const bool muted = m_muted.load();
            const float gainDb = m_gainDb.load();
            if (paused)
            {
                std::lock_guard lock(m_mutex);
                m_diagnostics.droppedPausedFrames += sample.frameCount;
                return;
            }

            AudioSample outputSample = sample;
            if (!muted)
            {
                const uint64_t clippedSamples = ApplyGain(outputSample, gainDb);
                if (clippedSamples != 0)
                {
                    std::lock_guard lock(m_mutex);
                    m_diagnostics.clippedSamples += clippedSamples;
                }
            }

            if (muted)
            {
                outputSample = AudioSilenceGenerator::CreateSilenceLike(outputSample);
            }

            std::vector<AudioSampleHandler> handlers;
            {
                std::lock_guard lock(m_callbackState->mutex);
                if (!m_callbackState->acceptingCallbacks)
                {
                    return;
                }

                handlers.reserve(m_callbackState->handlers.size());
                for (const HandlerEntry& entry : m_callbackState->handlers)
                {
                    handlers.push_back(entry.handler);
                }
                ++m_callbackState->activeDispatches;
            }

            try
            {
                for (const AudioSampleHandler& handler : handlers)
                {
                    handler(outputSample);
                }
            }
            catch (...)
            {
                FinishCallbackDispatch();
                throw;
            }

            FinishCallbackDispatch();
        }

        void FinishCallbackDispatch()
        {
            std::lock_guard lock(m_callbackState->mutex);
            --m_callbackState->activeDispatches;
            if (m_callbackState->activeDispatches == 0)
            {
                m_callbackState->idleCondition.notify_all();
            }
        }

        [[nodiscard]] static uint64_t ApplyGain(AudioSample& sample, float gainDb)
        {
            if (gainDb == AudioGainSettings::DefaultGainDb || sample.pcmData.empty())
            {
                return 0;
            }

            const double scalar = std::pow(10.0, static_cast<double>(gainDb) / 20.0);
            switch (sample.mediaType.sampleFormat)
            {
            case AudioSampleFormat::Pcm16:
                return ApplyIntegerGain<int16_t>(sample.pcmData, scalar);
            case AudioSampleFormat::Pcm24:
                return ApplyPcm24Gain(sample.pcmData, scalar);
            case AudioSampleFormat::Pcm32:
                return ApplyIntegerGain<int32_t>(sample.pcmData, scalar);
            case AudioSampleFormat::Float32:
                return ApplyFloat32Gain(sample.pcmData, scalar);
            default:
                return 0;
            }
        }

        template <typename TSample>
        [[nodiscard]] static uint64_t ApplyIntegerGain(
            std::vector<uint8_t>& data,
            double scalar)
        {
            constexpr size_t SampleBytes = sizeof(TSample);
            uint64_t clippedSamples = 0;
            for (size_t offset = 0; offset + SampleBytes <= data.size(); offset += SampleBytes)
            {
                TSample value{};
                std::memcpy(&value, data.data() + offset, SampleBytes);
                const double scaled = static_cast<double>(value) * scalar;
                const double clipped = std::clamp(
                    scaled,
                    static_cast<double>((std::numeric_limits<TSample>::min)()),
                    static_cast<double>((std::numeric_limits<TSample>::max)()));
                if (clipped != scaled)
                {
                    ++clippedSamples;
                }

                const auto output = static_cast<TSample>(std::llround(clipped));
                std::memcpy(data.data() + offset, &output, SampleBytes);
            }

            return clippedSamples;
        }

        [[nodiscard]] static uint64_t ApplyPcm24Gain(
            std::vector<uint8_t>& data,
            double scalar)
        {
            constexpr int32_t MinimumPcm24 = -8'388'608;
            constexpr int32_t MaximumPcm24 = 8'388'607;
            uint64_t clippedSamples = 0;
            for (size_t offset = 0; offset + 3 <= data.size(); offset += 3)
            {
                int32_t value =
                    static_cast<int32_t>(data[offset])
                    | (static_cast<int32_t>(data[offset + 1]) << 8)
                    | (static_cast<int32_t>(data[offset + 2]) << 16);
                if ((value & 0x00800000) != 0)
                {
                    value |= 0xFF000000;
                }

                const double scaled = static_cast<double>(value) * scalar;
                const double clipped = std::clamp(
                    scaled,
                    static_cast<double>(MinimumPcm24),
                    static_cast<double>(MaximumPcm24));
                if (clipped != scaled)
                {
                    ++clippedSamples;
                }

                const int32_t output = static_cast<int32_t>(std::llround(clipped));
                data[offset] = static_cast<uint8_t>(output & 0xFF);
                data[offset + 1] = static_cast<uint8_t>((output >> 8) & 0xFF);
                data[offset + 2] = static_cast<uint8_t>((output >> 16) & 0xFF);
            }

            return clippedSamples;
        }

        [[nodiscard]] static uint64_t ApplyFloat32Gain(
            std::vector<uint8_t>& data,
            double scalar)
        {
            uint64_t clippedSamples = 0;
            for (size_t offset = 0; offset + sizeof(float) <= data.size(); offset += sizeof(float))
            {
                float value = 0.0f;
                std::memcpy(&value, data.data() + offset, sizeof(float));
                const double scaled = static_cast<double>(value) * scalar;
                const double clipped = std::clamp(scaled, -1.0, 1.0);
                if (clipped != scaled)
                {
                    ++clippedSamples;
                }

                const float output = static_cast<float>(clipped);
                std::memcpy(data.data() + offset, &output, sizeof(float));
            }

            return clippedSamples;
        }

        static void Unregister(
            const std::weak_ptr<CallbackState>& weakState,
            uint64_t id)
        {
            std::shared_ptr<CallbackState> state = weakState.lock();
            if (!state)
            {
                return;
            }

            std::lock_guard lock(state->mutex);
            state->handlers.erase(
                std::remove_if(
                    state->handlers.begin(),
                    state->handlers.end(),
                    [id](const HandlerEntry& entry)
                    {
                        return entry.id == id;
                    }),
                state->handlers.end());
        }

        WasapiLoopbackAudioSourceConfig m_config;
        std::shared_ptr<IWasapiLoopbackAudioProvider> m_provider;
        std::shared_ptr<IWasapiLoopbackPacketProvider> m_packetProvider;
        // Lifecycle transitions are serialized by m_lifecycleMutex. Runtime command
        // state is atomic so the capture thread can snapshot pause/mute/gain without
        // taking the diagnostics/lifecycle lock. Callback publication copies handlers
        // and tracks active dispatches before invoking observers without internal locks.
        mutable std::mutex m_lifecycleMutex;
        mutable std::mutex m_mutex;
        std::shared_ptr<CallbackState> m_callbackState{ std::make_shared<CallbackState>() };
        std::thread m_worker;
        std::atomic_bool m_stopRequested{ false };
        std::atomic<float> m_gainDb{ AudioGainSettings::DefaultGainDb };
        std::atomic_bool m_muted{ false };
        std::atomic_bool m_paused{ false };
        bool m_started{ false };
        WasapiLoopbackAudioSourceDiagnostics m_diagnostics;
    };
}
