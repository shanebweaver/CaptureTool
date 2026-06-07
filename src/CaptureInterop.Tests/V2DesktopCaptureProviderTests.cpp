#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Desktop/DesktopColorMetadata.h"
#include "V2/Desktop/FakeDesktopD3DDeviceDependency.h"
#include "V2/Desktop/FakeDesktopCaptureProvider.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;
using namespace CaptureInterop::V2::Desktop;

namespace
{
    VideoMediaType CreateMediaType()
    {
        return VideoMediaType{
            1920,
            1080,
            Rational::From(60, 1),
            VideoPixelFormat::Bgra8,
            ColorPrimaries::Unknown,
            TransferFunction::Unknown,
            ColorRange::Unknown
        };
    }

    FakeDesktopCaptureProvider CreateProvider()
    {
        return FakeDesktopCaptureProvider(
            SourceDescriptor{ SourceId::FromValue(1), SourceKind::Desktop, "Desktop" },
            { StreamDescriptor{ StreamId::FromValue(1), SourceId::FromValue(1), MediaKind::Video, "Desktop video" } },
            CreateMediaType());
    }

    FakeDesktopCaptureProvider CreateProvider(VideoMediaType mediaType)
    {
        return FakeDesktopCaptureProvider(
            SourceDescriptor{ SourceId::FromValue(1), SourceKind::Desktop, "Desktop" },
            { StreamDescriptor{ StreamId::FromValue(1), SourceId::FromValue(1), MediaKind::Video, "Desktop video" } },
            mediaType);
    }

    std::shared_ptr<FakeDesktopD3DDeviceDependency> CreateDependency()
    {
        return std::make_shared<FakeDesktopD3DDeviceDependency>();
    }

    DesktopCaptureFrame CreateFrame(uint64_t sequence = 1)
    {
        return DesktopCaptureFrame{
            SourceId::FromValue(1),
            StreamId::FromValue(1),
            CreateMediaType(),
            MediaTime::FromTicks(123),
            MediaDuration::FromMilliseconds(16),
            sequence,
            { 1, 2, 3, 4 }
        };
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2DesktopCaptureProviderTests)
    {
    public:
        TEST_METHOD(FakeProvider_ExposesDescriptorsAndMediaTypeWithoutWindowsTypes)
        {
            FakeDesktopCaptureProvider provider = CreateProvider();

            const SourceDescriptor source = provider.DescribeSource();
            const std::vector<StreamDescriptor> streams = provider.DescribeStreams();
            const VideoMediaType mediaType = provider.CurrentMediaType();

            Assert::AreEqual("FakeDesktopCaptureProvider", provider.ProviderName().c_str());
            Assert::AreEqual(1u, source.id.value);
            Assert::AreEqual(static_cast<int>(SourceKind::Desktop), static_cast<int>(source.kind));
            Assert::AreEqual(static_cast<size_t>(1), streams.size());
            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(streams[0].kind));
            Assert::AreEqual(1920u, mediaType.width);
            Assert::AreEqual(1080u, mediaType.height);
            Assert::AreEqual(static_cast<int>(VideoPixelFormat::Bgra8), static_cast<int>(mediaType.pixelFormat));
        }

        TEST_METHOD(FakeProvider_StartThenEmit_InvokesFrameHandler)
        {
            FakeDesktopCaptureProvider provider = CreateProvider();
            uint64_t receivedSequence = 0;
            CallbackRegistrationToken token = provider.RegisterFrameArrivedHandler(
                [&receivedSequence](const DesktopCaptureFrame& frame)
                {
                    receivedSequence = frame.sequenceNumber;
                });

            Assert::IsTrue(provider.ConfigureDeviceDependency(CreateDependency()).IsSuccess());
            Assert::IsTrue(provider.Start().IsSuccess());
            Assert::IsTrue(provider.EmitFrame(CreateFrame(42)).IsSuccess());

            Assert::AreEqual(42ull, receivedSequence);
        }

        TEST_METHOD(FakeProvider_StartWithoutD3DDependency_ReturnsInvalidState)
        {
            FakeDesktopCaptureProvider provider = CreateProvider();

            const OperationResult result = provider.Start();

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual(static_cast<int>(CoreResultCode::InvalidState), static_cast<int>(result.code));
            Assert::AreEqual("Start", result.diagnostic->operation.c_str());
        }

        TEST_METHOD(FakeProvider_ConfigureDeviceDependency_StoresGraphDependency)
        {
            FakeDesktopCaptureProvider provider = CreateProvider();
            std::shared_ptr<FakeDesktopD3DDeviceDependency> dependency = CreateDependency();

            const OperationResult result = provider.ConfigureDeviceDependency(dependency);

            Assert::IsTrue(result.IsSuccess());
            Assert::IsTrue(provider.DeviceDependency() == dependency);
        }

