#pragma once

#include "DesktopVideoSourceConfig.h"
#include "IDesktopCaptureProvider.h"

#include <algorithm>
#include <memory>
#include <mutex>
#include <utility>

namespace CaptureInterop::V2::Desktop
{
    class WindowsDesktopVideoSource final : public IVideoCaptureSource
    {
    public:
        WindowsDesktopVideoSource(
            DesktopVideoSourceConfig config,
            std::shared_ptr<IDesktopCaptureProvider> provider)
            : m_config(std::move(config)),
              m_provider(std::move(provider))
        {
        }

        [[nodiscard]] SourceDescriptor Describe() const override
        {
            return m_config.SourceDescriptor();
        }

        [[nodiscard]] std::vector<StreamDescriptor> Streams() const override
        {
            return BuildDesktopVideoStreams(m_config);
        }

        [[nodiscard]] OperationResult Start() noexcept override
        {
            if (!m_provider)
            {
                return OperationResult::Failure(
                    CoreResultCode::InvalidState,
                    "WindowsDesktopVideoSource",
                    "Start",
                    "Desktop capture provider is required");
            }

            OperationResult startResult = m_provider->Start();
            if (!startResult.IsSuccess())
            {
                return startResult;
            }

            std::lock_guard lock(m_mutex);
            if (!m_providerCallback)
            {
                m_providerCallback = m_provider->RegisterFrameArrivedHandler(
                    [this](const DesktopCaptureFrame& frame)
                    {
                        ForwardFrame(frame);
                    });
            }

            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult Stop() noexcept override
        {
            CallbackRegistrationToken providerCallback;
            {
                std::lock_guard lock(m_mutex);
                providerCallback = std::move(m_providerCallback);
            }

            providerCallback.reset();
            return m_provider ? m_provider->Stop() : OperationResult::Success();
        }

        [[nodiscard]] CallbackRegistrationToken RegisterFrameArrivedHandler(VideoSampleHandler handler) override
        {
            if (!handler)
            {
                return nullptr;
            }

            const uint64_t id = m_nextHandlerId++;
            {
                std::lock_guard lock(m_mutex);
                m_handlers.push_back(HandlerEntry{ id, std::move(handler) });
            }

            return std::make_unique<CallbackToken>(
                [this, id]
                {
                    Unregister(id);
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
            VideoSampleHandler handler;
        };

        void ForwardFrame(const DesktopCaptureFrame& frame)
        {
            std::vector<VideoSampleHandler> handlers;
            {
                std::lock_guard lock(m_mutex);
                handlers.reserve(m_handlers.size());
                for (const HandlerEntry& entry : m_handlers)
                {
                    handlers.push_back(entry.handler);
                }
            }

            VideoSample sample{
                frame.sourceId,
                frame.streamId,
                frame.timestamp,
                frame.duration,
                frame.mediaType,
                frame.placeholderPixels
            };

            for (const VideoSampleHandler& handler : handlers)
            {
                handler(sample);
            }
        }

        void Unregister(uint64_t id)
        {
            std::lock_guard lock(m_mutex);
            m_handlers.erase(
                std::remove_if(
                    m_handlers.begin(),
                    m_handlers.end(),
                    [id](const HandlerEntry& entry)
                    {
                        return entry.id == id;
                    }),
                m_handlers.end());
        }

        DesktopVideoSourceConfig m_config;
        std::shared_ptr<IDesktopCaptureProvider> m_provider;
        mutable std::mutex m_mutex;
        std::vector<HandlerEntry> m_handlers;
        CallbackRegistrationToken m_providerCallback;
        uint64_t m_nextHandlerId{ 1 };
    };
}
