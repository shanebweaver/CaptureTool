#include "pch.h"
#include "WindowsGraphicsCaptureProvider.h"
#include "WindowsGraphicsCaptureHelpers.h"

#include <Windows.h>
#include <algorithm>
#include <functional>
#include <mutex>
#include <vector>
#include <utility>

using namespace ABI::Windows::Foundation;
using namespace ABI::Windows::Graphics;
using namespace ABI::Windows::Graphics::Capture;
using namespace ABI::Windows::Graphics::DirectX::Direct3D11;
using namespace WindowsGraphicsCaptureHelpers;

namespace CaptureInterop::V2::Desktop
{
    struct WindowsGraphicsCaptureProvider::Impl;

    namespace
    {
        class WgcFrameArrivedHandler final
            : public ITypedEventHandler<Direct3D11CaptureFramePool*, IInspectable*>
        {
        public:
            explicit WgcFrameArrivedHandler(WindowsGraphicsCaptureProvider::Impl* owner) noexcept
                : m_owner(owner)
            {
            }

            HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override
            {
                if (!ppvObject)
                {
                    return E_POINTER;
                }

                *ppvObject = nullptr;
                if (riid == __uuidof(IUnknown) ||
                    riid == __uuidof(ITypedEventHandler<Direct3D11CaptureFramePool*, IInspectable*>))
                {
                    *ppvObject = static_cast<ITypedEventHandler<Direct3D11CaptureFramePool*, IInspectable*>*>(this);
                    AddRef();
                    return S_OK;
                }

                return E_NOINTERFACE;
            }

            ULONG STDMETHODCALLTYPE AddRef() override
            {
                return InterlockedIncrement(&m_ref);
            }

            ULONG STDMETHODCALLTYPE Release() override
            {
                const ULONG ref = InterlockedDecrement(&m_ref);
                if (ref == 0)
                {
                    delete this;
                }

                return ref;
            }

            HRESULT STDMETHODCALLTYPE Invoke(IDirect3D11CaptureFramePool* sender, IInspectable*) noexcept override;

        private:
            volatile long m_ref{ 1 };
            WindowsGraphicsCaptureProvider::Impl* m_owner;
        };

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

        VideoMediaType CreateDefaultMediaType(const DesktopVideoSourceConfig& config)
        {
            VideoMediaType mediaType;
            mediaType.width = config.region.has_value() ? config.region->width : 0;
            mediaType.height = config.region.has_value() ? config.region->height : 0;
            mediaType.frameRate = config.requestedFrameRate;
            mediaType.pixelFormat = VideoPixelFormat::Bgra8;
            mediaType.colorPrimaries = ColorPrimaries::Unknown;
            mediaType.transferFunction = TransferFunction::Unknown;
            mediaType.range = ColorRange::Unknown;
            return mediaType;
        }

        OperationResult NativeFailure(std::string operation, std::string message, HRESULT hr)
        {
            return OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "WindowsGraphicsCaptureProvider",
                std::move(operation),
                std::move(message),
                static_cast<int64_t>(hr));
        }

