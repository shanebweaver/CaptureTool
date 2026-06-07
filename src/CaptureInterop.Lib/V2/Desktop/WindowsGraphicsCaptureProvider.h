#pragma once

#include "DesktopVideoSourceConfig.h"
#include "IDesktopCaptureProvider.h"

#include <memory>

namespace CaptureInterop::V2::Desktop
{
    class WindowsGraphicsCaptureProvider final : public IDesktopCaptureProvider
    {
    public:
        struct Impl;

        explicit WindowsGraphicsCaptureProvider(DesktopVideoSourceConfig config);
        ~WindowsGraphicsCaptureProvider() override;

        WindowsGraphicsCaptureProvider(const WindowsGraphicsCaptureProvider&) = delete;
        WindowsGraphicsCaptureProvider& operator=(const WindowsGraphicsCaptureProvider&) = delete;
        WindowsGraphicsCaptureProvider(WindowsGraphicsCaptureProvider&&) = delete;
        WindowsGraphicsCaptureProvider& operator=(WindowsGraphicsCaptureProvider&&) = delete;

        [[nodiscard]] std::string ProviderName() const override;
        [[nodiscard]] SourceDescriptor DescribeSource() const override;
        [[nodiscard]] std::vector<StreamDescriptor> DescribeStreams() const override;
        [[nodiscard]] VideoMediaType CurrentMediaType() const override;
        [[nodiscard]] DesktopCaptureProviderDiagnostics Diagnostics() const override;
        [[nodiscard]] std::shared_ptr<IDesktopD3DDeviceDependency> DeviceDependency() const override;

        [[nodiscard]] OperationResult ConfigureDeviceDependency(
            std::shared_ptr<IDesktopD3DDeviceDependency> dependency) noexcept override;
        void ReleaseDeviceResources() noexcept override;
        [[nodiscard]] OperationResult Start() noexcept override;
        [[nodiscard]] OperationResult Stop() noexcept override;

        [[nodiscard]] CallbackRegistrationToken RegisterFrameArrivedHandler(
            DesktopCaptureFrameHandler handler) override;
        [[nodiscard]] CallbackRegistrationToken RegisterProviderFailedHandler(
            DesktopCaptureProviderFailureHandler handler) override;
    private:
        std::unique_ptr<Impl> m_impl;
    };
}
