#pragma once

#include "IDesktopCaptureProvider.h"

#include <algorithm>
#include <mutex>
#include <utility>

namespace CaptureInterop::V2::Desktop
{
    class FakeDesktopCaptureProvider final : public IDesktopCaptureProvider
    {
    public:
        FakeDesktopCaptureProvider(
            SourceDescriptor source,
            std::vector<StreamDescriptor> streams,
            VideoMediaType mediaType)
            : m_source(std::move(source)),
              m_streams(std::move(streams)),
              m_mediaType(mediaType)
        {
            m_diagnostics.providerName = ProviderName();
        }

        [[nodiscard]] std::string ProviderName() const override
        {
            return "FakeDesktopCaptureProvider";
        }

        [[nodiscard]] SourceDescriptor DescribeSource() const override
        {
            return m_source;
        }

        [[nodiscard]] std::vector<StreamDescriptor> DescribeStreams() const override
        {
            return m_streams;
        }

        [[nodiscard]] VideoMediaType CurrentMediaType() const override
        {
            return m_mediaType;
        }

        [[nodiscard]] DesktopCaptureProviderDiagnostics Diagnostics() const override
        {
            std::lock_guard lock(m_mutex);
            return m_diagnostics;
        }

        [[nodiscard]] OperationResult Start() noexcept override
        {
            std::lock_guard lock(m_mutex);
            if (m_started)
            {
                return OperationResult::Failure(
                    CoreResultCode::InvalidState,
                    ProviderName(),
                    "Start",
                    "Provider is already started");
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

        [[nodiscard]] CallbackRegistrationToken RegisterFrameArrivedHandler(
            DesktopCaptureFrameHandler handler) override
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

        [[nodiscard]] OperationResult EmitFrame(DesktopCaptureFrame frame)
        {
            std::vector<DesktopCaptureFrameHandler> handlers;
            {
                std::lock_guard lock(m_mutex);
                if (!m_started)
                {
                    return OperationResult::Failure(
                        CoreResultCode::InvalidState,
                        ProviderName(),
                        "EmitFrame",
                        "Provider must be started before emitting frames");
                }

                ++m_diagnostics.framesProduced;
                handlers.reserve(m_handlers.size());
                for (const HandlerEntry& entry : m_handlers)
                {
                    handlers.push_back(entry.handler);
                }
            }

            for (const DesktopCaptureFrameHandler& handler : handlers)
            {
                handler(frame);
            }

            return OperationResult::Success();
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
            DesktopCaptureFrameHandler handler;
        };

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

        SourceDescriptor m_source;
        std::vector<StreamDescriptor> m_streams;
        VideoMediaType m_mediaType;
        mutable std::mutex m_mutex;
        std::vector<HandlerEntry> m_handlers;
        DesktopCaptureProviderDiagnostics m_diagnostics;
        uint64_t m_nextHandlerId{ 1 };
        bool m_started{ false };
    };
}
