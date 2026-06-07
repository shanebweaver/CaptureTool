#pragma once

#include "DesktopD3DDeviceDependency.h"
#include "V2/Core/MediaSamples.h"
#include "V2/Core/PipelineInterfaces.h"
#include "V2/Core/ResultTypes.h"

#include <cstdint>
#include <functional>
#include <memory>
#include <string>
#include <vector>

namespace CaptureInterop::V2::Desktop
{
    struct DesktopCaptureProviderDiagnostics
    {
        std::string providerName;
        uint64_t framesProduced{ 0 };
        uint64_t providerFailures{ 0 };
    };

    struct DesktopCaptureFrame
    {
        SourceId sourceId;
        StreamId streamId;
        VideoMediaType mediaType;
        MediaTime timestamp;
        MediaDuration duration;
        uint64_t sequenceNumber{ 0 };
        std::vector<uint8_t> placeholderPixels;
        VideoFrameDimensions frameDimensions;
        std::shared_ptr<IVideoTextureReference> texture;
    };

    using DesktopCaptureFrameHandler = std::function<void(const DesktopCaptureFrame&)>;

    class IDesktopCaptureProvider
    {
    public:
        virtual ~IDesktopCaptureProvider() = default;

        [[nodiscard]] virtual std::string ProviderName() const = 0;
        [[nodiscard]] virtual SourceDescriptor DescribeSource() const = 0;
        [[nodiscard]] virtual std::vector<StreamDescriptor> DescribeStreams() const = 0;
        [[nodiscard]] virtual VideoMediaType CurrentMediaType() const = 0;
        [[nodiscard]] virtual DesktopCaptureProviderDiagnostics Diagnostics() const = 0;
        [[nodiscard]] virtual std::shared_ptr<IDesktopD3DDeviceDependency> DeviceDependency() const = 0;

        [[nodiscard]] virtual OperationResult ConfigureDeviceDependency(
            std::shared_ptr<IDesktopD3DDeviceDependency> dependency) noexcept = 0;

        // Called during source stop and failed-start cleanup before the graph-owned
        // D3D dependency is released. Providers must drop API-specific capture
        // objects and texture references here.
        virtual void ReleaseDeviceResources() noexcept = 0;
        [[nodiscard]] virtual OperationResult Start() noexcept = 0;
        [[nodiscard]] virtual OperationResult Stop() noexcept = 0;

        // Destroying the returned token synchronously unregisters the frame handler.
        // Providers must copy handlers before invocation so callbacks can safely
        // register or unregister without re-entering provider-state locks.
        [[nodiscard]] virtual CallbackRegistrationToken RegisterFrameArrivedHandler(
            DesktopCaptureFrameHandler handler) = 0;
    };
}