        TEST_METHOD(FakeProvider_ConfigureRemovedDevice_ReturnsStructuredFailure)
        {
            FakeDesktopCaptureProvider provider = CreateProvider();
            std::shared_ptr<FakeDesktopD3DDeviceDependency> dependency = CreateDependency();
            dependency->SetHealthFailure(OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "FakeDesktopD3DDeviceDependency",
                "CheckDeviceHealth",
                "D3D device was removed or reset",
                static_cast<int64_t>(0x887A0005)));

            const OperationResult result = provider.ConfigureDeviceDependency(dependency);

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual(static_cast<int>(CoreResultCode::NativeFailure), static_cast<int>(result.code));
            Assert::AreEqual("CheckDeviceHealth", result.diagnostic->operation.c_str());
            Assert::AreEqual(static_cast<int64_t>(0x887A0005), *result.diagnostic->nativeStatus);
        }

        TEST_METHOD(FakeProvider_EmitBeforeStart_ReturnsInvalidState)
        {
            FakeDesktopCaptureProvider provider = CreateProvider();

            const OperationResult result = provider.EmitFrame(CreateFrame());

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual(static_cast<int>(CoreResultCode::InvalidState), static_cast<int>(result.diagnostic->code));
            Assert::AreEqual("EmitFrame", result.diagnostic->operation.c_str());
        }

        TEST_METHOD(FakeProvider_DestroyedCallbackToken_PreventsFutureInvocation)
        {
            FakeDesktopCaptureProvider provider = CreateProvider();
            uint32_t invocationCount = 0;

            {
                CallbackRegistrationToken token = provider.RegisterFrameArrivedHandler(
                    [&invocationCount](const DesktopCaptureFrame&)
                    {
                        ++invocationCount;
                    });
            }

            Assert::IsTrue(provider.ConfigureDeviceDependency(CreateDependency()).IsSuccess());
            Assert::IsTrue(provider.Start().IsSuccess());
            Assert::IsTrue(provider.EmitFrame(CreateFrame()).IsSuccess());

            Assert::AreEqual(0u, invocationCount);
        }

        TEST_METHOD(FakeProvider_FrameCallbackTokenCanOutliveProvider)
        {
            CallbackRegistrationToken token;
            {
                FakeDesktopCaptureProvider provider = CreateProvider();
                token = provider.RegisterFrameArrivedHandler(
                    [](const DesktopCaptureFrame&)
                    {
                    });
            }

            token.reset();
        }

        TEST_METHOD(FakeProvider_FailureCallbackTokenCanOutliveProvider)
        {
            CallbackRegistrationToken token;
            {
                FakeDesktopCaptureProvider provider = CreateProvider();
                token = provider.RegisterProviderFailedHandler(
                    [](const OperationResult&)
                    {
                    });
            }

            token.reset();
        }

        TEST_METHOD(FakeProvider_EmitCopiesHandlersBeforeInvoking)
        {
            FakeDesktopCaptureProvider provider = CreateProvider();
            CallbackRegistrationToken token;
            uint32_t invocationCount = 0;
            token = provider.RegisterFrameArrivedHandler(
                [&token, &invocationCount](const DesktopCaptureFrame&)
                {
                    ++invocationCount;
                    token.reset();
                });

            Assert::IsTrue(provider.ConfigureDeviceDependency(CreateDependency()).IsSuccess());
            Assert::IsTrue(provider.Start().IsSuccess());

            Assert::IsTrue(provider.EmitFrame(CreateFrame()).IsSuccess());
            Assert::IsTrue(provider.EmitFrame(CreateFrame(2)).IsSuccess());

            Assert::AreEqual(1u, invocationCount);
        }

        TEST_METHOD(FakeProvider_DiagnosticsTrackProducedFrames)
        {
            FakeDesktopCaptureProvider provider = CreateProvider();
            CallbackRegistrationToken token = provider.RegisterFrameArrivedHandler(
                [](const DesktopCaptureFrame&)
                {
                });

            Assert::IsTrue(provider.ConfigureDeviceDependency(CreateDependency()).IsSuccess());
            Assert::IsTrue(provider.Start().IsSuccess());
            Assert::IsTrue(provider.EmitFrame(CreateFrame()).IsSuccess());
            Assert::IsTrue(provider.EmitFrame(CreateFrame(2)).IsSuccess());

            const DesktopCaptureProviderDiagnostics diagnostics = provider.Diagnostics();

            Assert::AreEqual("FakeDesktopCaptureProvider", diagnostics.providerName.c_str());
            Assert::AreEqual(2ull, diagnostics.framesProduced);
            Assert::AreEqual(0ull, diagnostics.providerFailures);
        }

        TEST_METHOD(ColorMetadata_KnownValuesAreMappedIntoMediaType)
        {
            VideoMediaType mediaType = CreateMediaType();

            mediaType = ApplyDesktopColorMetadata(
                mediaType,
                DesktopColorMetadata{
                    ColorPrimaries::Rec709,
                    TransferFunction::Srgb,
                    ColorRange::Full
                });

            Assert::AreEqual(static_cast<int>(ColorPrimaries::Rec709), static_cast<int>(mediaType.colorPrimaries));
            Assert::AreEqual(static_cast<int>(TransferFunction::Srgb), static_cast<int>(mediaType.transferFunction));
            Assert::AreEqual(static_cast<int>(ColorRange::Full), static_cast<int>(mediaType.range));
        }

        TEST_METHOD(ColorMetadata_UnknownValuesAreNotGuessed)
        {
            VideoMediaType mediaType = CreateMediaType();

            mediaType = ApplyDesktopColorMetadata(
                mediaType,
                DesktopColorMetadata{
                    ColorPrimaries::Unknown,
                    TransferFunction::Unknown,
                    ColorRange::Unknown
                });

            Assert::AreEqual(static_cast<int>(ColorPrimaries::Unknown), static_cast<int>(mediaType.colorPrimaries));
            Assert::AreEqual(static_cast<int>(TransferFunction::Unknown), static_cast<int>(mediaType.transferFunction));
            Assert::AreEqual(static_cast<int>(ColorRange::Unknown), static_cast<int>(mediaType.range));
        }

        TEST_METHOD(ColorDiagnostics_HdrAndWideColorReportPendingAutoToneMapping)
        {
            VideoMediaType mediaType = CreateMediaType();
            mediaType.pixelFormat = VideoPixelFormat::Rgba16Float;
            mediaType.colorPrimaries = ColorPrimaries::Rec2020;
            mediaType.transferFunction = TransferFunction::St2084Pq;
            FakeDesktopCaptureProvider provider = CreateProvider(mediaType);

            const DesktopCaptureProviderDiagnostics diagnostics = provider.Diagnostics();

            Assert::AreEqual(static_cast<int>(HdrPolicy::Auto), static_cast<int>(diagnostics.color.hdrPolicy));
            Assert::AreEqual(static_cast<int>(ColorPrimaries::Rec2020), static_cast<int>(diagnostics.color.colorPrimaries));
            Assert::AreEqual(
                static_cast<int>(TransferFunction::St2084Pq),
                static_cast<int>(diagnostics.color.transferFunction));
            Assert::AreEqual(static_cast<int>(ColorRange::Unknown), static_cast<int>(diagnostics.color.colorRange));
            Assert::IsTrue(diagnostics.color.hdrInputDetected);
            Assert::IsTrue(diagnostics.color.wideColorInputDetected);
            Assert::IsTrue(diagnostics.color.hdrToneMappingPending);
        }

        TEST_METHOD(FakeProvider_FailActiveCapture_NotifiesFailureHandlerAndDiagnostics)
        {
            FakeDesktopCaptureProvider provider = CreateProvider();
            OperationResult receivedFailure;
            CallbackRegistrationToken token = provider.RegisterProviderFailedHandler(
                [&receivedFailure](const OperationResult& failure)
                {
                    receivedFailure = failure;
                });

            Assert::IsTrue(provider.ConfigureDeviceDependency(CreateDependency()).IsSuccess());
            Assert::IsTrue(provider.Start().IsSuccess());

            OperationResult failure = OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "FakeDesktopCaptureProvider",
                "MonitorLost",
                "Configured monitor disappeared",
                -7);
            Assert::IsTrue(provider.FailActiveCapture(failure).IsSuccess());

            const DesktopCaptureProviderDiagnostics diagnostics = provider.Diagnostics();
            Assert::AreEqual(static_cast<int>(CoreResultCode::NativeFailure), static_cast<int>(receivedFailure.code));
            Assert::AreEqual(1ull, diagnostics.providerFailures);
            Assert::AreEqual("MonitorLost", diagnostics.lastFailureOperation.c_str());
            Assert::AreEqual("Configured monitor disappeared", diagnostics.lastFailureMessage.c_str());
            Assert::AreEqual(static_cast<int64_t>(-7), *diagnostics.lastNativeStatus);
            Assert::IsFalse(diagnostics.resourcesActive);
            Assert::IsFalse(provider.EmitFrame(CreateFrame(99)).IsSuccess());
        }
    };
}