        void CloseCaptureSession(wil::com_ptr<IGraphicsCaptureSession>& session) noexcept
        {
            if (!session)
            {
                return;
            }

            wil::com_ptr<IClosable> closable;
            if (SUCCEEDED(session->QueryInterface(IID_PPV_ARGS(closable.put()))))
            {
                (void)closable->Close();
            }

            session.reset();
        }
    }

    struct WindowsGraphicsCaptureProvider::Impl
    {
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

        explicit Impl(DesktopVideoSourceConfig providerConfig)
            : config(std::move(providerConfig)),
              mediaType(CreateDefaultMediaType(config))
        {
            diagnostics.providerName = "WindowsGraphicsCaptureProvider";
            diagnostics.cursorPolicy = config.cursorPolicy;
            diagnostics.color = BuildDesktopColorDiagnostics(mediaType);
        }

        OperationResult Activate()
        {
            ++diagnostics.activationAttempts;

            if (!deviceDependency)
            {
                OperationResult result = OperationResult::Failure(
                    CoreResultCode::InvalidState,
                    "WindowsGraphicsCaptureProvider",
                    "Start",
                    "D3D device dependency must be configured before activation");
                RecordFailure(result);
                return result;
            }

            OperationResult health = deviceDependency->CheckDeviceHealth();
            if (!health.IsSuccess())
            {
                RecordFailure(health);
                return health;
            }

            if (config.monitor.monitorHandle == 0)
            {
                OperationResult result = OperationResult::Failure(
                    CoreResultCode::ValidationFailure,
                    "WindowsGraphicsCaptureProvider",
                    "Start",
                    "Monitor handle is required for Windows Graphics Capture activation");
                RecordFailure(result);
                return result;
            }

            HRESULT hr = S_OK;
            wil::com_ptr<IGraphicsCaptureItemInterop> interop = GetGraphicsCaptureItemInterop(&hr);
            if (!interop)
            {
                OperationResult result = NativeFailure(
                    "GetGraphicsCaptureItemInterop",
                    "Failed to resolve Windows Graphics Capture interop factory",
                    hr);
                RecordFailure(result);
                return result;
            }

            wil::com_ptr<IGraphicsCaptureItem> nextCaptureItem =
                GetGraphicsCaptureItemForMonitor(
                    reinterpret_cast<HMONITOR>(config.monitor.monitorHandle),
                    interop,
                    &hr);
            if (!nextCaptureItem)
            {
                OperationResult result = NativeFailure(
                    "CreateForMonitor",
                    "Failed to create Windows Graphics Capture item for selected monitor",
                    hr);
                RecordFailure(result);
                return result;
            }

            wil::com_ptr<ID3D11Device> graphDevice;
            ID3D11Device* rawGraphDevice = deviceDependency->Device();
            if (!rawGraphDevice)
            {
                OperationResult result = OperationResult::Failure(
                    CoreResultCode::InvalidState,
                    "WindowsGraphicsCaptureProvider",
                    "Start",
                    "D3D device dependency returned a null device");
                RecordFailure(result);
                return result;
            }

            rawGraphDevice->AddRef();
            graphDevice.attach(rawGraphDevice);
            wil::com_ptr<IDirect3DDevice> direct3DDevice = CreateDirect3DDevice(graphDevice, &hr);
            if (!direct3DDevice)
            {
                OperationResult result = NativeFailure(
                    "CreateDirect3DDevice",
                    "Failed to create WinRT Direct3D device for Windows Graphics Capture",
                    hr);
                RecordFailure(result);
                return result;
            }

            wil::com_ptr<IDirect3D11CaptureFramePool> nextFramePool =
                CreateCaptureFramePool(nextCaptureItem, direct3DDevice, &hr);
            if (!nextFramePool)
            {
                OperationResult result = NativeFailure(
                    "CreateCaptureFramePool",
                    "Failed to create Windows Graphics Capture frame pool",
                    hr);
                RecordFailure(result);
                return result;
            }

            wil::com_ptr<IGraphicsCaptureSession> nextCaptureSession =
                CreateCaptureSession(nextFramePool, nextCaptureItem, &hr);
            if (!nextCaptureSession)
            {
                OperationResult result = NativeFailure(
                    "CreateCaptureSession",
                    "Failed to create Windows Graphics Capture session",
                    hr);
                RecordFailure(result);
                return result;
            }

            wil::com_ptr<IGraphicsCaptureSession2> captureSession2;
            if (SUCCEEDED(nextCaptureSession->QueryInterface(IID_PPV_ARGS(captureSession2.put()))))
            {
                hr = captureSession2->put_IsCursorCaptureEnabled(
                    config.cursorPolicy == CursorCapturePolicy::Included);
                if (FAILED(hr))
                {
                    OperationResult result = NativeFailure(
                        "SetCursorCapturePolicy",
                        "Failed to apply Windows Graphics Capture cursor policy",
                        hr);
                    RecordFailure(result);
                    return result;
                }
            }

            wil::com_ptr<WgcFrameArrivedHandler> nextFrameArrivedHandler;
            nextFrameArrivedHandler.attach(new WgcFrameArrivedHandler(this));
            EventRegistrationToken nextFrameArrivedToken{};
            hr = nextFramePool->add_FrameArrived(nextFrameArrivedHandler.get(), &nextFrameArrivedToken);
            if (FAILED(hr))
            {
                OperationResult result = NativeFailure(
                    "RegisterFrameArrived",
                    "Failed to register Windows Graphics Capture frame callback",
                    hr);
                RecordFailure(result);
                return result;
            }

            hr = nextCaptureSession->StartCapture();
            if (FAILED(hr))
            {
                (void)nextFramePool->remove_FrameArrived(nextFrameArrivedToken);
                OperationResult result = NativeFailure(
                    "StartCapture",
                    "Failed to start Windows Graphics Capture session",
                    hr);
                RecordFailure(result);
                return result;
            }

            SizeInt32 size{};
            hr = nextCaptureItem->get_Size(&size);
            if (FAILED(hr))
            {
                OperationResult result = NativeFailure(
                    "ReadCaptureItemSize",
                    "Failed to read selected monitor capture size",
                    hr);
                RecordFailure(result);
                return result;
            }

            captureItem = std::move(nextCaptureItem);
            framePool = std::move(nextFramePool);
            captureSession = std::move(nextCaptureSession);
            frameArrivedHandler = std::move(nextFrameArrivedHandler);
            frameArrivedToken = nextFrameArrivedToken;
            frameArrivedRegistered = true;
            mediaType.width = size.Width > 0 ? static_cast<uint32_t>(size.Width) : 0;
            mediaType.height = size.Height > 0 ? static_cast<uint32_t>(size.Height) : 0;
            diagnostics.resourcesActive = true;
            diagnostics.color = BuildDesktopColorDiagnostics(mediaType);
            diagnostics.lastNativeStatus.reset();
            diagnostics.lastFailureOperation.clear();
            diagnostics.lastFailureMessage.clear();
            started = true;
            return OperationResult::Success();
        }

        void ReleaseResources() noexcept
        {
            if (framePool && frameArrivedRegistered)
            {
                (void)framePool->remove_FrameArrived(frameArrivedToken);
            }

            frameArrivedRegistered = false;
            frameArrivedToken = {};
            frameArrivedHandler.reset();
            CloseCaptureSession(captureSession);
            framePool.reset();
            captureItem.reset();
            diagnostics.resourcesActive = false;
            started = false;
        }

        void RecordFailure(const OperationResult& result)
        {
            ++diagnostics.providerFailures;
            ++diagnostics.activationFailures;
            diagnostics.resourcesActive = false;

            if (result.diagnostic.has_value())
            {
                diagnostics.lastNativeStatus = result.diagnostic->nativeStatus;
                diagnostics.lastFailureOperation = result.diagnostic->operation;
                diagnostics.lastFailureMessage = result.diagnostic->message;
            }
        }

        HRESULT OnFrameArrived(IDirect3D11CaptureFramePool* sender) noexcept
        {
            if (!sender)
            {
                RecordFrameFailure("FrameArrived", E_POINTER, "Frame-arrived callback did not provide a frame pool");
                return E_POINTER;
            }

            wil::com_ptr<IDirect3D11CaptureFrame> frame;
            HRESULT hr = sender->TryGetNextFrame(frame.put());
            if (FAILED(hr) || !frame)
            {
                RecordFrameFailure("TryGetNextFrame", hr, "Failed to read next Windows Graphics Capture frame");
                return hr;
            }

            wil::com_ptr<IDirect3DSurface> surface;
            hr = frame->get_Surface(surface.put());
            if (FAILED(hr) || !surface)
            {
                RecordFrameFailure("ReadFrameSurface", hr, "Failed to read Windows Graphics Capture frame surface");
                return hr;
            }

            wil::com_ptr<Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess> access;
            hr = surface->QueryInterface(IID_PPV_ARGS(access.put()));
            if (FAILED(hr) || !access)
            {
                RecordFrameFailure("QueryDxgiInterfaceAccess", hr, "Failed to access DXGI texture from frame surface");
                return hr;
            }

            wil::com_ptr<ID3D11Texture2D> texture;
            hr = access->GetInterface(IID_PPV_ARGS(texture.put()));
            if (FAILED(hr) || !texture)
            {
                RecordFrameFailure("ReadFrameTexture", hr, "Failed to read D3D11 texture from frame surface");
                return hr;
            }

            SizeInt32 contentSize{};
            hr = frame->get_ContentSize(&contentSize);
            if (FAILED(hr))
            {
                RecordFrameFailure("ReadContentSize", hr, "Failed to read Windows Graphics Capture frame dimensions");
                return hr;
            }

            TimeSpan systemRelativeTime{};
            hr = frame->get_SystemRelativeTime(&systemRelativeTime);
            if (FAILED(hr))
            {
                RecordFrameFailure("ReadSystemRelativeTime", hr, "Failed to read Windows Graphics Capture frame timestamp");
                return hr;
            }

            std::vector<DesktopCaptureFrameHandler> handlersToInvoke;
            DesktopCaptureFrame desktopFrame;
            {
                std::lock_guard lock(mutex);
                if (!started)
                {
                    return S_OK;
                }

                ++diagnostics.framesProduced;
                desktopFrame.sourceId = config.sourceId;
                desktopFrame.streamId = config.streamId;
                desktopFrame.mediaType = mediaType;
                desktopFrame.timestamp = MediaTime::FromTicks(systemRelativeTime.Duration);
                desktopFrame.duration = MediaDuration{};
                desktopFrame.sequenceNumber = nextSequenceNumber++;
                desktopFrame.frameDimensions = VideoFrameDimensions{
                    contentSize.Width > 0 ? static_cast<uint32_t>(contentSize.Width) : mediaType.width,
                    contentSize.Height > 0 ? static_cast<uint32_t>(contentSize.Height) : mediaType.height
                };
                desktopFrame.texture = std::make_shared<D3D11VideoTextureReference>(std::move(texture));

                handlersToInvoke.reserve(handlers.size());
                for (const HandlerEntry& entry : handlers)
                {
                    handlersToInvoke.push_back(entry.handler);
                }
            }

            for (const DesktopCaptureFrameHandler& handler : handlersToInvoke)
            {
                handler(desktopFrame);
            }

            return S_OK;
        }

        void RecordFrameFailure(
            std::string operation,
            HRESULT hr,
            std::string message)
        {
            OperationResult failure = NativeFailure(operation, message, hr);
            std::vector<DesktopCaptureProviderFailureHandler> handlersToInvoke;
            {
                std::lock_guard lock(mutex);
                ++diagnostics.providerFailures;
                diagnostics.resourcesActive = false;
                diagnostics.lastNativeStatus = static_cast<int64_t>(hr);
                diagnostics.lastFailureOperation = std::move(operation);
                diagnostics.lastFailureMessage = std::move(message);
                if (started)
                {
                    started = false;
                    handlersToInvoke.reserve(failureHandlers.size());
                    for (const FailureHandlerEntry& entry : failureHandlers)
                    {
                        handlersToInvoke.push_back(entry.handler);
                    }
                }
            }

            for (const DesktopCaptureProviderFailureHandler& handler : handlersToInvoke)
            {
                handler(failure);
            }
        }

        void Unregister(uint64_t id)
        {
            std::lock_guard lock(mutex);
            handlers.erase(
                std::remove_if(
                    handlers.begin(),
                    handlers.end(),
                    [id](const HandlerEntry& entry)
                    {
                        return entry.id == id;
                    }),
                handlers.end());
        }

        void UnregisterFailureHandler(uint64_t id)
        {
            std::lock_guard lock(mutex);
            failureHandlers.erase(
                std::remove_if(
                    failureHandlers.begin(),
                    failureHandlers.end(),
                    [id](const FailureHandlerEntry& entry)
                    {
                        return entry.id == id;
                    }),
                failureHandlers.end());
        }

        DesktopVideoSourceConfig config;
        SourceDescriptor source;
        std::vector<StreamDescriptor> streams;
        VideoMediaType mediaType;
        mutable std::mutex mutex;
        DesktopCaptureProviderDiagnostics diagnostics;
        std::shared_ptr<IDesktopD3DDeviceDependency> deviceDependency;
        wil::com_ptr<IGraphicsCaptureItem> captureItem;
        wil::com_ptr<IDirect3D11CaptureFramePool> framePool;
        wil::com_ptr<IGraphicsCaptureSession> captureSession;
        wil::com_ptr<WgcFrameArrivedHandler> frameArrivedHandler;
        EventRegistrationToken frameArrivedToken{};
        std::vector<HandlerEntry> handlers;
        std::vector<FailureHandlerEntry> failureHandlers;
        uint64_t nextHandlerId{ 1 };
        uint64_t nextFailureHandlerId{ 1 };
        uint64_t nextSequenceNumber{ 1 };
        bool started{ false };
        bool frameArrivedRegistered{ false };
    };

    HRESULT STDMETHODCALLTYPE WgcFrameArrivedHandler::Invoke(
        IDirect3D11CaptureFramePool* sender,
        IInspectable*) noexcept
    {
        return m_owner ? m_owner->OnFrameArrived(sender) : E_POINTER;
    }

    WindowsGraphicsCaptureProvider::WindowsGraphicsCaptureProvider(DesktopVideoSourceConfig config)
        : m_impl(std::make_unique<Impl>(std::move(config)))
    {
        m_impl->source = m_impl->config.SourceDescriptor();
        m_impl->streams = BuildDesktopVideoStreams(m_impl->config);
    }

    WindowsGraphicsCaptureProvider::~WindowsGraphicsCaptureProvider()
    {
        ReleaseDeviceResources();
    }

    std::string WindowsGraphicsCaptureProvider::ProviderName() const
    {
        return "WindowsGraphicsCaptureProvider";
    }

    SourceDescriptor WindowsGraphicsCaptureProvider::DescribeSource() const
    {
        return m_impl->source;
    }

    std::vector<StreamDescriptor> WindowsGraphicsCaptureProvider::DescribeStreams() const
    {
        return m_impl->streams;
    }

    VideoMediaType WindowsGraphicsCaptureProvider::CurrentMediaType() const
    {
        std::lock_guard lock(m_impl->mutex);
        return m_impl->mediaType;
    }

    DesktopCaptureProviderDiagnostics WindowsGraphicsCaptureProvider::Diagnostics() const
    {
        std::lock_guard lock(m_impl->mutex);
        return m_impl->diagnostics;
    }

    std::shared_ptr<IDesktopD3DDeviceDependency> WindowsGraphicsCaptureProvider::DeviceDependency() const
    {
        std::lock_guard lock(m_impl->mutex);
        return m_impl->deviceDependency;
    }

    OperationResult WindowsGraphicsCaptureProvider::ConfigureDeviceDependency(
        std::shared_ptr<IDesktopD3DDeviceDependency> dependency) noexcept
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

        std::lock_guard lock(m_impl->mutex);
        m_impl->deviceDependency = std::move(dependency);
        return OperationResult::Success();
    }

    void WindowsGraphicsCaptureProvider::ReleaseDeviceResources() noexcept
    {
        std::lock_guard lock(m_impl->mutex);
        m_impl->ReleaseResources();
    }

    OperationResult WindowsGraphicsCaptureProvider::Start() noexcept
    {
        std::lock_guard lock(m_impl->mutex);
        if (m_impl->started)
        {
            return OperationResult::Failure(
                CoreResultCode::InvalidState,
                ProviderName(),
                "Start",
                "Provider is already started");
        }

        return m_impl->Activate();
    }

    OperationResult WindowsGraphicsCaptureProvider::Stop() noexcept
    {
        ReleaseDeviceResources();
        return OperationResult::Success();
    }

    CallbackRegistrationToken WindowsGraphicsCaptureProvider::RegisterFrameArrivedHandler(
        DesktopCaptureFrameHandler handler)
    {
        if (!handler)
        {
            return nullptr;
        }

        uint64_t id = 0;
        {
            std::lock_guard lock(m_impl->mutex);
            id = m_impl->nextHandlerId++;
            m_impl->handlers.push_back(Impl::HandlerEntry{ id, std::move(handler) });
        }

        return std::make_unique<CallbackToken>(
            [impl = m_impl.get(), id]
            {
                impl->Unregister(id);
            });
    }

    CallbackRegistrationToken WindowsGraphicsCaptureProvider::RegisterProviderFailedHandler(
        DesktopCaptureProviderFailureHandler handler)
    {
        if (!handler)
        {
            return nullptr;
        }

        uint64_t id = 0;
        {
            std::lock_guard lock(m_impl->mutex);
            id = m_impl->nextFailureHandlerId++;
            m_impl->failureHandlers.push_back(Impl::FailureHandlerEntry{ id, std::move(handler) });
        }

        return std::make_unique<CallbackToken>(
            [impl = m_impl.get(), id]
            {
                impl->UnregisterFailureHandler(id);
            });
    }
}
