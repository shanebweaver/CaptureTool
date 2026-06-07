#include "pch.h"
#include "CppUnitTest.h"
#include "V2/CaptureInteropV2Exports.h"

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

    struct ValidLifecycleConfig
    {
        CtCaptureV2_SourceConfig sources[2]{};
        CtCaptureV2_AudioGainConfig gains[1]{};
        CtCaptureV2_Config config{};

        ValidLifecycleConfig()
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
    TEST_CLASS(V2NativeLifecycleCommandTests)
    {
    public:
        TEST_METHOD(Start_WithValidConfig_SucceedsAndStopPopulatesResult)
        {
            RecorderFixture recorder;
            ValidLifecycleConfig config;
            CtCaptureV2_StopResult stopResult{};

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_Start(recorder.handle, &config.config));
            const int32_t stopCode = CtCaptureV2_Stop(recorder.handle, &stopResult);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_Success), stopCode);
            Assert::AreEqual(static_cast<uint32_t>(sizeof(CtCaptureV2_StopResult)), stopResult.size);
            Assert::AreEqual(CtCaptureV2_DtoVersion, stopResult.version);
            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_Success), stopResult.resultCode);
        }

        TEST_METHOD(Start_WhileRecording_ReturnsAlreadyStarted)
        {
            RecorderFixture recorder;
            ValidLifecycleConfig config;

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_Start(recorder.handle, &config.config));

            const int32_t result = CtCaptureV2_Start(recorder.handle, &config.config);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_AlreadyStarted), result);
        }

        TEST_METHOD(Start_NullConfig_ReturnsInvalidArgument)
        {
            RecorderFixture recorder;

            const int32_t result = CtCaptureV2_Start(recorder.handle, nullptr);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidArgument), result);
        }

        TEST_METHOD(Start_UnsupportedConfigVersion_ReturnsUnsupportedVersion)
        {
            RecorderFixture recorder;
            ValidLifecycleConfig config;
            config.config.version = 99;

            const int32_t result = CtCaptureV2_Start(recorder.handle, &config.config);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_UnsupportedVersion), result);
        }

        TEST_METHOD(PauseResume_ValidTransitions_Succeed)
        {
            RecorderFixture recorder;
            ValidLifecycleConfig config;

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_Start(recorder.handle, &config.config));

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_Pause(recorder.handle));
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_Resume(recorder.handle));
        }

        TEST_METHOD(Pause_WhileIdle_ReturnsInvalidState)
        {
            RecorderFixture recorder;

            const int32_t result = CtCaptureV2_Pause(recorder.handle);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidState), result);
        }

        TEST_METHOD(AudioCommands_WhileIdle_ReturnInvalidState)
        {
            RecorderFixture recorder;

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidState),
                CtCaptureV2_SetAudioMuted(recorder.handle, 2, 1));
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidState),
                CtCaptureV2_SetAudioGain(recorder.handle, 2, 0.0F));
        }

        TEST_METHOD(AudioCommands_TargetArmedSourceWhileRecordingOrPaused_Succeed)
        {
            RecorderFixture recorder;
            ValidLifecycleConfig config;

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_Start(recorder.handle, &config.config));

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_SetAudioMuted(recorder.handle, 2, 1));
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_SetAudioGain(recorder.handle, 2, -6.0F));
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_Pause(recorder.handle));
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_SetAudioMuted(recorder.handle, 2, 0));
        }

        TEST_METHOD(AudioCommands_MissingSource_ReturnNotFound)
        {
            RecorderFixture recorder;
            ValidLifecycleConfig config;

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_Start(recorder.handle, &config.config));

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_NotFound),
                CtCaptureV2_SetAudioMuted(recorder.handle, 99, 1));
        }

        TEST_METHOD(AudioGain_OutOfRange_ReturnsValidationFailed)
        {
            RecorderFixture recorder;
            ValidLifecycleConfig config;

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_Start(recorder.handle, &config.config));

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_ValidationFailed),
                CtCaptureV2_SetAudioGain(recorder.handle, 2, 13.0F));
        }

        TEST_METHOD(Stop_WhileIdle_ReturnsAlreadyStoppedAndPopulatesResult)
        {
            RecorderFixture recorder;
            CtCaptureV2_StopResult stopResult{};

            const int32_t result = CtCaptureV2_Stop(recorder.handle, &stopResult);

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_AlreadyStopped), result);
            Assert::AreEqual(static_cast<uint32_t>(sizeof(CtCaptureV2_StopResult)), stopResult.size);
            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_AlreadyStopped), stopResult.resultCode);
        }

        TEST_METHOD(Start_CopiesBorrowedConfigDataBeforeReturning)
        {
            RecorderFixture recorder;
            ValidLifecycleConfig config;

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_Start(recorder.handle, &config.config));

            config.config.output.outputPath = nullptr;
            config.config.sources = nullptr;
            config.config.sourceCount = 0;

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_Pause(recorder.handle));
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_Resume(recorder.handle));
        }
    };
}
