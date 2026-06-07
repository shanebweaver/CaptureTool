#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Desktop/DesktopD3DDeviceDependency.h"
#include "V2/Desktop/DesktopMonitorResolver.h"
#include "V2/Desktop/FakeDesktopD3DDeviceDependency.h"
#include "V2/Desktop/FakeDesktopCaptureProvider.h"
#include "V2/Desktop/WindowsDesktopVideoSource.h"
#include "V2/Desktop/WindowsGraphicsCaptureProvider.h"

#include <chrono>
#include <condition_variable>
#include <sstream>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;
using namespace CaptureInterop::V2::Desktop;

namespace
{
    class FakeVideoTextureReference final : public IVideoTextureReference
    {
    public:
        explicit FakeVideoTextureReference(std::shared_ptr<std::vector<std::string>> lifecycleEvents)
            : m_lifecycleEvents(std::move(lifecycleEvents))
        {
        }

        ~FakeVideoTextureReference() override
        {
            m_lifecycleEvents->push_back("texture-destroyed");
        }

        [[nodiscard]] ID3D11Texture2D* Texture() const noexcept override
        {
            return nullptr;
        }

    private:
        std::shared_ptr<std::vector<std::string>> m_lifecycleEvents;
    };

    VideoMediaType CreateMediaType()
    {
        return VideoMediaType{
            1280,
            720,
            Rational::From(60, 1),
            VideoPixelFormat::Bgra8,
            ColorPrimaries::Unknown,
            TransferFunction::Unknown,
            ColorRange::Unknown
        };
    }

    DesktopVideoSourceConfig CreateConfig()
    {
        DesktopSourceConfig source;
        source.id = SourceId::FromValue(7);
        source.videoStreamId = StreamId::FromValue(9);
        source.name = "Test monitor";
        source.displayId = "DISPLAY7";
        source.frameRate = Rational::From(60, 1);
        return MapDesktopVideoSourceConfig(source);
    }

    DesktopVideoSourceConfig CreateConfigWithRegion(CaptureRectangle region)
    {
        DesktopSourceConfig source;
        source.id = SourceId::FromValue(7);
        source.videoStreamId = StreamId::FromValue(9);
        source.name = "Test monitor";
        source.displayId = "DISPLAY7";
        source.frameRate = Rational::From(60, 1);
        source.captureArea = region;
        return MapDesktopVideoSourceConfig(source);
    }

    DesktopVideoSourceConfig CreateConfigWithCursorPolicy(CursorCapturePolicy cursorPolicy)
    {
        DesktopSourceConfig source;
        source.id = SourceId::FromValue(7);
        source.videoStreamId = StreamId::FromValue(9);
        source.name = "Test monitor";
        source.displayId = "DISPLAY7";
        source.frameRate = Rational::From(60, 1);
        source.cursorPolicy = cursorPolicy;
        return MapDesktopVideoSourceConfig(source);
    }

    std::shared_ptr<FakeDesktopCaptureProvider> CreateProvider()
    {
        DesktopVideoSourceConfig config = CreateConfig();
        return std::make_shared<FakeDesktopCaptureProvider>(
            config.SourceDescriptor(),
            BuildDesktopVideoStreams(config),
            CreateMediaType());
    }

    std::shared_ptr<FakeDesktopCaptureProvider> CreateProvider(VideoMediaType mediaType)
    {
        DesktopVideoSourceConfig config = CreateConfig();
        return std::make_shared<FakeDesktopCaptureProvider>(
            config.SourceDescriptor(),
            BuildDesktopVideoStreams(config),
            mediaType);
    }

    std::shared_ptr<FakeDesktopD3DDeviceDependency> CreateDependency()
    {
        return std::make_shared<FakeDesktopD3DDeviceDependency>();
    }

    std::shared_ptr<DesktopD3DDeviceDependency> CreateRealD3DDependency()
    {
        D3D_FEATURE_LEVEL featureLevels[] = { D3D_FEATURE_LEVEL_11_0 };
        wil::com_ptr<ID3D11Device> device;
        wil::com_ptr<ID3D11DeviceContext> context;

        HRESULT hr = D3D11CreateDevice(
            nullptr,
            D3D_DRIVER_TYPE_HARDWARE,
            nullptr,
            D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            featureLevels,
            ARRAYSIZE(featureLevels),
            D3D11_SDK_VERSION,
            device.put(),
            nullptr,
            context.put());

        if (FAILED(hr))
        {
            hr = D3D11CreateDevice(
                nullptr,
                D3D_DRIVER_TYPE_WARP,
                nullptr,
                D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                featureLevels,
                ARRAYSIZE(featureLevels),
                D3D11_SDK_VERSION,
                device.put(),
                nullptr,
                context.put());
        }

        Assert::IsTrue(SUCCEEDED(hr), L"Failed to create D3D11 device for crop test.");
        return std::make_shared<DesktopD3DDeviceDependency>(
            std::move(device),
            std::move(context),
            "DesktopSourceCropTestD3DDependency");
    }

    bool IsDesktopSourceProbeEnabled()
    {
        wchar_t value[8]{};
        const DWORD length = GetEnvironmentVariableW(
            L"CAPTURETOOL_V2_DESKTOP_SOURCE_PROBE",
            value,
            ARRAYSIZE(value));
        return length > 0 && value[0] == L'1';
    }

