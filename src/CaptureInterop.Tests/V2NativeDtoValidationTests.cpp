#include "pch.h"
#include "CppUnitTest.h"
#include "V2/CaptureInteropV2Validation.h"

#include <cstddef>
#include <type_traits>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2::Api;

namespace
{
    struct ValidConfigFixture
    {
        CtCaptureV2_SourceConfig sources[2]{};
        CtCaptureV2_AudioGainConfig gains[1]{};
        CtCaptureV2_Config config{};

        ValidConfigFixture()
        {
            CtCaptureV2_InitSourceConfig(&sources[0]);
            sources[0].sourceId = 1;
            sources[0].sourceKind = CtCaptureV2_SourceKind_Desktop;
            sources[0].captureRect = CtCaptureV2_Rect{ 0, 0, 1920, 1080 };
            sources[0].enabled = 1;

            CtCaptureV2_InitSourceConfig(&sources[1]);
            sources[1].sourceId = 2;
            sources[1].sourceKind = CtCaptureV2_SourceKind_SystemAudio;
            sources[1].enabled = 1;

            CtCaptureV2_InitAudioGainConfig(&gains[0]);
            gains[0].sourceId = 2;
            gains[0].gainDb = 0.0F;

            CtCaptureV2_InitConfig(&config);
            config.sources = sources;
            config.sourceCount = 2;
            config.output.outputPath = u"C:\\Temp\\capture-v2.mp4";
            config.output.containerFormat = CtCaptureV2_ContainerFormat_Mp4;
            config.output.video.codec = CtCaptureV2_VideoCodec_H264;
            config.output.video.bitrate = 8'000'000;
            config.output.video.frameRateNumerator = 60;
            config.output.video.frameRateDenominator = 1;
            config.output.video.gopLength = 120;
            config.output.video.hardwareAccelerationPreferred = 1;
            config.output.audio.codec = CtCaptureV2_AudioCodec_Aac;
            config.output.audio.bitrate = 192'000;
            config.output.audio.sampleRate = 48'000;
            config.output.audio.channels = 2;
            config.controls.audioGains = gains;
            config.controls.audioGainCount = 1;
        }
    };
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2NativeDtoValidationTests)
    {
    public:
        TEST_METHOD(Dtos_StartWithSizeAndVersion)
        {
            Assert::AreEqual(static_cast<size_t>(0), offsetof(CtCaptureV2_Config, size));
            Assert::AreEqual(sizeof(uint32_t), offsetof(CtCaptureV2_Config, version));
            Assert::AreEqual(static_cast<size_t>(0), offsetof(CtCaptureV2_SourceConfig, size));
            Assert::AreEqual(sizeof(uint32_t), offsetof(CtCaptureV2_SourceConfig, version));
            Assert::AreEqual(static_cast<size_t>(0), offsetof(CtCaptureV2_OutputConfig, size));
            Assert::AreEqual(sizeof(uint32_t), offsetof(CtCaptureV2_OutputConfig, version));
            Assert::AreEqual(static_cast<size_t>(0), offsetof(CtCaptureV2_StopResult, size));
            Assert::AreEqual(sizeof(uint32_t), offsetof(CtCaptureV2_StopResult, version));
            Assert::AreEqual(static_cast<size_t>(0), offsetof(CtCaptureV2_Event, size));
            Assert::AreEqual(sizeof(uint32_t), offsetof(CtCaptureV2_Event, version));
            Assert::AreEqual(static_cast<size_t>(0), offsetof(CtCaptureV2_CallbackConfig, size));
            Assert::AreEqual(sizeof(uint32_t), offsetof(CtCaptureV2_CallbackConfig, version));
        }

        TEST_METHOD(Dtos_ArePlainAbiData)
        {
            Assert::IsTrue(std::is_standard_layout_v<CtCaptureV2_Config>);
            Assert::IsTrue(std::is_trivially_copyable_v<CtCaptureV2_Config>);
            Assert::IsTrue(std::is_standard_layout_v<CtCaptureV2_StopResult>);
            Assert::IsTrue(std::is_trivially_copyable_v<CtCaptureV2_StopResult>);
            Assert::IsTrue(std::is_standard_layout_v<CtCaptureV2_Event>);
            Assert::IsTrue(std::is_trivially_copyable_v<CtCaptureV2_Event>);
            Assert::IsTrue(std::is_standard_layout_v<CtCaptureV2_CallbackConfig>);
            Assert::IsTrue(std::is_trivially_copyable_v<CtCaptureV2_CallbackConfig>);
            Assert::AreEqual(sizeof(uint8_t), sizeof(((CtCaptureV2_SourceConfig*)nullptr)->enabled));
            Assert::AreEqual(sizeof(uint8_t), sizeof(((CtCaptureV2_ControlConfig*)nullptr)->startMuted));
        }

        TEST_METHOD(Initializers_SetSizeAndVersion)
        {
            CtCaptureV2_Config config{};
            CtCaptureV2_StopResult stopResult{};
            CtCaptureV2_Event eventData{};
            CtCaptureV2_CallbackConfig callbackConfig{};

            CtCaptureV2_InitConfig(&config);
            CtCaptureV2_InitStopResult(&stopResult);
            CtCaptureV2_InitEvent(&eventData);
            CtCaptureV2_InitCallbackConfig(&callbackConfig);

            Assert::AreEqual(static_cast<uint32_t>(sizeof(CtCaptureV2_Config)), config.size);
            Assert::AreEqual(CtCaptureV2_DtoVersion, config.version);
            Assert::AreEqual(static_cast<uint32_t>(sizeof(CtCaptureV2_OutputConfig)), config.output.size);
            Assert::AreEqual(static_cast<uint32_t>(sizeof(CtCaptureV2_StopResult)), stopResult.size);
            Assert::AreEqual(CtCaptureV2_DtoVersion, stopResult.version);
            Assert::AreEqual(static_cast<uint32_t>(sizeof(CtCaptureV2_Event)), eventData.size);
            Assert::AreEqual(CtCaptureV2_DtoVersion, eventData.version);
            Assert::AreEqual(static_cast<uint32_t>(sizeof(CtCaptureV2_CallbackConfig)), callbackConfig.size);
            Assert::AreEqual(CtCaptureV2_DtoVersion, callbackConfig.version);
        }

        TEST_METHOD(ValidateConfig_ValidDesktopMp4Config_Succeeds)
        {
            ValidConfigFixture fixture;

            const int32_t result = ValidateConfig(&fixture.config);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_Success), result);
        }

        TEST_METHOD(ValidateConfig_NullConfig_ReturnsInvalidArgument)
        {
            const int32_t result = ValidateConfig(nullptr);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidArgument), result);
        }

        TEST_METHOD(ValidateConfig_MissingSize_ReturnsInvalidArgument)
        {
            ValidConfigFixture fixture;
            fixture.config.size = 0;

            const int32_t result = ValidateConfig(&fixture.config);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidArgument), result);
        }

        TEST_METHOD(ValidateConfig_UnsupportedVersion_ReturnsUnsupportedVersion)
        {
            ValidConfigFixture fixture;
            fixture.config.version = 99;

            const int32_t result = ValidateConfig(&fixture.config);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_UnsupportedVersion), result);
        }

        TEST_METHOD(ValidateConfig_NonZeroReservedField_FailsValidation)
        {
            ValidConfigFixture fixture;
            fixture.config.reserved = 1;

            const int32_t result = ValidateConfig(&fixture.config);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_ValidationFailed), result);
        }

        TEST_METHOD(ValidateConfig_DuplicateSourceIds_FailsValidation)
        {
            ValidConfigFixture fixture;
            fixture.sources[1].sourceId = fixture.sources[0].sourceId;

            const int32_t result = ValidateConfig(&fixture.config);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_ValidationFailed), result);
        }

        TEST_METHOD(ValidateConfig_EmptyOutputPath_FailsValidation)
        {
            ValidConfigFixture fixture;
            fixture.config.output.outputPath = u"";

            const int32_t result = ValidateConfig(&fixture.config);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_ValidationFailed), result);
        }

        TEST_METHOD(ValidateConfig_InvalidDesktopCaptureRectangle_FailsValidation)
        {
            ValidConfigFixture fixture;
            fixture.sources[0].captureRect.width = -1;

            const int32_t result = ValidateConfig(&fixture.config);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_ValidationFailed), result);
        }

        TEST_METHOD(ValidateConfig_InvalidEnumValue_FailsValidation)
        {
            ValidConfigFixture fixture;
            fixture.config.output.containerFormat = 999;

            const int32_t result = ValidateConfig(&fixture.config);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_ValidationFailed), result);
        }

        TEST_METHOD(ValidateConfig_MissingOutputStreams_FailsValidation)
        {
            ValidConfigFixture fixture;
            fixture.config.output.video.codec = CtCaptureV2_VideoCodec_None;
            fixture.config.output.audio.codec = CtCaptureV2_AudioCodec_None;

            const int32_t result = ValidateConfig(&fixture.config);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_ValidationFailed), result);
        }

        TEST_METHOD(ValidateConfig_AudioGainOutsideRange_FailsValidation)
        {
            ValidConfigFixture fixture;
            fixture.gains[0].gainDb = 13.0F;

            const int32_t result = ValidateConfig(&fixture.config);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_ValidationFailed), result);
        }
    };
}
