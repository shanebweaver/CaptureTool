#include "pch.h"
#include "CppUnitTest.h"
#include "V2/CaptureInteropV2Exports.h"

#include <type_traits>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    TEST_CLASS(V2NativeAbiFoundationTests)
    {
    public:
        TEST_METHOD(GetApiVersion_PopulatesVersionDto)
        {
            CtCaptureV2_ApiVersion version{};

            const int32_t result = CtCaptureV2_GetApiVersion(&version);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_Success), result);
            Assert::AreEqual(static_cast<uint32_t>(sizeof(CtCaptureV2_ApiVersion)), version.size);
            Assert::AreEqual(static_cast<uint32_t>(1), version.version);
            Assert::AreEqual(static_cast<uint32_t>(2), version.major);
            Assert::AreEqual(static_cast<uint32_t>(0), version.minor);
            Assert::AreEqual(static_cast<uint32_t>(0), version.patch);
            Assert::AreEqual(static_cast<uint32_t>(0), version.reserved);
        }

        TEST_METHOD(GetApiVersion_NullOutput_ReturnsInvalidArgument)
        {
            const int32_t result = CtCaptureV2_GetApiVersion(nullptr);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidArgument), result);
        }

        TEST_METHOD(ResultCode_ValuesAreStable)
        {
            Assert::AreEqual(0, static_cast<int>(CtCaptureV2_ResultCode_Success));
            Assert::AreEqual(1, static_cast<int>(CtCaptureV2_ResultCode_InvalidArgument));
            Assert::AreEqual(2, static_cast<int>(CtCaptureV2_ResultCode_InvalidHandle));
            Assert::AreEqual(3, static_cast<int>(CtCaptureV2_ResultCode_InvalidState));
            Assert::AreEqual(4, static_cast<int>(CtCaptureV2_ResultCode_UnsupportedVersion));
            Assert::AreEqual(5, static_cast<int>(CtCaptureV2_ResultCode_UnsupportedOperation));
            Assert::AreEqual(6, static_cast<int>(CtCaptureV2_ResultCode_ValidationFailed));
            Assert::AreEqual(7, static_cast<int>(CtCaptureV2_ResultCode_NotFound));
            Assert::AreEqual(8, static_cast<int>(CtCaptureV2_ResultCode_AlreadyStarted));
            Assert::AreEqual(9, static_cast<int>(CtCaptureV2_ResultCode_AlreadyStopped));
            Assert::AreEqual(10, static_cast<int>(CtCaptureV2_ResultCode_BufferTooSmall));
            Assert::AreEqual(11, static_cast<int>(CtCaptureV2_ResultCode_NativeFailure));
            Assert::AreEqual(12, static_cast<int>(CtCaptureV2_ResultCode_ExternalApiFailure));
            Assert::AreEqual(13, static_cast<int>(CtCaptureV2_ResultCode_CallbackRegistrationFailed));
            Assert::AreEqual(14, static_cast<int>(CtCaptureV2_ResultCode_CallbackInvocationFailed));
        }

        TEST_METHOD(ApiVersionDto_IsSizePrefixedAndPlainData)
        {
            Assert::IsTrue(std::is_standard_layout_v<CtCaptureV2_ApiVersion>);
            Assert::IsTrue(std::is_trivially_copyable_v<CtCaptureV2_ApiVersion>);
            Assert::AreEqual(static_cast<size_t>(0), offsetof(CtCaptureV2_ApiVersion, size));
            Assert::AreEqual(sizeof(uint32_t), offsetof(CtCaptureV2_ApiVersion, version));
        }
    };
}