    DesktopMonitorInfo CreatePrimaryMonitorInfo()
    {
        const HMONITOR primaryMonitor = MonitorFromPoint(POINT{ 0, 0 }, MONITOR_DEFAULTTOPRIMARY);
        Assert::IsNotNull(primaryMonitor);

        MONITORINFOEXW monitorInfo{};
        monitorInfo.cbSize = sizeof(monitorInfo);
        Assert::IsTrue(GetMonitorInfoW(primaryMonitor, &monitorInfo) != FALSE);

        DesktopMonitorTarget target;
        target.monitorHandle = reinterpret_cast<uintptr_t>(primaryMonitor);
        target.displayId = "PRIMARY";

        return DesktopMonitorInfo{
            target,
            DesktopMonitorBounds{
                monitorInfo.rcMonitor.left,
                monitorInfo.rcMonitor.top,
                static_cast<uint32_t>(monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left),
                static_cast<uint32_t>(monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top)
            },
            "Primary display"
        };
    }

    CaptureRectangle CenteredProbeRegion(const DesktopMonitorInfo& monitor)
    {
        const uint32_t width = std::min<uint32_t>(monitor.bounds.width, 640);
        const uint32_t height = std::min<uint32_t>(monitor.bounds.height, 360);
        return CaptureRectangle{
            static_cast<int32_t>((monitor.bounds.width - width) / 2),
            static_cast<int32_t>((monitor.bounds.height - height) / 2),
            width,
            height
        };
    }

    std::shared_ptr<FakeDesktopMonitorResolver> CreateResolver(DesktopMonitorInfo monitor)
    {
        auto resolver = std::make_shared<FakeDesktopMonitorResolver>();
        resolver->AddMonitor(std::move(monitor));
        return resolver;
    }

    DesktopVideoSourceConfig CreateProbeConfig(
        const DesktopMonitorInfo& monitor,
        std::optional<CaptureRectangle> region = std::nullopt)
    {
        DesktopSourceConfig source;
        source.id = SourceId::FromValue(region.has_value() ? 31 : 30);
        source.videoStreamId = StreamId::FromValue(region.has_value() ? 41 : 40);
        source.name = region.has_value() ? "Primary monitor region probe" : "Primary monitor full probe";
        source.displayId = monitor.target.displayId;
        source.monitorHandle = monitor.target.monitorHandle;
        source.frameRate = Rational::From(60, 1);
        source.cursorPolicy = CursorCapturePolicy::Included;
        source.captureArea = region;
        return MapDesktopVideoSourceConfig(source);
    }

    std::wstring ToProbeLine(
        const wchar_t* label,
        const DesktopVideoSourceDiagnostics& diagnostics,
        const VideoSample& sample)
    {
        std::wstringstream stream;
        stream
            << L"[V2 Desktop Probe] " << label
            << L" provider=" << std::wstring(diagnostics.providerName.begin(), diagnostics.providerName.end())
            << L" output=" << diagnostics.effectiveOutputDimensions.width << L"x"
            << diagnostics.effectiveOutputDimensions.height
            << L" sample=" << sample.frameDimensions.width << L"x" << sample.frameDimensions.height
            << L" pixelFormat=" << static_cast<int>(sample.mediaType.pixelFormat)
            << L" cursorPolicy=" << static_cast<int>(diagnostics.cursorPolicy)
            << L" colorPrimaries=" << static_cast<int>(diagnostics.color.colorPrimaries)
            << L" transferFunction=" << static_cast<int>(diagnostics.color.transferFunction)
            << L" colorRange=" << static_cast<int>(diagnostics.color.colorRange)
            << L" hdrDetected=" << diagnostics.color.hdrInputDetected
            << L" wideColorDetected=" << diagnostics.color.wideColorInputDetected
            << L" frames=" << diagnostics.framesReceived
            << L" duplicate=" << diagnostics.duplicateFrames
            << L" skipped=" << diagnostics.skippedFrames
            << L" late=" << diagnostics.lateFrames
            << L" sequence=" << sample.sequenceNumber
            << L" timestamp=" << sample.timestamp.ticks100ns;
        return stream.str();
    }

