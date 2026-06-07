#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Desktop/DesktopVideoSourceConfig.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;
using namespace CaptureInterop::V2::Desktop;

namespace
{
    DesktopSourceConfig CreateDesktopConfig()
    {
        DesktopSourceConfig config;
        config.id = SourceId::FromValue(10);
        config.videoStreamId = StreamId::FromValue(20);
        config.name = "Primary monitor";
        config.displayId = "DISPLAY1";
        config.monitorDeviceName = "\\\\.\\DISPLAY1";
        config.monitorHandle = 0x1234;
        config.frameRate = Rational::From(60, 1);
        return config;
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2DesktopSourceConfigTests)
    {
    public:
        TEST_METHOD(MapDesktopVideoSourceConfig_FullMonitor_PreservesMonitorTarget)
        {
            DesktopSourceConfig config = CreateDesktopConfig();

            const DesktopVideoSourceConfig mapped = MapDesktopVideoSourceConfig(config);

            Assert::AreEqual(10u, mapped.sourceId.value);
            Assert::AreEqual(20u, mapped.streamId.value);
            Assert::AreEqual("Primary monitor", mapped.sourceName.c_str());
            Assert::AreEqual("Primary monitor video", mapped.streamName.c_str());
            Assert::AreEqual(static_cast<uintptr_t>(0x1234), mapped.monitor.monitorHandle);
            Assert::AreEqual("DISPLAY1", mapped.monitor.displayId.c_str());
            Assert::AreEqual("\\\\.\\DISPLAY1", mapped.monitor.deviceName.c_str());
            Assert::IsTrue(mapped.monitor.HasIdentity());
            Assert::IsFalse(mapped.CapturesRegion());
            Assert::IsFalse(mapped.region.has_value());
        }

        TEST_METHOD(MapDesktopVideoSourceConfig_Region_PreservesPhysicalPixelArea)
        {
            DesktopSourceConfig config = CreateDesktopConfig();
            config.captureArea = CaptureRectangle{ 100, 200, 1280, 720 };

            const DesktopVideoSourceConfig mapped = MapDesktopVideoSourceConfig(config);

            Assert::IsTrue(mapped.CapturesRegion());
            Assert::IsTrue(mapped.region.has_value());
            Assert::AreEqual(100, mapped.region->x);
            Assert::AreEqual(200, mapped.region->y);
            Assert::AreEqual(1280u, mapped.region->width);
            Assert::AreEqual(720u, mapped.region->height);
        }

        TEST_METHOD(MapDesktopVideoSourceConfig_DefaultStreamId_UsesSourceId)
        {
            DesktopSourceConfig config = CreateDesktopConfig();
            config.videoStreamId = StreamId::Invalid();

            const DesktopVideoSourceConfig mapped = MapDesktopVideoSourceConfig(config);

            Assert::AreEqual(config.id.value, mapped.streamId.value);
        }

        TEST_METHOD(MapDesktopVideoSourceConfig_PreservesCursorCapturePolicy)
        {
            DesktopSourceConfig config = CreateDesktopConfig();
            config.cursorPolicy = CursorCapturePolicy::Excluded;

            const DesktopVideoSourceConfig mapped = MapDesktopVideoSourceConfig(config);

            Assert::AreEqual(
                static_cast<int>(CursorCapturePolicy::Excluded),
                static_cast<int>(mapped.cursorPolicy));
        }

        TEST_METHOD(SourceDescriptor_UsesDesktopKindAndHumanReadableName)
        {
            const DesktopVideoSourceConfig mapped = MapDesktopVideoSourceConfig(CreateDesktopConfig());

            const SourceDescriptor descriptor = mapped.SourceDescriptor();

            Assert::AreEqual(10u, descriptor.id.value);
            Assert::AreEqual(static_cast<int>(SourceKind::Desktop), static_cast<int>(descriptor.kind));
            Assert::AreEqual("Primary monitor", descriptor.name.c_str());
        }

        TEST_METHOD(StreamDescriptor_UsesVideoKindAndParentSourceId)
        {
            const DesktopVideoSourceConfig mapped = MapDesktopVideoSourceConfig(CreateDesktopConfig());

            const StreamDescriptor descriptor = mapped.StreamDescriptor();

            Assert::AreEqual(20u, descriptor.id.value);
            Assert::AreEqual(10u, descriptor.sourceId.value);
            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(descriptor.kind));
            Assert::AreEqual("Primary monitor video", descriptor.name.c_str());
        }

        TEST_METHOD(BuildDesktopVideoStreams_CreatesExactlyOneVideoStream)
        {
            const DesktopVideoSourceConfig mapped = MapDesktopVideoSourceConfig(CreateDesktopConfig());

            const std::vector<StreamDescriptor> streams = BuildDesktopVideoStreams(mapped);

            Assert::AreEqual(static_cast<size_t>(1), streams.size());
            Assert::AreEqual(20u, streams[0].id.value);
            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(streams[0].kind));
        }

        TEST_METHOD(Descriptors_AreStableAcrossRepeatedReads)
        {
            const DesktopVideoSourceConfig mapped = MapDesktopVideoSourceConfig(CreateDesktopConfig());

            const SourceDescriptor firstSource = mapped.SourceDescriptor();
            const SourceDescriptor secondSource = mapped.SourceDescriptor();
            const StreamDescriptor firstStream = mapped.StreamDescriptor();
            const StreamDescriptor secondStream = mapped.StreamDescriptor();

            Assert::AreEqual(firstSource.id.value, secondSource.id.value);
            Assert::AreEqual(firstSource.name.c_str(), secondSource.name.c_str());
            Assert::AreEqual(firstStream.id.value, secondStream.id.value);
            Assert::AreEqual(firstStream.sourceId.value, secondStream.sourceId.value);
            Assert::AreEqual(firstStream.name.c_str(), secondStream.name.c_str());
        }
    };
}
