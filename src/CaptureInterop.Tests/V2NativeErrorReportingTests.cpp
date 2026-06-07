#include "pch.h"
#include "CppUnitTest.h"
#include "V2/CaptureInteropV2Exports.h"

#include <string>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace
{
    struct RecorderFixture
    {
        CtCaptureV2_RecorderHandle handle{ nullptr };

        RecorderFixture()
        {
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_CreateRecorder(&handle));
        }

        ~RecorderFixture()
        {
            (void)CtCaptureV2_DestroyRecorder(handle);
        }
    };

    struct ValidErrorConfig
    {
        CtCaptureV2_SourceConfig source{};
        CtCaptureV2_Config config{};

        ValidErrorConfig()
        {
            CtCaptureV2_InitSourceConfig(&source);
            source.sourceId = 1;
            source.sourceKind = CtCaptureV2_SourceKind_Desktop;
            source.captureRect = CtCaptureV2_Rect{ 0, 0, 1920, 1080 };
            source.enabled = 1;

            CtCaptureV2_InitConfig(&config);
            config.sources = &source;
            config.sourceCount = 1;
            config.output.outputPath = u"C:\\Temp\\capture-v2.mp4";
            config.output.containerFormat = CtCaptureV2_ContainerFormat_Mp4;
            config.output.video.codec = CtCaptureV2_VideoCodec_H264;
            config.output.video.bitrate = 8'000'000;
            config.output.video.frameRateNumerator = 60;
            config.output.video.frameRateDenominator = 1;
            config.output.video.gopLength = 120;
            config.output.video.hardwareAccelerationPreferred = 1;
        }
    };
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2NativeErrorReportingTests)
    {
    public:
        TEST_METHOD(GetLastError_AfterInvalidStartSizingCall_ReturnsRequiredLengthAndDetails)
        {
            RecorderFixture recorder;
            CtCaptureV2_ErrorInfo errorInfo{};
            uint32_t requiredLength = 0;

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidArgument),
                CtCaptureV2_Start(recorder.handle, nullptr));

            const int32_t result = CtCaptureV2_GetLastError(
                recorder.handle,
                &errorInfo,
                nullptr,
                0,
                &requiredLength);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_BufferTooSmall), result);
            Assert::AreEqual(static_cast<uint32_t>(sizeof(CtCaptureV2_ErrorInfo)), errorInfo.size);
            Assert::AreEqual(CtCaptureV2_DtoVersion, errorInfo.version);
            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidArgument), errorInfo.resultCode);
            Assert::AreEqual("CaptureInteropV2Recorder", errorInfo.component);
            Assert::AreEqual("Start", errorInfo.operation);
            Assert::IsTrue(requiredLength > 1);
        }

        TEST_METHOD(GetLastError_WithExactBuffer_ReturnsMessage)
        {
            RecorderFixture recorder;
            CtCaptureV2_ErrorInfo errorInfo{};
            uint32_t requiredLength = 0;
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidState),
                CtCaptureV2_Pause(recorder.handle));
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_BufferTooSmall),
                CtCaptureV2_GetLastError(recorder.handle, &errorInfo, nullptr, 0, &requiredLength));

            std::vector<char16_t> message(requiredLength);
            const int32_t result = CtCaptureV2_GetLastError(
                recorder.handle,
                &errorInfo,
                message.data(),
                static_cast<uint32_t>(message.size()),
                &requiredLength);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_Success), result);
            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidState), errorInfo.resultCode);
            Assert::AreEqual("Pause", errorInfo.operation);
            Assert::IsTrue(std::u16string{ message.data() }.find(u"paused") != std::u16string::npos);
        }

        TEST_METHOD(GetLastError_WithTooSmallBuffer_ReturnsBufferTooSmall)
        {
            RecorderFixture recorder;
            CtCaptureV2_ErrorInfo errorInfo{};
            char16_t smallBuffer[2]{};
            uint32_t requiredLength = 0;
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidState),
                CtCaptureV2_SetAudioGain(recorder.handle, 2, 0.0F));

            const int32_t result = CtCaptureV2_GetLastError(
                recorder.handle,
                &errorInfo,
                smallBuffer,
                2,
                &requiredLength);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_BufferTooSmall), result);
            Assert::IsTrue(requiredLength > 2);
        }

        TEST_METHOD(GetLastError_NullHandle_ReturnsInvalidHandle)
        {
            CtCaptureV2_ErrorInfo errorInfo{};
            uint32_t requiredLength = 0;

            const int32_t result = CtCaptureV2_GetLastError(
                nullptr,
                &errorInfo,
                nullptr,
                0,
                &requiredLength);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidHandle), result);
        }

        TEST_METHOD(GetLastError_SuccessfulOperationClearsPreviousError)
        {
            RecorderFixture recorder;
            ValidErrorConfig config;
            CtCaptureV2_ErrorInfo errorInfo{};
            char16_t message[1]{};
            uint32_t requiredLength = 0;
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidState),
                CtCaptureV2_Pause(recorder.handle));
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_Start(recorder.handle, &config.config));

            const int32_t result = CtCaptureV2_GetLastError(
                recorder.handle,
                &errorInfo,
                message,
                1,
                &requiredLength);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_Success), result);
            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_Success), errorInfo.resultCode);
            Assert::AreEqual(static_cast<uint32_t>(1), requiredLength);
            Assert::AreEqual(0, static_cast<int>(message[0]));
        }
    };
}
