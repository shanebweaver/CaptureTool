#include "pch.h"
#include "CppUnitTest.h"
#include "FrameAdmissionController.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    TEST_CLASS(FrameAdmissionControllerTests)
    {
    public:
        TEST_METHOD(ShouldAccept_AcceptsFirstFrame)
        {
            FrameAdmissionController controller(30);

            Assert::IsTrue(controller.ShouldAccept(0));
            Assert::IsTrue(controller.HasAcceptedFrame());
            Assert::AreEqual(0LL, controller.GetLastAcceptedTimestamp());
        }

        TEST_METHOD(ShouldAccept_DropsFramesBeforeTargetInterval)
        {
            FrameAdmissionController controller(30);
            const LONGLONG targetDuration = controller.GetTargetFrameDurationTicks();

            Assert::IsTrue(controller.ShouldAccept(1'000'000));
            Assert::IsFalse(controller.ShouldAccept(1'000'000 + targetDuration - 1));
            Assert::AreEqual(1'000'000LL, controller.GetLastAcceptedTimestamp());
        }

        TEST_METHOD(ShouldAccept_AcceptsFrameAtTargetInterval)
        {
            FrameAdmissionController controller(30);
            const LONGLONG start = 2'000'000;
            const LONGLONG targetDuration = controller.GetTargetFrameDurationTicks();

            Assert::IsTrue(controller.ShouldAccept(start));
            Assert::IsTrue(controller.ShouldAccept(start + targetDuration));
            Assert::AreEqual(start + targetDuration, controller.GetLastAcceptedTimestamp());
        }

        TEST_METHOD(ShouldAccept_DropsNonMonotonicTimestamps)
        {
            FrameAdmissionController controller(30);

            Assert::IsTrue(controller.ShouldAccept(5'000'000));
            Assert::IsFalse(controller.ShouldAccept(5'000'000));
            Assert::IsFalse(controller.ShouldAccept(4'999'999));
            Assert::AreEqual(5'000'000LL, controller.GetLastAcceptedTimestamp());
        }

        TEST_METHOD(ShouldAccept_UsesConfiguredFrameRate)
        {
            FrameAdmissionController controller(60);
            const LONGLONG targetDuration = controller.GetTargetFrameDurationTicks();

            Assert::AreEqual(10'000'000LL / 60, targetDuration);
            Assert::IsTrue(controller.ShouldAccept(0));
            Assert::IsFalse(controller.ShouldAccept(targetDuration - 1));
            Assert::IsTrue(controller.ShouldAccept(targetDuration));
        }
    };
}
