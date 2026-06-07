#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Desktop/DesktopD3DDeviceDependency.h"
#include "V2/Desktop/FakeDesktopD3DDeviceDependency.h"
#include "V2/Desktop/WindowsGraphicsCaptureProvider.h"

#include <chrono>
#include <condition_variable>
#include <mutex>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;
using namespace CaptureInterop::V2::Desktop;

namespace
{
    DesktopVideoSourceConfig CreateConfig(uintptr_t monitorHandle = 0)
    {
        DesktopSourceConfig source;
        source.id = SourceId::FromValue(11);
        source.videoStreamId = StreamId::FromValue(12);
        source.name = "WGC test monitor";
        source.monitorHandle = monitorHandle;
        source.frameRate = Rational::From(60, 1);
        return MapDesktopVideoSourceConfig(source);
    }

    bool IsPrimaryMonitorProbeEnabled()
    {
        wchar_t value[8]{};
        const DWORD length = GetEnvironmentVariableW(
            L"CAPTURETOOL_V2_WGC_PRIMARY_MONITOR_PROBE",
            value,
            ARRAYSIZE(value));
        return length > 0 && value[0] == L'1';
    }

    std::shared_ptr<DesktopD3DDeviceDependency> CreateRealD3DDependency()
    {
        D3D_FEATURE_LEVEL featureLevels[] = { D3D_FEATURE_LEVEL_11_0 };
        wil::com_ptr<ID3D11Device> device;
        wil::com_ptr<ID3D11DeviceContext> context;

        const HRESULT hr = D3D11CreateDevice(
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

        Assert::IsTrue(SUCCEEDED(hr), L"Failed to create a local D3D11 dependency for the WGC probe.");
        return std::make_shared<DesktopD3DDeviceDependency>(
            std::move(device),
            std::move(context),
            "PrimaryMonitorProbeD3DDependency");
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2WindowsGraphicsCaptureProviderTests)
    {
    public:
        TEST_METHOD(ProviderName_IsWindowsGraphicsCaptureProvider)
        {
            WindowsGraphicsCaptureProvider provider(CreateConfig());

            Assert::AreEqual("WindowsGraphicsCaptureProvider", provider.ProviderName().c_str());
            Assert::AreEqual("WindowsGraphicsCaptureProvider", provider.Diagnostics().providerName.c_str());
        }

        TEST_METHOD(Descriptors_AreDerivedFromDesktopSourceConfig)
        {
            WindowsGraphicsCaptureProvider provider(CreateConfig());

            const SourceDescriptor source = provider.DescribeSource();
            const std::vector<StreamDescriptor> streams = provider.DescribeStreams();

            Assert::AreEqual(11u, source.id.value);
            Assert::AreEqual(static_cast<int>(SourceKind::Desktop), static_cast<int>(source.kind));
            Assert::AreEqual("WGC test monitor", source.name.c_str());
            Assert::AreEqual(static_cast<size_t>(1), streams.size());
            Assert::AreEqual(12u, streams[0].id.value);
            Assert::AreEqual(11u, streams[0].sourceId.value);
            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(streams[0].kind));
        }

        TEST_METHOD(ConfigureDeviceDependency_StoresGraphDependency)
        {
            WindowsGraphicsCaptureProvider provider(CreateConfig());
            auto dependency = std::make_shared<FakeDesktopD3DDeviceDependency>();

            const OperationResult result = provider.ConfigureDeviceDependency(dependency);

            Assert::IsTrue(result.IsSuccess());
            Assert::IsTrue(provider.DeviceDependency() == dependency);
        }

        TEST_METHOD(Start_WithoutDeviceDependency_ReturnsStructuredFailure)
        {
            WindowsGraphicsCaptureProvider provider(CreateConfig());

            const OperationResult result = provider.Start();
            const DesktopCaptureProviderDiagnostics diagnostics = provider.Diagnostics();

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual(static_cast<int>(CoreResultCode::InvalidState), static_cast<int>(result.code));
            Assert::AreEqual("WindowsGraphicsCaptureProvider", result.diagnostic->component.c_str());
            Assert::AreEqual("Start", result.diagnostic->operation.c_str());
            Assert::AreEqual(1ull, diagnostics.activationAttempts);
            Assert::AreEqual(1ull, diagnostics.activationFailures);
            Assert::IsFalse(diagnostics.resourcesActive);
        }

        TEST_METHOD(Start_WithoutMonitorHandle_ReturnsValidationFailureAndDiagnostics)
        {
            WindowsGraphicsCaptureProvider provider(CreateConfig());
            Assert::IsTrue(
                provider.ConfigureDeviceDependency(std::make_shared<FakeDesktopD3DDeviceDependency>()).IsSuccess());

            const OperationResult result = provider.Start();
            const DesktopCaptureProviderDiagnostics diagnostics = provider.Diagnostics();

            Assert::IsFalse(result.IsSuccess());
            Assert::AreEqual(static_cast<int>(CoreResultCode::ValidationFailure), static_cast<int>(result.code));
            Assert::AreEqual("Start", result.diagnostic->operation.c_str());
            Assert::AreEqual(1ull, diagnostics.providerFailures);
            Assert::AreEqual(1ull, diagnostics.activationFailures);
            Assert::AreEqual("Start", diagnostics.lastFailureOperation.c_str());
            Assert::IsFalse(diagnostics.resourcesActive);
        }

        TEST_METHOD(PrimaryMonitorActivationProbe_WhenEnabled_StartsAndStopsCaptureResources)
        {
            if (!IsPrimaryMonitorProbeEnabled())
            {
                return;
            }

            const HMONITOR primaryMonitor = MonitorFromPoint(POINT{ 0, 0 }, MONITOR_DEFAULTTOPRIMARY);
            Assert::IsNotNull(primaryMonitor);

            WindowsGraphicsCaptureProvider provider(
                CreateConfig(reinterpret_cast<uintptr_t>(primaryMonitor)));
            Assert::IsTrue(provider.ConfigureDeviceDependency(CreateRealD3DDependency()).IsSuccess());

            std::mutex frameMutex;
            std::condition_variable frameAvailable;
            uint64_t frameCount = 0;
            DesktopCaptureFrame lastFrame;
            CallbackRegistrationToken frameToken = provider.RegisterFrameArrivedHandler(
                [&](const DesktopCaptureFrame& frame)
                {
                    std::lock_guard lock(frameMutex);
                    ++frameCount;
                    lastFrame = frame;
                    frameAvailable.notify_one();
                });

            const OperationResult startResult = provider.Start();
            const DesktopCaptureProviderDiagnostics startedDiagnostics = provider.Diagnostics();

            if (!startResult.IsSuccess())
            {
                std::wstring message = L"WGC activation failed at ";
                message.append(
                    startedDiagnostics.lastFailureOperation.begin(),
                    startedDiagnostics.lastFailureOperation.end());
                message.append(L"; HRESULT=");
                message.append(startedDiagnostics.lastNativeStatus.has_value()
                    ? std::to_wstring(startedDiagnostics.lastNativeStatus.value())
                    : L"<none>");
                message.append(L"; ");
                message.append(
                    startedDiagnostics.lastFailureMessage.begin(),
                    startedDiagnostics.lastFailureMessage.end());
                Logger::WriteMessage(message.c_str());
            }

            Assert::IsTrue(startResult.IsSuccess());
            Assert::AreEqual("WindowsGraphicsCaptureProvider", provider.ProviderName().c_str());
            Assert::IsTrue(startedDiagnostics.resourcesActive);
            Assert::AreEqual(1ull, startedDiagnostics.activationAttempts);
            Assert::AreEqual(0ull, startedDiagnostics.activationFailures);
            Assert::IsTrue(provider.CurrentMediaType().width > 0);
            Assert::IsTrue(provider.CurrentMediaType().height > 0);

            {
                std::unique_lock lock(frameMutex);
                Assert::IsTrue(frameAvailable.wait_for(
                    lock,
                    std::chrono::seconds(3),
                    [&frameCount]
                    {
                        return frameCount > 0;
                    }));
            }

            const DesktopCaptureProviderDiagnostics frameDiagnostics = provider.Diagnostics();
            Assert::IsTrue(frameDiagnostics.framesProduced >= 1);
            Assert::AreEqual(11u, lastFrame.sourceId.value);
            Assert::AreEqual(12u, lastFrame.streamId.value);
            Assert::AreEqual(1ull, lastFrame.sequenceNumber);
            Assert::IsTrue(lastFrame.timestamp.ticks100ns >= 0);
            Assert::IsTrue(lastFrame.frameDimensions.width > 0);
            Assert::IsTrue(lastFrame.frameDimensions.height > 0);
            Assert::IsTrue(lastFrame.texture != nullptr);

            Assert::IsTrue(provider.Stop().IsSuccess());
            Assert::IsFalse(provider.Diagnostics().resourcesActive);
        }
    };
}
