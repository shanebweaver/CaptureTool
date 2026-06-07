#pragma once

#include "IDesktopCaptureProvider.h"

#include <algorithm>
#include <mutex>
#include <optional>
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
            m_diagnostics.color = BuildDesktopColorDiagnostics(m_mediaType);
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

        [[nodiscard]] std::shared_ptr<IDesktopD3DDeviceDependency> DeviceDependency() const override
        {
            std::lock_guard lock(m_mutex);
            return m_deviceDependency;
        }

        [[nodiscard]] OperationResult ConfigureDeviceDependency(
            std::shared_ptr<IDesktopD3DDeviceDependency> dependency) noexcept override
        {
            if (!dependency)
            {
                return OperationResult::Failure(
                    CoreResultCode::InvalidState,
                    ProviderName(),
                    "ConfigureDeviceDependency",
                    "D3D device dependency is required");
            }

            OperationResult health = dependency->CheckDeviceHealth();
            if (!health.IsSuccess())
            {
                return health;
            }

            std::lock_guard lock(m_mutex);
            m_deviceDependency = std::move(dependency);
            return OperationResult::Success();
        }

        void ReleaseDeviceResources() noexcept override
        {
            std::lock_guard lock(m_mutex);
            if (m_deviceDependency && m_lifecycleEvents)
            {
                m_lifecycleEvents->push_back("provider-device-resources-released");
            }

            m_deviceDependency.reset();
        }

        void SetLifecycleEvents(std::shared_ptr<std::vector<std::string>> lifecycleEvents)
        {
            std::lock_guard lock(m_mutex);
            m_lifecycleEvents = std::move(lifecycleEvents);
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

            if (!m_deviceDependency)
            {
                return OperationResult::Failure(
                    CoreResultCode::InvalidState,
                    ProviderName(),
                    "Start",
                    "D3D device dependency must be configured before provider start");
            }

            if (m_startFailure.has_value())
            {
                return *m_startFailure;
            }

            m_started = true;
            m_diagnostics.resourcesActive = true;
            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult Stop() noexcept override
        {
            std::lock_guard lock(m_mutex);
            m_started = false;
            m_diagnostics.resourcesActive = false;
            if (m_stopFailure.has_value())
            {
                return *m_stopFailure;
            }

            return OperationResult::Success();
        }

        void SetStartFailure(OperationResult failure)
        {
            std::lock_guard lock(m_mutex);
            m_startFailure = std::move(failure);
        }

        void SetStopFailure(OperationResult failure)
        {
            std::lock_guard lock(m_mutex);
            m_stopFailure = std::move(failure);
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
                    UnregisterFrameHandler(id);
                });
        }

        [[nodiscard]] CallbackRegistrationToken RegisterProviderFailedHandler(
            DesktopCaptureProviderFailureHandler handler) override
        {
            if (!handler)
            {
                return nullptr;
            }

            const uint64_t id = m_nextFailureHandlerId++;
            {
                std::lock_guard lock(m_mutex);
                m_failureHandlers.push_back(FailureHandlerEntry{ id, std::move(handler) });
            }

            return std::make_unique<CallbackToken>(
                [this, id]
                {
                    UnregisterFailureHandler(id);
                });
        }

        [[nodiscard]] OperationResult FailActiveCapture(OperationResult failure)
        {
            std::vector<DesktopCaptureProviderFailureHandler> handlers;
            {
                std::lock_guard lock(m_mutex);
                if (!m_started)
                {
                    return OperationResult::Failure(
                        CoreResultCode::InvalidState,
                        ProviderName(),
                        "FailActiveCapture",
                        "Provider must be started before failing active capture");
                }

                m_started = false;
                RecordFailureLocked(failure);
                handlers.reserve(m_failureHandlers.size());
                for (const FailureHandlerEntry& entry : m_failureHandlers)
                {
                    handlers.push_back(entry.handler);
                }
            }

            for (const DesktopCaptureProviderFailureHandler& handler : handlers)
            {
                handler(failure);
            }

            return OperationResult::Success();
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

        struct FailureHandlerEntry
        {
            uint64_t id{ 0 };
            DesktopCaptureProviderFailureHandler handler;
        };

        void UnregisterFrameHandler(uint64_t id)
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

        void UnregisterFailureHandler(uint64_t id)
        {
            std::lock_guard lock(m_mutex);
            m_failureHandlers.erase(
                std::remove_if(
                    m_failureHandlers.begin(),
                    m_failureHandlers.end(),
                    [id](const FailureHandlerEntry& entry)
                    {
                        return entry.id == id;
                    }),
                m_failureHandlers.end());
        }

        void RecordFailureLocked(const OperationResult& failure)
        {
            ++m_diagnostics.providerFailures;
            m_diagnostics.resourcesActive = false;
            if (failure.diagnostic.has_value())
            {
                m_diagnostics.lastNativeStatus = failure.diagnostic->nativeStatus;
                m_diagnostics.lastFailureOperation = failure.diagnostic->operation;
                m_diagnostics.lastFailureMessage = failure.diagnostic->message;
            }
        }

        SourceDescriptor m_source;
        std::vector<StreamDescriptor> m_streams;
        VideoMediaType m_mediaType;
        mutable std::mutex m_mutex;
        std::vector<HandlerEntry> m_handlers;
        std::vector<FailureHandlerEntry> m_failureHandlers;
        DesktopCaptureProviderDiagnostics m_diagnostics;
        std::shared_ptr<IDesktopD3DDeviceDependency> m_deviceDependency;
        std::shared_ptr<std::vector<std::string>> m_lifecycleEvents;
        std::optional<OperationResult> m_startFailure;
        std::optional<OperationResult> m_stopFailure;
        uint64_t m_nextHandlerId{ 1 };
        uint64_t m_nextFailureHandlerId{ 1 };
        bool m_started{ false };
    };
}