    VideoSample RunDesktopSourceProbe(
        const wchar_t* label,
        DesktopVideoSourceConfig config,
        DesktopMonitorInfo monitor)
    {
        std::shared_ptr<DesktopD3DDeviceDependency> dependency = CreateRealD3DDependency();
        auto provider = std::make_shared<WindowsGraphicsCaptureProvider>(config);
        WindowsDesktopVideoSource source(
            config,
            provider,
            CreateResolver(std::move(monitor)),
            dependency);

        std::mutex sampleMutex;
        std::condition_variable sampleAvailable;
        std::optional<VideoSample> receivedSample;
        CallbackRegistrationToken token = source.RegisterFrameArrivedHandler(
            [&](const VideoSample& sample)
            {
                std::lock_guard lock(sampleMutex);
                receivedSample = sample;
                sampleAvailable.notify_one();
            });

        const OperationResult startResult = source.Start();
        if (!startResult.IsSuccess())
        {
            const DesktopVideoSourceDiagnostics diagnostics = source.Diagnostics();
            std::wstring message = L"[V2 Desktop Probe] activation failed for ";
            message.append(label);
            message.append(L" provider=");
            message.append(diagnostics.providerName.begin(), diagnostics.providerName.end());
            if (startResult.diagnostic.has_value())
            {
                message.append(L" operation=");
                message.append(startResult.diagnostic->operation.begin(), startResult.diagnostic->operation.end());
                if (startResult.diagnostic->nativeStatus.has_value())
                {
                    message.append(L" nativeStatus=");
                    message.append(std::to_wstring(*startResult.diagnostic->nativeStatus));
                }
                message.append(L" message=");
                message.append(startResult.diagnostic->message.begin(), startResult.diagnostic->message.end());
            }
            Logger::WriteMessage(message.c_str());
        }

        Assert::IsTrue(startResult.IsSuccess());

        {
            std::unique_lock lock(sampleMutex);
            Assert::IsTrue(sampleAvailable.wait_for(
                lock,
                std::chrono::seconds(3),
                [&receivedSample]
                {
                    return receivedSample.has_value();
                }));
        }

        const DesktopVideoSourceDiagnostics diagnostics = source.Diagnostics();
        Assert::IsTrue(diagnostics.framesReceived >= 1);
        Assert::IsTrue(receivedSample.has_value());
        Assert::IsTrue(receivedSample->HasTexture());
        Logger::WriteMessage(ToProbeLine(label, diagnostics, *receivedSample).c_str());

        Assert::IsTrue(source.Stop().IsSuccess());
        return *receivedSample;
    }

    wil::com_ptr<ID3D11Texture2D> CreateTexture(
        ID3D11Device* device,
        uint32_t width,
        uint32_t height)
    {
        D3D11_TEXTURE2D_DESC desc{};
        desc.Width = width;
        desc.Height = height;
        desc.MipLevels = 1;
        desc.ArraySize = 1;
        desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
        desc.SampleDesc.Count = 1;
        desc.Usage = D3D11_USAGE_DEFAULT;
        desc.BindFlags = D3D11_BIND_SHADER_RESOURCE | D3D11_BIND_RENDER_TARGET;

        wil::com_ptr<ID3D11Texture2D> texture;
        const HRESULT hr = device->CreateTexture2D(&desc, nullptr, texture.put());
        Assert::IsTrue(SUCCEEDED(hr), L"Failed to create D3D11 texture for crop test.");
        return texture;
    }

    std::shared_ptr<FakeDesktopMonitorResolver> CreateResolverWithConfiguredMonitor()
    {
        auto resolver = std::make_shared<FakeDesktopMonitorResolver>();
        DesktopVideoSourceConfig config = CreateConfig();
        resolver->AddMonitor(DesktopMonitorInfo{
            config.monitor,
            DesktopMonitorBounds{ 0, 0, 2560, 1440 },
            "Primary display"
        });
        return resolver;
    }

    DesktopCaptureFrame CreateFrame(uint64_t sequence = 1)
    {
        return DesktopCaptureFrame{
            SourceId::FromValue(7),
            StreamId::FromValue(9),
            CreateMediaType(),
            MediaTime::FromTicks(456),
            MediaDuration::FromMilliseconds(16),
            sequence,
            { 9, 8, 7, 6 }
        };
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2WindowsDesktopVideoSourceTests)
    {
    public:
        TEST_METHOD(ConstructedWithFakeProvider_ExposesStableDescriptors)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            WindowsDesktopVideoSource source(CreateConfig(), provider);

            const SourceDescriptor firstSource = source.Describe();
            const SourceDescriptor secondSource = source.Describe();
            const std::vector<StreamDescriptor> firstStreams = source.Streams();
            const std::vector<StreamDescriptor> secondStreams = source.Streams();

            Assert::AreEqual(7u, firstSource.id.value);
            Assert::AreEqual(static_cast<int>(SourceKind::Desktop), static_cast<int>(firstSource.kind));
            Assert::AreEqual("Test monitor", firstSource.name.c_str());
            Assert::AreEqual(firstSource.id.value, secondSource.id.value);
            Assert::AreEqual(static_cast<size_t>(1), firstStreams.size());
            Assert::AreEqual(9u, firstStreams[0].id.value);
            Assert::AreEqual(firstStreams[0].id.value, secondStreams[0].id.value);
        }

        TEST_METHOD(Start_DelegatesToProvider)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            std::shared_ptr<FakeDesktopD3DDeviceDependency> dependency = CreateDependency();
            WindowsDesktopVideoSource source(CreateConfig(), provider, nullptr, dependency);

            const OperationResult result = source.Start();

