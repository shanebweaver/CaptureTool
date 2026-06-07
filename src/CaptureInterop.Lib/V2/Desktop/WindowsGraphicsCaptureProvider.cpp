#include "pch.h"
#include "WindowsGraphicsCaptureProvider.h"
#include "WindowsGraphicsCaptureHelpers.h"

#include <Windows.h>
#include <mutex>
#include <utility>

using namespace ABI::Windows::Foundation;
using namespace ABI::Windows::Graphics;
using namespace ABI::Windows::Graphics::Capture;
using namespace ABI::Windows::Graphics::DirectX::Direct3D11;
using namespace WindowsGraphicsCaptureHelpers;

namespace CaptureInterop::V2::Desktop
{
    namespace
    {
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
        explicit Impl(DesktopVideoSourceConfig providerConfig)
            : config(std::move(providerConfig)),
              mediaType(CreateDefaultMediaType(config))
        {
            diagnostics.providerName = "WindowsGraphicsCaptureProvider";
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
            mediaType.width = size.Width > 0 ? static_cast<uint32_t>(size.Width) : 0;
            mediaType.height = size.Height > 0 ? static_cast<uint32_t>(size.Height) : 0;
            diagnostics.resourcesActive = true;
            diagnostics.lastNativeStatus.reset();
            diagnostics.lastFailureOperation.clear();
            diagnostics.lastFailureMessage.clear();
            started = true;
            return OperationResult::Success();
        }

        void ReleaseResources() noexcept
        {
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
        bool started{ false };
    };

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
        DesktopCaptureFrameHandler)
    {
        return nullptr;
    }
}
