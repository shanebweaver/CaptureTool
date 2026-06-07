#pragma once

#include "DesktopMonitorResolver.h"
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
            std::shared_ptr<IDesktopCaptureProvider> provider,
            std::shared_ptr<IDesktopMonitorResolver> monitorResolver = nullptr,
            std::shared_ptr<IDesktopD3DDeviceDependency> d3dDeviceDependency = nullptr)
            : m_config(std::move(config)),
              m_provider(std::move(provider)),
              m_monitorResolver(std::move(monitorResolver)),
              m_d3dDeviceDependency(std::move(d3dDeviceDependency))
        {
        }

        ~WindowsDesktopVideoSource() override
        {
            [[maybe_unused]] OperationResult stopResult = Stop();
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

            if (!m_d3dDeviceDependency)
            {
                return OperationResult::Failure(
                    CoreResultCode::InvalidState,
                    "WindowsDesktopVideoSource",
                    "Start",
                    "D3D device dependency is required");
            }

            OperationResult deviceHealth = m_d3dDeviceDependency->CheckDeviceHealth();
            if (!deviceHealth.IsSuccess())
            {
                return deviceHealth;
            }

            if (m_monitorResolver)
            {
                std::optional<DesktopMonitorInfo> monitor = m_monitorResolver->Resolve(m_config.monitor);
                if (!monitor.has_value())
                {
                    return OperationResult::Failure(
                        CoreResultCode::NotFound,
                        "WindowsDesktopVideoSource",
                        "Start",
                        "Configured monitor target was not found");
                }

                OperationResult regionValidation = ValidateRegion(*monitor);
                if (!regionValidation.IsSuccess())
                {
                    return regionValidation;
                }

                std::lock_guard lock(m_mutex);
                VideoMediaType effectiveMediaType = m_provider->CurrentMediaType();
                if (m_config.region.has_value())
                {
                    effectiveMediaType.width = m_config.region->width;
                    effectiveMediaType.height = m_config.region->height;
                }
                else
                {
                    effectiveMediaType.width = monitor->bounds.width;
                    effectiveMediaType.height = monitor->bounds.height;
                }

                m_resolvedMonitor = std::move(monitor);
                m_effectiveMediaType = effectiveMediaType;
            }

            OperationResult configureResult = m_provider->ConfigureDeviceDependency(m_d3dDeviceDependency);
            if (!configureResult.IsSuccess())
            {
                return configureResult;
            }

            OperationResult startResult = m_provider->Start();
            if (!startResult.IsSuccess())
            {
                m_provider->ReleaseDeviceResources();
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

        [[nodiscard]] std::optional<DesktopMonitorInfo> ResolvedMonitor() const
        {
            std::lock_guard lock(m_mutex);
            return m_resolvedMonitor;
        }

        [[nodiscard]] VideoMediaType EffectiveMediaType() const
        {
            std::lock_guard lock(m_mutex);
            return m_effectiveMediaType.has_value() ? *m_effectiveMediaType : m_provider->CurrentMediaType();
        }

        [[nodiscard]] OperationResult Stop() noexcept override
        {
            CallbackRegistrationToken providerCallback;
            {
                std::lock_guard lock(m_mutex);
                providerCallback = std::move(m_providerCallback);
            }

            providerCallback.reset();
            if (!m_provider)
            {
                return OperationResult::Success();
            }

            OperationResult stopResult = m_provider->Stop();
            m_provider->ReleaseDeviceResources();
            return stopResult;
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
                EffectiveMediaType(),
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

        OperationResult ValidateRegion(const DesktopMonitorInfo& monitor) const
        {
            if (!monitor.bounds.IsValid())
            {
                return OperationResult::Failure(
                    CoreResultCode::ValidationFailure,
                    "WindowsDesktopVideoSource",
                    "Start",
                    "Resolved monitor bounds must have non-zero size");
            }

            if (!m_config.region.has_value())
            {
                return OperationResult::Success();
            }

            const CaptureRectangle& region = *m_config.region;
            if (region.x < 0 || region.y < 0)
            {
                return OperationResult::Failure(
                    CoreResultCode::ValidationFailure,
                    "WindowsDesktopVideoSource",
                    "Start",
                    "Desktop capture region cannot use negative physical-pixel coordinates");
            }

            if (!region.IsValid())
            {
                return OperationResult::Failure(
                    CoreResultCode::ValidationFailure,
                    "WindowsDesktopVideoSource",
                    "Start",
                    "Desktop capture region must have non-zero physical-pixel dimensions");
            }

            const uint64_t regionRight = static_cast<uint64_t>(region.x) + region.width;
            const uint64_t regionBottom = static_cast<uint64_t>(region.y) + region.height;
            if (regionRight > monitor.bounds.width || regionBottom > monitor.bounds.height)
            {
                return OperationResult::Failure(
                    CoreResultCode::ValidationFailure,
                    "WindowsDesktopVideoSource",
                    "Start",
                    "Desktop capture region must fit within resolved monitor bounds");
            }

            return OperationResult::Success();
        }

        DesktopVideoSourceConfig m_config;
        std::shared_ptr<IDesktopCaptureProvider> m_provider;
        std::shared_ptr<IDesktopMonitorResolver> m_monitorResolver;
        std::shared_ptr<IDesktopD3DDeviceDependency> m_d3dDeviceDependency;
        mutable std::mutex m_mutex;
        std::vector<HandlerEntry> m_handlers;
        CallbackRegistrationToken m_providerCallback;
        std::optional<DesktopMonitorInfo> m_resolvedMonitor;
        std::optional<VideoMediaType> m_effectiveMediaType;
        uint64_t m_nextHandlerId{ 1 };
    };
}
