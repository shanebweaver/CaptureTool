#include "pch.h"
#include "CppUnitTest.h"
#include "HdrToSdrVideoFrameProcessor.h"
#include "PassthroughVideoFrameProcessor.h"
#include "VideoFrameProcessorFactory.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    namespace
    {
        wil::com_ptr<ID3D11Device> CreateWarpDevice()
        {
            wil::com_ptr<ID3D11Device> device;
            D3D_FEATURE_LEVEL featureLevel{};
            HRESULT hr = D3D11CreateDevice(
                nullptr,
                D3D_DRIVER_TYPE_WARP,
                nullptr,
                D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                nullptr,
                0,
                D3D11_SDK_VERSION,
                device.put(),
                &featureLevel,
                nullptr);

            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(hr));
            return device;
        }
    }

    TEST_CLASS(VideoFrameProcessorFactoryTests)
    {
    public:
        TEST_METHOD(CreateProcessor_ForSdrMonitor_ReturnsProcessor)
        {
            VideoFrameProcessorFactory factory;
            VideoFrameProcessorFactoryContext context{
                MonitorHdrInfo::Sdr(),
                nullptr,
                0,
                0
            };

            auto result = factory.CreateProcessor(context);

            Assert::IsTrue(result.IsOk());
            Assert::IsTrue(result.Value() != nullptr);
            Assert::IsTrue(dynamic_cast<PassthroughVideoFrameProcessor*>(result.Value().get()) != nullptr);
        }

        TEST_METHOD(CreateProcessor_ForHdrMonitorWithDevice_ReturnsHdrProcessor)
        {
            VideoFrameProcessorFactory factory;
            auto device = CreateWarpDevice();
            VideoFrameProcessorFactoryContext context{
                MonitorHdrInfo::Hdr(),
                device.get(),
                16,
                16
            };

            auto result = factory.CreateProcessor(context);

            Assert::IsTrue(result.IsOk());
            Assert::IsTrue(result.Value() != nullptr);
            Assert::IsTrue(dynamic_cast<HdrToSdrVideoFrameProcessor*>(result.Value().get()) != nullptr);
        }

        TEST_METHOD(CreateProcessor_ForHdrMonitorWithInvalidDevice_ReturnsPassthrough)
        {
            VideoFrameProcessorFactory factory;
            VideoFrameProcessorFactoryContext context{
                MonitorHdrInfo::Hdr(),
                nullptr,
                16,
                16
            };

            auto result = factory.CreateProcessor(context);

            Assert::IsTrue(result.IsOk());
            Assert::IsTrue(result.Value() != nullptr);
            Assert::IsTrue(dynamic_cast<PassthroughVideoFrameProcessor*>(result.Value().get()) != nullptr);
        }

        TEST_METHOD(CreateProcessor_ForDetectorFailure_ReturnsPassthrough)
        {
            VideoFrameProcessorFactory factory;
            VideoFrameProcessorFactoryContext context{
                MonitorHdrInfo::Failed(MonitorHdrFallbackReason::QueryFailed, E_FAIL),
                nullptr,
                16,
                16
            };

            auto result = factory.CreateProcessor(context);

            Assert::IsTrue(result.IsOk());
            Assert::IsTrue(result.Value() != nullptr);
            Assert::IsTrue(dynamic_cast<PassthroughVideoFrameProcessor*>(result.Value().get()) != nullptr);
        }

        TEST_METHOD(CreateProcessor_ForUnknownMonitorState_ReturnsPassthrough)
        {
            VideoFrameProcessorFactory factory;
            VideoFrameProcessorFactoryContext context{
                MonitorHdrInfo::Unknown(),
                nullptr,
                16,
                16
            };

            auto result = factory.CreateProcessor(context);

            Assert::IsTrue(result.IsOk());
            Assert::IsTrue(result.Value() != nullptr);
            Assert::IsTrue(dynamic_cast<PassthroughVideoFrameProcessor*>(result.Value().get()) != nullptr);
        }
    };
}
