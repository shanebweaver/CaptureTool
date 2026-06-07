#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Desktop/DesktopMonitorResolver.h"
#include "V2/Desktop/FakeDesktopCaptureProvider.h"
#include "V2/Desktop/WindowsDesktopVideoSource.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;
using namespace CaptureInterop::V2::Desktop;

namespace
{
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

    std::shared_ptr<FakeDesktopCaptureProvider> CreateProvider()
    {
        DesktopVideoSourceConfig config = CreateConfig();
        return std::make_shared<FakeDesktopCaptureProvider>(
            config.SourceDescriptor(),
            BuildDesktopVideoStreams(config),
            CreateMediaType());
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
            WindowsDesktopVideoSource source(CreateConfig(), provider);

            const OperationResult result = source.Start();

            Assert::IsTrue(result.IsSuccess());
            Assert::IsTrue(provider->Diagnostics().framesProduced == 0);
        }

        TEST_METHOD(FakeProviderFrame_IsForwardedAsVideoSample)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            WindowsDesktopVideoSource source(CreateConfig(), provider);
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
            Assert::AreEqual(1280u, receivedSample.mediaType.width);
            Assert::AreEqual(static_cast<size_t>(4), receivedSample.pixelData.size());
            Assert::AreEqual(static_cast<uint8_t>(9), receivedSample.pixelData[0]);
        }

        TEST_METHOD(CallbackTokenDestroyed_PreventsForwardedSample)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            WindowsDesktopVideoSource source(CreateConfig(), provider);
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
            WindowsDesktopVideoSource source(CreateConfig(), provider);
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
            WindowsDesktopVideoSource source(CreateConfig(), provider, resolver);

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
            WindowsDesktopVideoSource source(CreateConfig(), provider, resolver);

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
                resolver);
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

        TEST_METHOD(Start_NegativeRegionCoordinates_ReturnsValidationFailure)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            std::shared_ptr<FakeDesktopMonitorResolver> resolver = CreateResolverWithConfiguredMonitor();
            WindowsDesktopVideoSource source(
                CreateConfigWithRegion(CaptureRectangle{ -1, 0, 800, 600 }),
                provider,
                resolver);

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
                resolver);

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
                resolver);

            const OperationResult result = source.Start();

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual(static_cast<int>(CoreResultCode::ValidationFailure), static_cast<int>(result.code));
        }

        TEST_METHOD(Start_WithMissingMonitor_ReturnsNotFoundBeforeProviderStart)
        {
            std::shared_ptr<FakeDesktopCaptureProvider> provider = CreateProvider();
            auto resolver = std::make_shared<FakeDesktopMonitorResolver>();
            WindowsDesktopVideoSource source(CreateConfig(), provider, resolver);

            const OperationResult result = source.Start();

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual(static_cast<int>(CoreResultCode::NotFound), static_cast<int>(result.code));
            Assert::AreEqual("WindowsDesktopVideoSource", result.diagnostic->component.c_str());
            Assert::AreEqual("Start", result.diagnostic->operation.c_str());
            Assert::IsFalse(provider->EmitFrame(CreateFrame()).IsSuccess());
        }
    };
}