            Assert::IsTrue(result.IsSuccess());
            Assert::IsTrue(provider->DeviceDependency() == dependency);
            Assert::IsTrue(provider->Diagnostics().framesProduced == 0);
        }

        TEST_METHOD(Start_WhileAlreadyStarted_ReturnsInvalidState)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            WindowsDesktopVideoSource source(CreateConfig(), provider, nullptr, CreateDependency());

            Assert::IsTrue(source.Start().IsSuccess());
            const OperationResult result = source.Start();

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual(static_cast<int>(CoreResultCode::InvalidState), static_cast<int>(result.code));
            Assert::AreEqual("WindowsDesktopVideoSource", result.diagnostic->component.c_str());
            Assert::AreEqual("Start", result.diagnostic->operation.c_str());
        }

        TEST_METHOD(Stop_BeforeStart_IsIdempotentSuccess)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            WindowsDesktopVideoSource source(CreateConfig(), provider, nullptr, CreateDependency());

            Assert::IsTrue(source.Stop().IsSuccess());
            Assert::IsTrue(source.Stop().IsSuccess());
        }

        TEST_METHOD(Stop_AfterStop_IsIdempotentSuccess)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            WindowsDesktopVideoSource source(CreateConfig(), provider, nullptr, CreateDependency());

            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(source.Stop().IsSuccess());
            Assert::IsTrue(source.Stop().IsSuccess());
        }

        TEST_METHOD(Start_WithoutD3DDependency_ReturnsInvalidState)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            WindowsDesktopVideoSource source(CreateConfig(), provider);

            const OperationResult result = source.Start();

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual(static_cast<int>(CoreResultCode::InvalidState), static_cast<int>(result.code));
            Assert::AreEqual("WindowsDesktopVideoSource", result.diagnostic->component.c_str());
            Assert::AreEqual("Start", result.diagnostic->operation.c_str());
        }

        TEST_METHOD(Start_WhenD3DDependencyReportsFailure_ReturnsStructuredFailureBeforeProviderStart)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            std::shared_ptr<FakeDesktopD3DDeviceDependency> dependency = CreateDependency();
            dependency->SetHealthFailure(OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "FakeDesktopD3DDeviceDependency",
                "CheckDeviceHealth",
                "D3D device was removed or reset",
                static_cast<int64_t>(0x887A0007)));
            WindowsDesktopVideoSource source(CreateConfig(), provider, nullptr, dependency);

            const OperationResult result = source.Start();

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual(static_cast<int>(CoreResultCode::NativeFailure), static_cast<int>(result.code));
            Assert::AreEqual("CheckDeviceHealth", result.diagnostic->operation.c_str());
            Assert::IsFalse(provider->EmitFrame(CreateFrame()).IsSuccess());
        }

        TEST_METHOD(Start_WhenProviderStartFails_ReleasesResourcesAndDrainsCallback)
        {
            auto lifecycleEvents = std::make_shared<std::vector<std::string>>();
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            provider->SetLifecycleEvents(lifecycleEvents);
            provider->SetStartFailure(OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "FakeDesktopCaptureProvider",
                "Start",
                "Provider start failed",
                -1));
            WindowsDesktopVideoSource source(CreateConfig(), provider, nullptr, CreateDependency());
            uint32_t invocationCount = 0;
            CallbackRegistrationToken token = source.RegisterFrameArrivedHandler(
                [&invocationCount](const VideoSample&)
                {
                    ++invocationCount;
                });

            const OperationResult result = source.Start();

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual(static_cast<int>(CoreResultCode::NativeFailure), static_cast<int>(result.code));
            Assert::AreEqual("Start", result.diagnostic->operation.c_str());
            Assert::IsNull(provider->DeviceDependency().get());
            Assert::AreEqual(static_cast<size_t>(1), lifecycleEvents->size());
            Assert::IsFalse(provider->EmitFrame(CreateFrame()).IsSuccess());
            Assert::AreEqual(0u, invocationCount);
        }

        TEST_METHOD(Stop_WhenProviderStopFails_ReturnsFailureAndReleasesResources)
        {
            auto lifecycleEvents = std::make_shared<std::vector<std::string>>();
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            provider->SetLifecycleEvents(lifecycleEvents);
            provider->SetStopFailure(OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "FakeDesktopCaptureProvider",
                "Stop",
                "Provider stop failed",
                -2));
            WindowsDesktopVideoSource source(CreateConfig(), provider, nullptr, CreateDependency());

            Assert::IsTrue(source.Start().IsSuccess());
            const OperationResult result = source.Stop();

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual(static_cast<int>(CoreResultCode::NativeFailure), static_cast<int>(result.code));
            Assert::AreEqual("FakeDesktopCaptureProvider", result.diagnostic->component.c_str());
            Assert::AreEqual("Stop", result.diagnostic->operation.c_str());
            Assert::IsNull(provider->DeviceDependency().get());
            Assert::AreEqual(static_cast<size_t>(1), lifecycleEvents->size());
        }

        TEST_METHOD(Stop_ReleasesProviderDeviceResourcesBeforeGraphDependencyDestroyed)
        {
            auto lifecycleEvents = std::make_shared<std::vector<std::string>>();
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            provider->SetLifecycleEvents(lifecycleEvents);
            std::shared_ptr<FakeDesktopD3DDeviceDependency> dependency =
                std::make_shared<FakeDesktopD3DDeviceDependency>(lifecycleEvents);

            {
                WindowsDesktopVideoSource source(CreateConfig(), provider, nullptr, dependency);
                Assert::IsTrue(source.Start().IsSuccess());
                Assert::IsTrue(source.Stop().IsSuccess());
            }

            dependency.reset();

            Assert::AreEqual(static_cast<size_t>(2), lifecycleEvents->size());
            Assert::AreEqual("provider-device-resources-released", lifecycleEvents->at(0).c_str());
            Assert::AreEqual("dependency-destroyed", lifecycleEvents->at(1).c_str());
        }

        TEST_METHOD(FakeProviderFrame_IsForwardedAsVideoSample)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            WindowsDesktopVideoSource source(CreateConfig(), provider, nullptr, CreateDependency());
            VideoSample receivedSample;
            CallbackRegistrationToken token = source.RegisterFrameArrivedHandler(
                [&receivedSample](const VideoSample& sample)
                {
                    receivedSample = sample;
                });

            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(provider->EmitFrame(CreateFrame(42)).IsSuccess());

            Assert::AreEqual(7u, receivedSample.sourceId.value);
            Assert::AreEqual(9u, receivedSample.streamId.value);
            Assert::AreEqual(456ll, receivedSample.timestamp.ticks100ns);
            Assert::AreEqual(42ull, receivedSample.sequenceNumber);
            Assert::AreEqual(1280u, receivedSample.mediaType.width);
            Assert::AreEqual(1280u, receivedSample.Dimensions().width);
            Assert::AreEqual(720u, receivedSample.Dimensions().height);
            Assert::AreEqual(static_cast<size_t>(4), receivedSample.pixelData.size());
            Assert::AreEqual(static_cast<uint8_t>(9), receivedSample.pixelData[0]);
        }

        TEST_METHOD(FakeProviderTextureFrame_SampleOwnsTextureReferenceBeyondCallback)
        {
            auto lifecycleEvents = std::make_shared<std::vector<std::string>>();
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            WindowsDesktopVideoSource source(CreateConfig(), provider, nullptr, CreateDependency());
            std::optional<VideoSample> retainedSample;
            CallbackRegistrationToken token = source.RegisterFrameArrivedHandler(
                [&retainedSample](const VideoSample& sample)
                {
                    retainedSample = sample;
                });

            {
                DesktopCaptureFrame frame = CreateFrame(77);
                frame.frameDimensions = VideoFrameDimensions{ 640, 480 };
                frame.texture = std::make_shared<FakeVideoTextureReference>(lifecycleEvents);

                Assert::IsTrue(source.Start().IsSuccess());
                Assert::IsTrue(provider->EmitFrame(frame).IsSuccess());
            }

            Assert::IsTrue(retainedSample.has_value());
            Assert::IsTrue(retainedSample->HasTexture());
            Assert::AreEqual(77ull, retainedSample->sequenceNumber);
            Assert::AreEqual(640u, retainedSample->Dimensions().width);
            Assert::AreEqual(480u, retainedSample->Dimensions().height);
            Assert::AreEqual(static_cast<size_t>(0), lifecycleEvents->size());

            retainedSample.reset();

            Assert::AreEqual(static_cast<size_t>(1), lifecycleEvents->size());
            Assert::AreEqual("texture-destroyed", lifecycleEvents->at(0).c_str());
        }

        TEST_METHOD(TextureRegionFrame_IsCroppedToRegionSizedTextureAndPreservesMetadata)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            std::shared_ptr<FakeDesktopMonitorResolver> resolver = CreateResolverWithConfiguredMonitor();
            std::shared_ptr<DesktopD3DDeviceDependency> dependency = CreateRealD3DDependency();
            std::optional<VideoSample> receivedSample;
            WindowsDesktopVideoSource source(
                CreateConfigWithRegion(CaptureRectangle{ 100, 200, 800, 600 }),
                provider,
                resolver,
                dependency);
            CallbackRegistrationToken token = source.RegisterFrameArrivedHandler(
                [&receivedSample](const VideoSample& sample)
                {
                    receivedSample = sample;
                });

            DesktopCaptureFrame frame = CreateFrame(123);
            frame.texture = std::make_shared<D3D11VideoTextureReference>(
                CreateTexture(dependency->Device(), 2560, 1440));
            frame.frameDimensions = VideoFrameDimensions{ 2560, 1440 };

            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(provider->EmitFrame(frame).IsSuccess());

            Assert::IsTrue(receivedSample.has_value());
            Assert::IsTrue(receivedSample->HasTexture());
            Assert::AreEqual(7u, receivedSample->sourceId.value);
            Assert::AreEqual(9u, receivedSample->streamId.value);
            Assert::AreEqual(123ull, receivedSample->sequenceNumber);
            Assert::AreEqual(456ll, receivedSample->timestamp.ticks100ns);
            Assert::AreEqual(800u, receivedSample->mediaType.width);
            Assert::AreEqual(600u, receivedSample->mediaType.height);
            Assert::AreEqual(800u, receivedSample->frameDimensions.width);
            Assert::AreEqual(600u, receivedSample->frameDimensions.height);

            ID3D11Texture2D* croppedTexture = receivedSample->texture->Texture();
            Assert::IsNotNull(croppedTexture);
            D3D11_TEXTURE2D_DESC croppedDesc{};
            croppedTexture->GetDesc(&croppedDesc);
            Assert::AreEqual(800u, croppedDesc.Width);
            Assert::AreEqual(600u, croppedDesc.Height);
        }

        TEST_METHOD(Diagnostics_IncludeProviderIdentitySourceStreamOutputAndRequestedRegion)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            std::shared_ptr<FakeDesktopMonitorResolver> resolver = CreateResolverWithConfiguredMonitor();
            WindowsDesktopVideoSource source(
                CreateConfigWithRegion(CaptureRectangle{ 100, 200, 800, 600 }),
                provider,
                resolver,
                CreateDependency());

            Assert::IsTrue(source.Start().IsSuccess());

            const DesktopVideoSourceDiagnostics diagnostics = source.Diagnostics();
            Assert::AreEqual("FakeDesktopCaptureProvider", diagnostics.providerName.c_str());
            Assert::AreEqual(7u, diagnostics.sourceId.value);
            Assert::AreEqual(9u, diagnostics.streamId.value);
            Assert::AreEqual(800u, diagnostics.effectiveOutputDimensions.width);
            Assert::AreEqual(600u, diagnostics.effectiveOutputDimensions.height);
            Assert::IsTrue(diagnostics.requestedRegion.has_value());
            Assert::AreEqual(100, diagnostics.requestedRegion->x);
            Assert::AreEqual(200, diagnostics.requestedRegion->y);
            Assert::AreEqual(800u, diagnostics.requestedRegion->width);
            Assert::AreEqual(600u, diagnostics.requestedRegion->height);
        }

        TEST_METHOD(Diagnostics_ReportCursorPolicyWithoutChangingMediaTypeOrTiming)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            WindowsDesktopVideoSource source(
                CreateConfigWithCursorPolicy(CursorCapturePolicy::Excluded),
                provider,
                nullptr,
                CreateDependency());
            VideoSample receivedSample;
            CallbackRegistrationToken token = source.RegisterFrameArrivedHandler(
                [&receivedSample](const VideoSample& sample)
                {
                    receivedSample = sample;
                });

            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(provider->EmitFrame(CreateFrame(33)).IsSuccess());

            const DesktopVideoSourceDiagnostics diagnostics = source.Diagnostics();
            Assert::AreEqual(
                static_cast<int>(CursorCapturePolicy::Excluded),
                static_cast<int>(diagnostics.cursorPolicy));
            Assert::AreEqual(1280u, receivedSample.mediaType.width);
            Assert::AreEqual(720u, receivedSample.mediaType.height);
            Assert::AreEqual(33ull, receivedSample.sequenceNumber);
            Assert::AreEqual(456ll, receivedSample.timestamp.ticks100ns);
        }

        TEST_METHOD(Diagnostics_CountDuplicateSkippedAndLateFrames)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            WindowsDesktopVideoSource source(CreateConfig(), provider, nullptr, CreateDependency());
            CallbackRegistrationToken token = source.RegisterFrameArrivedHandler(
                [](const VideoSample&)
                {
                });

            DesktopCaptureFrame first = CreateFrame(1);
            first.timestamp = MediaTime::FromTicks(1000);
            DesktopCaptureFrame duplicate = CreateFrame(1);
            duplicate.timestamp = MediaTime::FromTicks(1010);
            DesktopCaptureFrame skipped = CreateFrame(4);
            skipped.timestamp = MediaTime::FromTicks(1020);
            DesktopCaptureFrame late = CreateFrame(5);
            late.timestamp = MediaTime::FromTicks(900);

            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(provider->EmitFrame(first).IsSuccess());
            Assert::IsTrue(provider->EmitFrame(duplicate).IsSuccess());
            Assert::IsTrue(provider->EmitFrame(skipped).IsSuccess());
            Assert::IsTrue(provider->EmitFrame(late).IsSuccess());

            const DesktopVideoSourceDiagnostics diagnostics = source.Diagnostics();
            Assert::AreEqual(4ull, diagnostics.framesReceived);
            Assert::AreEqual(1ull, diagnostics.duplicateFrames);
            Assert::AreEqual(2ull, diagnostics.skippedFrames);
            Assert::AreEqual(1ull, diagnostics.lateFrames);
        }

        TEST_METHOD(ProviderFailure_TransitionsSourceToTerminalFailedState)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            WindowsDesktopVideoSource source(CreateConfig(), provider, nullptr, CreateDependency());
            uint32_t invocationCount = 0;
            CallbackRegistrationToken token = source.RegisterFrameArrivedHandler(
                [&invocationCount](const VideoSample&)
                {
                    ++invocationCount;
                });

            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(provider->EmitFrame(CreateFrame(1)).IsSuccess());

            OperationResult failure = OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "FakeDesktopCaptureProvider",
                "MonitorLost",
                "Configured monitor disappeared",
                -7);
            Assert::IsTrue(provider->FailActiveCapture(failure).IsSuccess());

            const DesktopVideoSourceDiagnostics diagnostics = source.Diagnostics();
            Assert::AreEqual(1u, invocationCount);
            Assert::IsTrue(diagnostics.terminalFailure);
            Assert::IsTrue(diagnostics.terminalDiagnostic.has_value());
            Assert::AreEqual(7u, diagnostics.sourceId.value);
            Assert::AreEqual(9u, diagnostics.streamId.value);
            Assert::AreEqual("FakeDesktopCaptureProvider", diagnostics.providerName.c_str());
            Assert::AreEqual("FakeDesktopCaptureProvider", diagnostics.terminalDiagnostic->component.c_str());
            Assert::AreEqual("MonitorLost", diagnostics.terminalDiagnostic->operation.c_str());
            Assert::AreEqual("Configured monitor disappeared", diagnostics.terminalDiagnostic->message.c_str());
            Assert::AreEqual(1ull, diagnostics.providerFailures);
        }

        TEST_METHOD(ProviderFailure_DropsLaterFramesAndBlocksRestart)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            WindowsDesktopVideoSource source(CreateConfig(), provider, nullptr, CreateDependency());
            uint32_t invocationCount = 0;
            CallbackRegistrationToken token = source.RegisterFrameArrivedHandler(
                [&invocationCount](const VideoSample&)
                {
                    ++invocationCount;
                });

            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(provider->FailActiveCapture(OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "FakeDesktopCaptureProvider",
                "FrameArrived",
                "Provider frame callback failed")).IsSuccess());

            Assert::IsFalse(provider->EmitFrame(CreateFrame(2)).IsSuccess());
            const OperationResult restartResult = source.Start();

            Assert::AreEqual(0u, invocationCount);
            Assert::IsFalse(restartResult.IsSuccess());
            Assert::AreEqual(static_cast<int>(CoreResultCode::InvalidState), static_cast<int>(restartResult.code));
            Assert::AreEqual("Start", restartResult.diagnostic->operation.c_str());
            Assert::IsTrue(source.Diagnostics().terminalFailure);
        }

        TEST_METHOD(CallbackTokenDestroyed_PreventsForwardedSample)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            WindowsDesktopVideoSource source(CreateConfig(), provider, nullptr, CreateDependency());
            uint32_t invocationCount = 0;

            {
                CallbackRegistrationToken token = source.RegisterFrameArrivedHandler(
                    [&invocationCount](const VideoSample&)
                    {
                        ++invocationCount;
                    });
            }

            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(provider->EmitFrame(CreateFrame()).IsSuccess());

            Assert::AreEqual(0u, invocationCount);
        }

        TEST_METHOD(Stop_DisconnectsProviderCallback)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            WindowsDesktopVideoSource source(CreateConfig(), provider, nullptr, CreateDependency());
            uint32_t invocationCount = 0;
            CallbackRegistrationToken token = source.RegisterFrameArrivedHandler(
                [&invocationCount](const VideoSample&)
                {
                    ++invocationCount;
                });

            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(source.Stop().IsSuccess());

            Assert::IsFalse(provider->EmitFrame(CreateFrame()).IsSuccess());
            Assert::AreEqual(0u, invocationCount);
        }

        TEST_METHOD(Start_WithMonitorResolver_StoresResolvedPhysicalBounds)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            std::shared_ptr<FakeDesktopMonitorResolver> resolver = CreateResolverWithConfiguredMonitor();
            WindowsDesktopVideoSource source(CreateConfig(), provider, resolver, CreateDependency());

            const OperationResult result = source.Start();
            const std::optional<DesktopMonitorInfo> monitor = source.ResolvedMonitor();

            Assert::IsTrue(result.IsSuccess());
            Assert::IsTrue(monitor.has_value());
            Assert::AreEqual(0, monitor->bounds.x);
            Assert::AreEqual(0, monitor->bounds.y);
            Assert::AreEqual(2560u, monitor->bounds.width);
            Assert::AreEqual(1440u, monitor->bounds.height);
            Assert::AreEqual("Primary display", monitor->displayName.c_str());
        }

        TEST_METHOD(Start_FullMonitor_UsesMonitorBoundsForEffectiveMediaType)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            std::shared_ptr<FakeDesktopMonitorResolver> resolver = CreateResolverWithConfiguredMonitor();
            WindowsDesktopVideoSource source(CreateConfig(), provider, resolver, CreateDependency());

            Assert::IsTrue(source.Start().IsSuccess());

            const VideoMediaType mediaType = source.EffectiveMediaType();
            Assert::AreEqual(2560u, mediaType.width);
            Assert::AreEqual(1440u, mediaType.height);
            Assert::AreEqual(static_cast<int>(VideoPixelFormat::Bgra8), static_cast<int>(mediaType.pixelFormat));
        }

        TEST_METHOD(Start_ValidRegion_UsesRegionDimensionsForEffectiveMediaType)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            std::shared_ptr<FakeDesktopMonitorResolver> resolver = CreateResolverWithConfiguredMonitor();
            VideoSample receivedSample;
            WindowsDesktopVideoSource source(
                CreateConfigWithRegion(CaptureRectangle{ 100, 200, 800, 600 }),
                provider,
                resolver,
                CreateDependency());
            CallbackRegistrationToken token = source.RegisterFrameArrivedHandler(
                [&receivedSample](const VideoSample& sample)
                {
                    receivedSample = sample;
                });

            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(provider->EmitFrame(CreateFrame()).IsSuccess());

            const VideoMediaType mediaType = source.EffectiveMediaType();
            Assert::AreEqual(800u, mediaType.width);
            Assert::AreEqual(600u, mediaType.height);
            Assert::AreEqual(800u, receivedSample.mediaType.width);
            Assert::AreEqual(600u, receivedSample.mediaType.height);
        }

        TEST_METHOD(Start_ValidRegion_PreservesKnownColorMetadata)
        {
            VideoMediaType providerMediaType = CreateMediaType();
            providerMediaType.colorPrimaries = ColorPrimaries::Rec2020;
            providerMediaType.transferFunction = TransferFunction::Hlg;
            providerMediaType.range = ColorRange::Limited;
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider(providerMediaType);
            std::shared_ptr<FakeDesktopMonitorResolver> resolver = CreateResolverWithConfiguredMonitor();
            WindowsDesktopVideoSource source(
                CreateConfigWithRegion(CaptureRectangle{ 100, 200, 800, 600 }),
                provider,
                resolver,
                CreateDependency());

            Assert::IsTrue(source.Start().IsSuccess());

            const VideoMediaType mediaType = source.EffectiveMediaType();
            const DesktopVideoSourceDiagnostics diagnostics = source.Diagnostics();
            Assert::AreEqual(800u, mediaType.width);
            Assert::AreEqual(600u, mediaType.height);
            Assert::AreEqual(static_cast<int>(ColorPrimaries::Rec2020), static_cast<int>(mediaType.colorPrimaries));
            Assert::AreEqual(static_cast<int>(TransferFunction::Hlg), static_cast<int>(mediaType.transferFunction));
            Assert::AreEqual(static_cast<int>(ColorRange::Limited), static_cast<int>(mediaType.range));
            Assert::IsTrue(diagnostics.color.hdrInputDetected);
            Assert::IsTrue(diagnostics.color.wideColorInputDetected);
            Assert::IsTrue(diagnostics.color.hdrToneMappingPending);
        }

        TEST_METHOD(Start_NegativeRegionCoordinates_ReturnsValidationFailure)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            std::shared_ptr<FakeDesktopMonitorResolver> resolver = CreateResolverWithConfiguredMonitor();
            WindowsDesktopVideoSource source(
                CreateConfigWithRegion(CaptureRectangle{ -1, 0, 800, 600 }),
                provider,
                resolver,
                CreateDependency());

            const OperationResult result = source.Start();

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual(static_cast<int>(CoreResultCode::ValidationFailure), static_cast<int>(result.code));
            Assert::AreEqual("Start", result.diagnostic->operation.c_str());
        }

        TEST_METHOD(Start_ZeroRegionDimensions_ReturnsValidationFailure)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            std::shared_ptr<FakeDesktopMonitorResolver> resolver = CreateResolverWithConfiguredMonitor();
            WindowsDesktopVideoSource source(
                CreateConfigWithRegion(CaptureRectangle{ 0, 0, 0, 600 }),
                provider,
                resolver,
                CreateDependency());

            const OperationResult result = source.Start();

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual(static_cast<int>(CoreResultCode::ValidationFailure), static_cast<int>(result.code));
        }

        TEST_METHOD(Start_OutOfBoundsRegion_ReturnsValidationFailure)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            std::shared_ptr<FakeDesktopMonitorResolver> resolver = CreateResolverWithConfiguredMonitor();
            WindowsDesktopVideoSource source(
                CreateConfigWithRegion(CaptureRectangle{ 2000, 1000, 800, 600 }),
                provider,
                resolver,
                CreateDependency());

            const OperationResult result = source.Start();

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual(static_cast<int>(CoreResultCode::ValidationFailure), static_cast<int>(result.code));
        }

        TEST_METHOD(Start_WithMissingMonitor_ReturnsNotFoundBeforeProviderStart)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            auto resolver = std::make_shared<FakeDesktopMonitorResolver>();
            WindowsDesktopVideoSource source(CreateConfig(), provider, resolver, CreateDependency());

            const OperationResult result = source.Start();

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual(static_cast<int>(CoreResultCode::NotFound), static_cast<int>(result.code));
            Assert::AreEqual("WindowsDesktopVideoSource", result.diagnostic->component.c_str());
            Assert::AreEqual("Start", result.diagnostic->operation.c_str());
            Assert::IsFalse(provider->EmitFrame(CreateFrame()).IsSuccess());
        }

        TEST_METHOD(PrimaryMonitorSourceProbe_WhenEnabled_CapturesFullMonitorAndRegion)
        {
            if (!IsDesktopSourceProbeEnabled())
            {
                return;
            }

            const DesktopMonitorInfo monitor = CreatePrimaryMonitorInfo();
            const DesktopVideoSourceConfig fullConfig = CreateProbeConfig(monitor);
            const VideoSample fullSample = RunDesktopSourceProbe(L"full", fullConfig, monitor);

            const CaptureRectangle region = CenteredProbeRegion(monitor);
            const DesktopVideoSourceConfig regionConfig = CreateProbeConfig(monitor, region);
            const VideoSample regionSample = RunDesktopSourceProbe(L"region", regionConfig, monitor);

            Assert::AreEqual(monitor.bounds.width, fullSample.mediaType.width);
            Assert::AreEqual(monitor.bounds.height, fullSample.mediaType.height);
            Assert::AreEqual(region.width, regionSample.mediaType.width);
            Assert::AreEqual(region.height, regionSample.mediaType.height);
            Assert::IsTrue(fullSample.HasTexture());
            Assert::IsTrue(regionSample.HasTexture());
        }
    };
}
