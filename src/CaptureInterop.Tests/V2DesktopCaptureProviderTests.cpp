#include "pch.h"
#include "CppUnitTest.h"
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

            Assert::IsTrue(provider.Start().IsSuccess());
            Assert::IsTrue(provider.EmitFrame(CreateFrame(42)).IsSuccess());

            Assert::AreEqual(42ull, receivedSequence);
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

            Assert::IsTrue(provider.Start().IsSuccess());
            Assert::IsTrue(provider.EmitFrame(CreateFrame()).IsSuccess());

            Assert::AreEqual(0u, invocationCount);
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

            Assert::IsTrue(provider.Start().IsSuccess());
            Assert::IsTrue(provider.EmitFrame(CreateFrame()).IsSuccess());
            Assert::IsTrue(provider.EmitFrame(CreateFrame(2)).IsSuccess());

            const DesktopCaptureProviderDiagnostics diagnostics = provider.Diagnostics();

            Assert::AreEqual("FakeDesktopCaptureProvider", diagnostics.providerName.c_str());
            Assert::AreEqual(2ull, diagnostics.framesProduced);
            Assert::AreEqual(0ull, diagnostics.providerFailures);
        }
    };
}
