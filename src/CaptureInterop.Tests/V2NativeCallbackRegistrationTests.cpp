#include "pch.h"
#include "CppUnitTest.h"
#include "V2/CaptureInteropV2Exports.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace
{
    constexpr uint64_t EventMask(int32_t eventType) noexcept
    {
        return 1ULL << eventType;
    }

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

    struct CallbackState
    {
        uint32_t invocationCount{ 0 };
        uint64_t lastSequence{ 0 };
        int32_t lastEventType{ CtCaptureV2_EventType_Unknown };
    };

    struct ReentrantCallbackState
    {
        CtCaptureV2_RecorderHandle recorder{ nullptr };
        int32_t pauseResult{ CtCaptureV2_ResultCode_NativeFailure };
    };

    void CTCAPTUREV2_CALL CountingCallback(const CtCaptureV2_Event* eventData, void* userData) noexcept
    {
        auto* state = static_cast<CallbackState*>(userData);
        ++state->invocationCount;
        state->lastSequence = eventData->sequence;
        state->lastEventType = eventData->eventType;
    }

    void CTCAPTUREV2_CALL ReentrantPauseCallback(const CtCaptureV2_Event*, void* userData) noexcept
    {
        auto* state = static_cast<ReentrantCallbackState*>(userData);
        state->pauseResult = CtCaptureV2_Pause(state->recorder);
    }

    CtCaptureV2_Event StateChangedEvent(uint64_t sequence = 1)
    {
        CtCaptureV2_Event eventData{};
        CtCaptureV2_InitEvent(&eventData);
        eventData.eventType = CtCaptureV2_EventType_StateChanged;
        eventData.resultCode = CtCaptureV2_ResultCode_Success;
        eventData.sequence = sequence;
        return eventData;
    }

    CtCaptureV2_CallbackConfig CallbackConfig(
        CtCaptureV2_EventCallback callback,
        void* userData,
        uint64_t eventMask = EventMask(CtCaptureV2_EventType_StateChanged))
    {
        CtCaptureV2_CallbackConfig config{};
        CtCaptureV2_InitCallbackConfig(&config);
        config.callback = callback;
        config.userData = userData;
        config.eventMask = eventMask;
        return config;
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2NativeCallbackRegistrationTests)
    {
    public:
        TEST_METHOD(RegisterCallbacks_ValidConfig_ReturnsOpaqueHandleAndReceivesEvents)
        {
            RecorderFixture recorder;
            CallbackState state;
            CtCaptureV2_CallbackRegistrationHandle registration = nullptr;
            CtCaptureV2_CallbackConfig config = CallbackConfig(CountingCallback, &state);
            CtCaptureV2_Event eventData = StateChangedEvent(42);

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_RegisterCallbacks(recorder.handle, &config, &registration));
            Assert::IsTrue(registration != nullptr);

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_TestTriggerEvent(recorder.handle, &eventData));

            Assert::AreEqual(static_cast<uint32_t>(1), state.invocationCount);
            Assert::AreEqual(static_cast<uint64_t>(42), state.lastSequence);
            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_EventType_StateChanged), state.lastEventType);

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_UnregisterCallbacks(registration));
        }

        TEST_METHOD(UnregisterCallbacks_PreventsFutureInvocations)
        {
            RecorderFixture recorder;
            CallbackState state;
            CtCaptureV2_CallbackRegistrationHandle registration = nullptr;
            CtCaptureV2_CallbackConfig config = CallbackConfig(CountingCallback, &state);
            CtCaptureV2_Event eventData = StateChangedEvent();

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_RegisterCallbacks(recorder.handle, &config, &registration));
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_UnregisterCallbacks(registration));

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_TestTriggerEvent(recorder.handle, &eventData));

            Assert::AreEqual(static_cast<uint32_t>(0), state.invocationCount);
        }

        TEST_METHOD(RegisterCallbacks_AfterRecorderDestroy_ReturnsInvalidHandle)
        {
            CtCaptureV2_RecorderHandle recorder = nullptr;
            CallbackState state;
            CtCaptureV2_CallbackRegistrationHandle registration = nullptr;
            CtCaptureV2_CallbackConfig config = CallbackConfig(CountingCallback, &state);

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_CreateRecorder(&recorder));
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_DestroyRecorder(recorder));

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidHandle),
                CtCaptureV2_RegisterCallbacks(recorder, &config, &registration));
            Assert::IsTrue(registration == nullptr);
        }

        TEST_METHOD(DestroyRecorder_UnregistersRemainingCallbacks)
        {
            CtCaptureV2_RecorderHandle recorder = nullptr;
            CallbackState state;
            CtCaptureV2_CallbackRegistrationHandle registration = nullptr;
            CtCaptureV2_CallbackConfig config = CallbackConfig(CountingCallback, &state);

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_CreateRecorder(&recorder));
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_RegisterCallbacks(recorder, &config, &registration));
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_DestroyRecorder(recorder));

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidHandle),
                CtCaptureV2_UnregisterCallbacks(registration));
        }

        TEST_METHOD(Stop_UnregistersRemainingCallbacks)
        {
            RecorderFixture recorder;
            ValidLifecycleConfig lifecycleConfig;
            CallbackState state;
            CtCaptureV2_CallbackRegistrationHandle registration = nullptr;
            CtCaptureV2_CallbackConfig config = CallbackConfig(CountingCallback, &state);
            CtCaptureV2_Event eventData = StateChangedEvent();
            CtCaptureV2_StopResult stopResult{};

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_Start(recorder.handle, &lifecycleConfig.config));
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_RegisterCallbacks(recorder.handle, &config, &registration));
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_Stop(recorder.handle, &stopResult));

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_TestTriggerEvent(recorder.handle, &eventData));
            Assert::AreEqual(static_cast<uint32_t>(0), state.invocationCount);
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidHandle),
                CtCaptureV2_UnregisterCallbacks(registration));
        }

        TEST_METHOD(Stop_WhileIdle_UnregistersRemainingCallbacks)
        {
            RecorderFixture recorder;
            CallbackState state;
            CtCaptureV2_CallbackRegistrationHandle registration = nullptr;
            CtCaptureV2_CallbackConfig config = CallbackConfig(CountingCallback, &state);
            CtCaptureV2_StopResult stopResult{};

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_RegisterCallbacks(recorder.handle, &config, &registration));
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_AlreadyStopped),
                CtCaptureV2_Stop(recorder.handle, &stopResult));

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_InvalidHandle),
                CtCaptureV2_UnregisterCallbacks(registration));
        }

        TEST_METHOD(TestTriggerEvent_CopiesCallbacksBeforeInvocation)
        {
            RecorderFixture recorder;
            ValidLifecycleConfig lifecycleConfig;
            ReentrantCallbackState state{ recorder.handle };
            CtCaptureV2_CallbackRegistrationHandle registration = nullptr;
            CtCaptureV2_CallbackConfig config = CallbackConfig(ReentrantPauseCallback, &state);
            CtCaptureV2_Event eventData = StateChangedEvent();

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_Start(recorder.handle, &lifecycleConfig.config));
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_RegisterCallbacks(recorder.handle, &config, &registration));

            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_TestTriggerEvent(recorder.handle, &eventData));

            Assert::AreEqual(static_cast<int32_t>(CtCaptureV2_ResultCode_Success), state.pauseResult);
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_Resume(recorder.handle));
            Assert::AreEqual(
                static_cast<int32_t>(CtCaptureV2_ResultCode_Success),
                CtCaptureV2_UnregisterCallbacks(registration));
        }
    };
}
