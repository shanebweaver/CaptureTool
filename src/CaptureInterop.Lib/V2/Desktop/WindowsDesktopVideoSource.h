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
    struct DesktopVideoSourceDiagnostics
    {
        std::string providerName;
        SourceId sourceId;
        StreamId streamId;
        VideoFrameDimensions effectiveOutputDimensions;
        std::optional<CaptureRectangle> requestedRegion;
        CursorCapturePolicy cursorPolicy{ CursorCapturePolicy::Included };
        uint64_t framesReceived{ 0 };
        uint64_t duplicateFrames{ 0 };
        uint64_t lateFrames{ 0 };
        uint64_t skippedFrames{ 0 };
        uint64_t providerFailures{ 0 };
    };

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

            {
                std::lock_guard lock(m_mutex);
                if (m_started)
                {
                    return OperationResult::Failure(
                        CoreResultCode::InvalidState,
                        "WindowsDesktopVideoSource",
                        "Start",
                        "Desktop video source is already started");
                }
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

            {
                std::lock_guard lock(m_mutex);
                if (!m_providerCallback)
                {
                    m_providerCallback = m_provider->RegisterFrameArrivedHandler(
                        [this](const DesktopCaptureFrame& frame)
                        {
                            ForwardFrame(frame);
                        });
                }
            }

            OperationResult startResult = m_provider->Start();
            if (!startResult.IsSuccess())
            {
                CallbackRegistrationToken providerCallback;
                {
                    std::lock_guard lock(m_mutex);
                    providerCallback = std::move(m_providerCallback);
                }

                providerCallback.reset();
                m_provider->ReleaseDeviceResources();
                return startResult;
            }

            std::lock_guard lock(m_mutex);
            m_started = true;
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

        [[nodiscard]] DesktopVideoSourceDiagnostics Diagnostics() const
        {
            std::lock_guard lock(m_mutex);
            DesktopVideoSourceDiagnostics diagnostics = m_diagnostics;
            diagnostics.sourceId = m_config.sourceId;
            diagnostics.streamId = m_config.streamId;
            diagnostics.requestedRegion = m_config.region;
            diagnostics.cursorPolicy = m_config.cursorPolicy;

            VideoMediaType mediaType = m_effectiveMediaType.has_value()
                ? *m_effectiveMediaType
                : (m_provider ? m_provider->CurrentMediaType() : VideoMediaType{});
            diagnostics.effectiveOutputDimensions = VideoFrameDimensions::FromMediaType(mediaType);

            if (m_provider)
            {
                const DesktopCaptureProviderDiagnostics providerDiagnostics = m_provider->Diagnostics();
                diagnostics.providerName = providerDiagnostics.providerName;
                diagnostics.providerFailures = providerDiagnostics.providerFailures;
            }

            return diagnostics;
        }

        [[nodiscard]] OperationResult Stop() noexcept override
        {
            CallbackRegistrationToken providerCallback;
            {
                std::lock_guard lock(m_mutex);
                if (!m_started && !m_providerCallback)
                {
                    return OperationResult::Success();
                }

                m_started = false;
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
                if (!m_started)
                {
                    return;
                }

                handlers.reserve(m_handlers.size());
                for (const HandlerEntry& entry : m_handlers)
                {
                    handlers.push_back(entry.handler);
                }

                UpdateTimingDiagnostics(frame);
            }

            VideoSample sample{
                frame.sourceId,
                frame.streamId,
                frame.timestamp,
                frame.duration,
                EffectiveMediaType(),
                frame.placeholderPixels,
                frame.sequenceNumber,
                ResolveFrameDimensions(frame),
                ResolveTextureReference(frame)
            };

            for (const VideoSampleHandler& handler : handlers)
            {
                handler(sample);
            }
        }

        void UpdateTimingDiagnostics(const DesktopCaptureFrame& frame)
        {
            ++m_diagnostics.framesReceived;

            if (m_lastSequenceNumber.has_value())
            {
                if (frame.sequenceNumber == *m_lastSequenceNumber)
                {
                    ++m_diagnostics.duplicateFrames;
                }
                else if (frame.sequenceNumber > *m_lastSequenceNumber + 1)
                {
                    m_diagnostics.skippedFrames += frame.sequenceNumber - *m_lastSequenceNumber - 1;
                }
            }

            if (m_lastTimestamp.has_value() && frame.timestamp.ticks100ns < m_lastTimestamp->ticks100ns)
            {
                ++m_diagnostics.lateFrames;
            }

            m_lastSequenceNumber = frame.sequenceNumber;
            m_lastTimestamp = frame.timestamp;
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

        VideoFrameDimensions ResolveFrameDimensions(const DesktopCaptureFrame& frame) const
        {
            if (m_config.region.has_value())
            {
                return VideoFrameDimensions{ m_config.region->width, m_config.region->height };
            }

            if (frame.frameDimensions.IsValid())
            {
                return frame.frameDimensions;
            }

            const VideoMediaType mediaType = EffectiveMediaType();
            return VideoFrameDimensions::FromMediaType(mediaType);
        }

        std::shared_ptr<IVideoTextureReference> ResolveTextureReference(const DesktopCaptureFrame& frame) const
        {
            if (!frame.texture || !m_config.region.has_value())
            {
                return frame.texture;
            }

            if (!m_d3dDeviceDependency)
            {
                return nullptr;
            }

            ID3D11Device* device = m_d3dDeviceDependency->Device();
            ID3D11DeviceContext* context = m_d3dDeviceDependency->ImmediateContext();
            ID3D11Texture2D* sourceTexture = frame.texture->Texture();
            if (!device || !context || !sourceTexture)
            {
                return nullptr;
            }

            const CaptureRectangle& region = *m_config.region;
            D3D11_TEXTURE2D_DESC desc{};
            sourceTexture->GetDesc(&desc);
            desc.Width = region.width;
            desc.Height = region.height;
            desc.MiscFlags = 0;

            wil::com_ptr<ID3D11Texture2D> croppedTexture;
            const HRESULT createResult = device->CreateTexture2D(&desc, nullptr, croppedTexture.put());
            if (FAILED(createResult) || !croppedTexture)
            {
                return nullptr;
            }

            const D3D11_BOX sourceBox{
                static_cast<UINT>(region.x),
                static_cast<UINT>(region.y),
                0,
                static_cast<UINT>(region.x + static_cast<int32_t>(region.width)),
                static_cast<UINT>(region.y + static_cast<int32_t>(region.height)),
                1
            };
            context->CopySubresourceRegion(
                croppedTexture.get(),
                0,
                0,
                0,
                0,
                sourceTexture,
                0,
                &sourceBox);

            return std::make_shared<D3D11VideoTextureReference>(std::move(croppedTexture));
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
        DesktopVideoSourceDiagnostics m_diagnostics;
        std::optional<uint64_t> m_lastSequenceNumber;
        std::optional<MediaTime> m_lastTimestamp;
        uint64_t m_nextHandlerId{ 1 };
        bool m_started{ false };
    };
}
