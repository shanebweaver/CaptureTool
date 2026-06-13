#include "pch.h"
#include "CppUnitTest.h"
#include "CaptureSessionConfig.h"
#include "MonitorHdrColorSpaceMapper.h"
#include "MonitorHdrInfo.h"
#include "WindowsDesktopVideoCaptureSourceFactory.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    TEST_CLASS(MonitorHdrInfoTests)
    {
    public:
        TEST_METHOD(Sdr_CreatesSuccessfulSdrResult)
        {
            auto info = MonitorHdrInfo::Sdr(true, 42, true, 203.0f);

            Assert::IsTrue(info.detectionSucceeded);
            Assert::AreEqual(static_cast<int>(MonitorHdrMode::Sdr), static_cast<int>(info.mode));
            Assert::IsFalse(info.IsHdrActive());
            Assert::IsFalse(info.ShouldUseToneMapper());
            Assert::IsTrue(info.hasSourceColorSpace);
            Assert::AreEqual(42, info.sourceColorSpace);
            Assert::IsTrue(info.hasSdrWhiteLevelNits);
            Assert::AreEqual(203.0f, info.sdrWhiteLevelNits);
            Assert::AreEqual(static_cast<int>(MonitorHdrFallbackReason::None), static_cast<int>(info.fallbackReason));
            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(info.hr));
        }

        TEST_METHOD(Hdr_CreatesSuccessfulHdrResult)
        {
            auto info = MonitorHdrInfo::Hdr(true, 99, true, 200.0f);

            Assert::IsTrue(info.detectionSucceeded);
            Assert::AreEqual(static_cast<int>(MonitorHdrMode::Hdr), static_cast<int>(info.mode));
            Assert::IsTrue(info.IsHdrActive());
            Assert::IsTrue(info.ShouldUseToneMapper());
            Assert::IsTrue(info.hasSourceColorSpace);
            Assert::AreEqual(99, info.sourceColorSpace);
            Assert::IsTrue(info.hasSdrWhiteLevelNits);
            Assert::AreEqual(200.0f, info.sdrWhiteLevelNits);
            Assert::AreEqual(static_cast<int>(MonitorHdrFallbackReason::None), static_cast<int>(info.fallbackReason));
            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(info.hr));
        }

        TEST_METHOD(Unknown_RepresentsUnknownWithoutFailure)
        {
            auto info = MonitorHdrInfo::Unknown(MonitorHdrFallbackReason::UnsupportedColorSpace);

            Assert::IsTrue(info.detectionSucceeded);
            Assert::AreEqual(static_cast<int>(MonitorHdrMode::Unknown), static_cast<int>(info.mode));
            Assert::IsFalse(info.IsHdrActive());
            Assert::IsFalse(info.ShouldUseToneMapper());
            Assert::IsFalse(info.hasSourceColorSpace);
            Assert::IsFalse(info.hasSdrWhiteLevelNits);
            Assert::AreEqual(
                static_cast<int>(MonitorHdrFallbackReason::UnsupportedColorSpace),
                static_cast<int>(info.fallbackReason));
            Assert::AreEqual(static_cast<long>(S_OK), static_cast<long>(info.hr));
        }

        TEST_METHOD(Failed_RepresentsDetectionFailure)
        {
            auto info = MonitorHdrInfo::Failed(MonitorHdrFallbackReason::QueryFailed, E_FAIL);

            Assert::IsFalse(info.detectionSucceeded);
            Assert::AreEqual(static_cast<int>(MonitorHdrMode::Unknown), static_cast<int>(info.mode));
            Assert::IsFalse(info.IsHdrActive());
            Assert::IsFalse(info.ShouldUseToneMapper());
            Assert::IsFalse(info.hasSourceColorSpace);
            Assert::IsFalse(info.hasSdrWhiteLevelNits);
            Assert::AreEqual(static_cast<int>(MonitorHdrFallbackReason::QueryFailed), static_cast<int>(info.fallbackReason));
            Assert::AreEqual(static_cast<long>(E_FAIL), static_cast<long>(info.hr));
        }

        TEST_METHOD(Unknown_CanPreserveSourceColorSpace)
        {
            auto info = MonitorHdrInfo::Unknown(MonitorHdrFallbackReason::UnsupportedColorSpace, true, 123);

            Assert::IsTrue(info.detectionSucceeded);
            Assert::AreEqual(static_cast<int>(MonitorHdrMode::Unknown), static_cast<int>(info.mode));
            Assert::IsTrue(info.hasSourceColorSpace);
            Assert::AreEqual(123, info.sourceColorSpace);
            Assert::AreEqual(
                static_cast<int>(MonitorHdrFallbackReason::UnsupportedColorSpace),
                static_cast<int>(info.fallbackReason));
        }
    };

    TEST_CLASS(MonitorHdrColorSpaceMapperTests)
    {
    public:
        TEST_METHOD(FromColorSpace_MapsPq2020ToHdr)
        {
            auto info = MonitorHdrColorSpaceMapper::FromColorSpace(DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020);

            Assert::IsTrue(info.detectionSucceeded);
            Assert::AreEqual(static_cast<int>(MonitorHdrMode::Hdr), static_cast<int>(info.mode));
            Assert::IsTrue(info.ShouldUseToneMapper());
            Assert::IsTrue(info.hasSourceColorSpace);
            Assert::AreEqual(
                static_cast<int>(DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020),
                info.sourceColorSpace);
        }

        TEST_METHOD(FromColorSpace_MapsKnownSdrToSdr)
        {
            auto info = MonitorHdrColorSpaceMapper::FromColorSpace(DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709);

            Assert::IsTrue(info.detectionSucceeded);
            Assert::AreEqual(static_cast<int>(MonitorHdrMode::Sdr), static_cast<int>(info.mode));
            Assert::IsFalse(info.ShouldUseToneMapper());
            Assert::IsTrue(info.hasSourceColorSpace);
            Assert::AreEqual(
                static_cast<int>(DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709),
                info.sourceColorSpace);
        }

        TEST_METHOD(FromColorSpace_MapsUnknownColorSpaceToUnknown)
        {
            auto info = MonitorHdrColorSpaceMapper::FromColorSpace(static_cast<DXGI_COLOR_SPACE_TYPE>(9999));

            Assert::IsTrue(info.detectionSucceeded);
            Assert::AreEqual(static_cast<int>(MonitorHdrMode::Unknown), static_cast<int>(info.mode));
            Assert::IsFalse(info.ShouldUseToneMapper());
            Assert::IsTrue(info.hasSourceColorSpace);
            Assert::AreEqual(9999, info.sourceColorSpace);
            Assert::AreEqual(
                static_cast<int>(MonitorHdrFallbackReason::UnsupportedColorSpace),
                static_cast<int>(info.fallbackReason));
        }
    };

    class FakeMonitorHdrDetector final : public IMonitorHdrDetector
    {
    public:
        MonitorHdrInfo Detect(HMONITOR) override
        {
            return MonitorHdrInfo::Hdr();
        }
    };

    class CountingMonitorHdrDetectorFactory final : public IMonitorHdrDetectorFactory
    {
    public:
        explicit CountingMonitorHdrDetectorFactory(int* createCount)
            : m_createCount(createCount)
        {
        }

        std::unique_ptr<IMonitorHdrDetector> CreateMonitorHdrDetector() override
        {
            if (m_createCount)
            {
                ++(*m_createCount);
            }

            return std::make_unique<FakeMonitorHdrDetector>();
        }

    private:
        int* m_createCount;
    };

    TEST_CLASS(MonitorHdrDetectorInjectionTests)
    {
    public:
        TEST_METHOD(VideoCaptureSourceFactory_UsesInjectedDetectorFactory)
        {
            int createCount = 0;
            WindowsDesktopVideoCaptureSourceFactory factory(
                std::make_unique<CountingMonitorHdrDetectorFactory>(&createCount));
            CaptureSessionConfig config(reinterpret_cast<HMONITOR>(1), L"test.mp4");

            auto source = factory.CreateVideoCaptureSource(config, nullptr);

            Assert::IsTrue(source != nullptr);
            Assert::AreEqual(1, createCount);
        }
    };
}
