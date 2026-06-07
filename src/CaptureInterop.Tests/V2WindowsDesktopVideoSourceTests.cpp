#include "pch.h"
#include "CppUnitTest.h"
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

    std::shared_ptr<FakeDesktopCaptureProvider> CreateProvider()
    {
        DesktopVideoSourceConfig config = CreateConfig();
        return std::make_shared<FakeDesktopCaptureProvider>(
            config.SourceDescriptor(),
            BuildDesktopVideoStreams(config),
            CreateMediaType());
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
    };
}
