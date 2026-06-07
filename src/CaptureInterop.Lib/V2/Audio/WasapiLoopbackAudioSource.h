#pragma once

#include "IWasapiLoopbackAudioProvider.h"

#include <algorithm>
#include <functional>
#include <memory>
#include <mutex>
#include <utility>
#include <vector>

namespace CaptureInterop::V2::Audio
{
    class WasapiLoopbackAudioSource final : public IAudioCaptureSource
    {
    public:
        WasapiLoopbackAudioSource(
            WasapiLoopbackAudioSourceConfig config,
            std::shared_ptr<IWasapiLoopbackAudioProvider> provider = nullptr)
            : m_config(std::move(config)),
              m_provider(std::move(provider))
        {
            m_muted = m_config.controls.initiallyMuted;
            m_gainDb = m_config.controls.initialGain.gainDb;
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
            std::lock_guard lock(m_mutex);
            return m_muted;
        }

        [[nodiscard]] float GainDb() const noexcept
        {
            std::lock_guard lock(m_mutex);
            return m_gainDb;
        }

        [[nodiscard]] OperationResult Start() noexcept override
        {
            if (!m_config.armed)
            {
                return OperationResult::Failure(
                    CoreResultCode::UnsupportedOperation,
                    "WasapiLoopbackAudioSource",
                    "Start",
                    "System audio source is not armed");
            }

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
            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult Stop() noexcept override
        {
            std::lock_guard lock(m_mutex);
            m_started = false;
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

            std::lock_guard lock(m_mutex);
            m_muted = muted;
            return OperationResult::Success();
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

            std::lock_guard lock(m_mutex);
            m_gainDb = gainDb;
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
            std::vector<HandlerEntry> handlers;
            uint64_t nextHandlerId{ 1 };
        };

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
        mutable std::mutex m_mutex;
        std::shared_ptr<CallbackState> m_callbackState{ std::make_shared<CallbackState>() };
        float m_gainDb{ AudioGainSettings::DefaultGainDb };
        bool m_muted{ false };
        bool m_started{ false };
    };
}
