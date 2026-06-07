#define CTCAPTUREV2_BUILD
#include "CaptureInteropV2Exports.h"
#include "CaptureInteropV2Validation.h"

#include <algorithm>
#include <memory>
#include <mutex>
#include <optional>
#include <string>
#include <unordered_set>
#include <vector>

namespace
{
    enum class RecorderState : int32_t
    {
        Idle = 0,
        Recording = 1,
        Paused = 2
    };

    struct CopiedSourceConfig
    {
        uint32_t sourceId{ 0 };
        int32_t sourceKind{ CtCaptureV2_SourceKind_Unknown };
        uint8_t enabled{ 0 };
    };

    struct CopiedAudioGainConfig
    {
        uint32_t sourceId{ 0 };
        float gainDb{ 0.0F };
    };

    struct CopiedRecorderConfig
    {
        std::vector<CopiedSourceConfig> sources;
        std::u16string outputPath;
        int32_t containerFormat{ CtCaptureV2_ContainerFormat_Unknown };
        int32_t videoCodec{ CtCaptureV2_VideoCodec_None };
        int32_t audioCodec{ CtCaptureV2_AudioCodec_None };
        uint8_t startMuted{ 0 };
        std::vector<CopiedAudioGainConfig> audioGains;
    };

    struct LastErrorState
    {
        int32_t resultCode{ CtCaptureV2_ResultCode_Success };
        int32_t errorCode{ 0 };
        int32_t nativeStatus{ 0 };
        int32_t stage{ 0 };
        std::string component;
        std::string operation;
        std::u16string message;
    };

    struct CopiedCallback
    {
        CtCaptureV2_EventCallback callback{ nullptr };
        void* userData{ nullptr };
    };
}

struct CtCaptureV2_CallbackRegistration_t
{
    CtCaptureV2_RecorderHandle recorder{ nullptr };
    CtCaptureV2_EventCallback callback{ nullptr };
    void* userData{ nullptr };
    uint64_t eventMask{ 0 };
};

struct CtCaptureV2_Recorder_t
{
    RecorderState state{ RecorderState::Idle };
    std::optional<CopiedRecorderConfig> activeConfig;
    std::vector<CopiedAudioGainConfig> runtimeGains;
    std::vector<uint32_t> mutedAudioSources;
    std::vector<CtCaptureV2_CallbackRegistrationHandle> callbackRegistrations;
    LastErrorState lastError;
};

namespace
{
    constexpr uint32_t ApiVersionDtoVersion = 1;
    constexpr uint32_t ApiVersionMajor = 2;
    constexpr uint32_t ApiVersionMinor = 0;
    constexpr uint32_t ApiVersionPatch = 0;

    std::mutex RecorderRegistryMutex;
    std::unordered_set<CtCaptureV2_RecorderHandle> RecorderRegistry;
    std::unordered_set<CtCaptureV2_CallbackRegistrationHandle> CallbackRegistrationRegistry;
    constexpr const char* RecorderComponent = "CaptureInteropV2Recorder";

    CtCaptureV2_Recorder_t* FindRecorder(CtCaptureV2_RecorderHandle handle) noexcept
    {
        if (handle == nullptr)
        {
            return nullptr;
        }

        std::lock_guard lock(RecorderRegistryMutex);
        return RecorderRegistry.find(handle) == RecorderRegistry.end() ? nullptr : handle;
    }

    bool IsEventTypeEnabled(uint64_t eventMask, int32_t eventType) noexcept
    {
        if (eventType <= CtCaptureV2_EventType_Unknown || eventType >= 64)
        {
            return false;
        }

        return (eventMask & (1ULL << eventType)) != 0;
    }

    void RemoveRegistrationFromRecorder(
        CtCaptureV2_Recorder_t& recorder,
        CtCaptureV2_CallbackRegistrationHandle registration)
    {
        recorder.callbackRegistrations.erase(
            std::remove(
                recorder.callbackRegistrations.begin(),
                recorder.callbackRegistrations.end(),
                registration),
            recorder.callbackRegistrations.end());
    }

    void DeleteRegistrations(std::vector<CtCaptureV2_CallbackRegistrationHandle>& registrations) noexcept
    {
        for (CtCaptureV2_CallbackRegistrationHandle registration : registrations)
        {
            delete registration;
        }

        registrations.clear();
    }

    void UnregisterAllCallbacksLocked(
        CtCaptureV2_Recorder_t& recorder,
        std::vector<CtCaptureV2_CallbackRegistrationHandle>& registrationsToDelete)
    {
        registrationsToDelete.insert(
            registrationsToDelete.end(),
            recorder.callbackRegistrations.begin(),
            recorder.callbackRegistrations.end());

        for (CtCaptureV2_CallbackRegistrationHandle registration : recorder.callbackRegistrations)
        {
            CallbackRegistrationRegistry.erase(registration);
        }

        recorder.callbackRegistrations.clear();
    }

    std::u16string CopyNullTerminatedString(const char16_t* value)
    {
        return value == nullptr ? std::u16string{} : std::u16string{ value };
    }

    CopiedRecorderConfig CopyConfig(const CtCaptureV2_Config& config)
    {
        CopiedRecorderConfig copy;
        copy.outputPath = CopyNullTerminatedString(config.output.outputPath);
        copy.containerFormat = config.output.containerFormat;
        copy.videoCodec = config.output.video.codec;
        copy.audioCodec = config.output.audio.codec;
        copy.startMuted = config.controls.startMuted;
        copy.sources.reserve(config.sourceCount);
        for (uint32_t index = 0; index < config.sourceCount; ++index)
        {
            copy.sources.push_back(CopiedSourceConfig{
                config.sources[index].sourceId,
                config.sources[index].sourceKind,
                config.sources[index].enabled
            });
        }

        copy.audioGains.reserve(config.controls.audioGainCount);
        for (uint32_t index = 0; index < config.controls.audioGainCount; ++index)
        {
            copy.audioGains.push_back(CopiedAudioGainConfig{
                config.controls.audioGains[index].sourceId,
                config.controls.audioGains[index].gainDb
            });
        }

        return copy;
    }

    bool HasArmedAudioSource(const CtCaptureV2_Recorder_t& recorder, uint32_t sourceId) noexcept
    {
        if (!recorder.activeConfig.has_value())
        {
            return false;
        }

        for (const CopiedSourceConfig& source : recorder.activeConfig->sources)
        {
            if (source.sourceId == sourceId
                && source.sourceKind == CtCaptureV2_SourceKind_SystemAudio
                && source.enabled != 0)
            {
                return true;
            }
        }

        return false;
    }

    void SetRuntimeGain(CtCaptureV2_Recorder_t& recorder, uint32_t sourceId, float gainDb)
    {
        for (CopiedAudioGainConfig& gain : recorder.runtimeGains)
        {
            if (gain.sourceId == sourceId)
            {
                gain.gainDb = gainDb;
                return;
            }
        }

        recorder.runtimeGains.push_back(CopiedAudioGainConfig{ sourceId, gainDb });
    }

    void SetRuntimeMuted(CtCaptureV2_Recorder_t& recorder, uint32_t sourceId, bool muted)
    {
        for (auto iterator = recorder.mutedAudioSources.begin(); iterator != recorder.mutedAudioSources.end(); ++iterator)
        {
            if (*iterator == sourceId)
            {
                if (!muted)
                {
                    recorder.mutedAudioSources.erase(iterator);
                }

                return;
            }
        }

        if (muted)
        {
            recorder.mutedAudioSources.push_back(sourceId);
        }
    }

    CtCaptureV2_StopResult MakeStopResult(int32_t resultCode, RecorderState finalState) noexcept
    {
        CtCaptureV2_StopResult result;
        CtCaptureV2_InitStopResult(&result);
        result.resultCode = resultCode;
        result.finalState = static_cast<int32_t>(finalState);
        return result;
    }

    void ClearLastError(CtCaptureV2_Recorder_t& recorder)
    {
        recorder.lastError = LastErrorState{};
    }

    int32_t RecordFailure(
        CtCaptureV2_Recorder_t& recorder,
        int32_t resultCode,
        const char* operation,
        const char16_t* message,
        int32_t nativeStatus = 0,
        int32_t stage = 0)
    {
        recorder.lastError.resultCode = resultCode;
        recorder.lastError.errorCode = resultCode;
        recorder.lastError.nativeStatus = nativeStatus;
        recorder.lastError.stage = stage;
        recorder.lastError.component = RecorderComponent;
        recorder.lastError.operation = operation == nullptr ? "" : operation;
        recorder.lastError.message = message == nullptr ? u"" : message;
        return resultCode;
    }

    int32_t RecordNativeFailure(
        CtCaptureV2_Recorder_t* recorder,
        const char* operation) noexcept
    {
        if (recorder != nullptr)
        {
            return RecordFailure(
                *recorder,
                CtCaptureV2_ResultCode_NativeFailure,
                operation,
                u"An unexpected native failure occurred.");
        }

        return CtCaptureV2_ResultCode_NativeFailure;
    }

    uint32_t RequiredMessageLength(const std::u16string& message) noexcept
    {
        return static_cast<uint32_t>(message.size() + 1);
    }

    void CopyMessage(const std::u16string& message, char16_t* buffer, uint32_t bufferLength) noexcept
    {
        if (buffer == nullptr || bufferLength == 0)
        {
            return;
        }

        uint32_t index = 0;
        for (; index < message.size() && index + 1 < bufferLength; ++index)
        {
            buffer[index] = message[index];
        }

        buffer[index] = u'\0';
    }
}

extern "C"
{
    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_GetApiVersion(
        CtCaptureV2_ApiVersion* outVersion) noexcept
    {
        if (outVersion == nullptr)
        {
            return CtCaptureV2_ResultCode_InvalidArgument;
        }

        *outVersion = CtCaptureV2_ApiVersion{
            sizeof(CtCaptureV2_ApiVersion),
            ApiVersionDtoVersion,
            ApiVersionMajor,
            ApiVersionMinor,
            ApiVersionPatch,
            0
        };

        return CtCaptureV2_ResultCode_Success;
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_CreateRecorder(
        CtCaptureV2_RecorderHandle* outHandle) noexcept
    {
        if (outHandle == nullptr)
        {
            return CtCaptureV2_ResultCode_InvalidArgument;
        }

        *outHandle = nullptr;

        try
        {
            auto recorder = std::make_unique<CtCaptureV2_Recorder_t>();
            CtCaptureV2_RecorderHandle handle = recorder.get();

            {
                std::lock_guard lock(RecorderRegistryMutex);
                RecorderRegistry.insert(handle);
            }

            *outHandle = recorder.release();
            ClearLastError(**outHandle);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return CtCaptureV2_ResultCode_NativeFailure;
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_DestroyRecorder(
        CtCaptureV2_RecorderHandle handle) noexcept
    {
        if (handle == nullptr)
        {
            return CtCaptureV2_ResultCode_Success;
        }

        try
        {
            std::vector<CtCaptureV2_CallbackRegistrationHandle> registrationsToDelete;

            {
                std::lock_guard lock(RecorderRegistryMutex);
                const auto found = RecorderRegistry.find(handle);
                if (found == RecorderRegistry.end())
                {
                    return CtCaptureV2_ResultCode_InvalidHandle;
                }

                RecorderRegistry.erase(found);
                UnregisterAllCallbacksLocked(*handle, registrationsToDelete);
            }

            DeleteRegistrations(registrationsToDelete);
            delete handle;
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return CtCaptureV2_ResultCode_NativeFailure;
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_Start(
        CtCaptureV2_RecorderHandle handle,
        const CtCaptureV2_Config* config) noexcept
    {
        try
        {
            CtCaptureV2_Recorder_t* recorder = FindRecorder(handle);
            if (recorder == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidHandle;
            }

            if (recorder->state != RecorderState::Idle)
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_AlreadyStarted,
                    "Start",
                    u"Recorder is already started.");
            }

            const int32_t validationResult = CaptureInterop::V2::Api::ValidateConfig(config);
            if (validationResult != CtCaptureV2_ResultCode_Success)
            {
                return RecordFailure(
                    *recorder,
                    validationResult,
                    "Start",
                    u"Recorder configuration validation failed.");
            }

            recorder->activeConfig = CopyConfig(*config);
            recorder->runtimeGains = recorder->activeConfig->audioGains;
            recorder->mutedAudioSources.clear();
            if (recorder->activeConfig->startMuted != 0)
            {
                for (const CopiedSourceConfig& source : recorder->activeConfig->sources)
                {
                    if (source.sourceKind == CtCaptureV2_SourceKind_SystemAudio && source.enabled != 0)
                    {
                        recorder->mutedAudioSources.push_back(source.sourceId);
                    }
                }
            }

            recorder->state = RecorderState::Recording;
            ClearLastError(*recorder);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return RecordNativeFailure(FindRecorder(handle), "Start");
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_Pause(
        CtCaptureV2_RecorderHandle handle) noexcept
    {
        try
        {
            CtCaptureV2_Recorder_t* recorder = FindRecorder(handle);
            if (recorder == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidHandle;
            }

            if (recorder->state != RecorderState::Recording)
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_InvalidState,
                    "Pause",
                    u"Recorder cannot be paused from the current state.");
            }

            recorder->state = RecorderState::Paused;
            ClearLastError(*recorder);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return RecordNativeFailure(FindRecorder(handle), "Pause");
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_Resume(
        CtCaptureV2_RecorderHandle handle) noexcept
    {
        try
        {
            CtCaptureV2_Recorder_t* recorder = FindRecorder(handle);
            if (recorder == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidHandle;
            }

            if (recorder->state != RecorderState::Paused)
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_InvalidState,
                    "Resume",
                    u"Recorder cannot be resumed from the current state.");
            }

            recorder->state = RecorderState::Recording;
            ClearLastError(*recorder);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return RecordNativeFailure(FindRecorder(handle), "Resume");
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_SetAudioMuted(
        CtCaptureV2_RecorderHandle handle,
        uint32_t sourceId,
        uint8_t muted) noexcept
    {
        try
        {
            CtCaptureV2_Recorder_t* recorder = FindRecorder(handle);
            if (recorder == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidHandle;
            }

            if (muted != 0 && muted != 1)
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_InvalidArgument,
                    "SetAudioMuted",
                    u"Muted must be 0 or 1.");
            }

            if (recorder->state != RecorderState::Recording && recorder->state != RecorderState::Paused)
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_InvalidState,
                    "SetAudioMuted",
                    u"Recorder cannot update audio mute from the current state.");
            }

            if (!HasArmedAudioSource(*recorder, sourceId))
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_NotFound,
                    "SetAudioMuted",
                    u"No armed audio source was found for the requested source id.");
            }

            SetRuntimeMuted(*recorder, sourceId, muted != 0);
            ClearLastError(*recorder);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return RecordNativeFailure(FindRecorder(handle), "SetAudioMuted");
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_SetAudioGain(
        CtCaptureV2_RecorderHandle handle,
        uint32_t sourceId,
        float gainDb) noexcept
    {
        try
        {
            CtCaptureV2_Recorder_t* recorder = FindRecorder(handle);
            if (recorder == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidHandle;
            }

            if (recorder->state != RecorderState::Recording && recorder->state != RecorderState::Paused)
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_InvalidState,
                    "SetAudioGain",
                    u"Recorder cannot update audio gain from the current state.");
            }

            if (gainDb < CaptureInterop::V2::Api::MinAudioGainDb
                || gainDb > CaptureInterop::V2::Api::MaxAudioGainDb)
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_ValidationFailed,
                    "SetAudioGain",
                    u"Audio gain is outside the supported range.");
            }

            if (!HasArmedAudioSource(*recorder, sourceId))
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_NotFound,
                    "SetAudioGain",
                    u"No armed audio source was found for the requested source id.");
            }

            SetRuntimeGain(*recorder, sourceId, gainDb);
            ClearLastError(*recorder);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return RecordNativeFailure(FindRecorder(handle), "SetAudioGain");
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_Stop(
        CtCaptureV2_RecorderHandle handle,
        CtCaptureV2_StopResult* result) noexcept
    {
        try
        {
            if (result == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidArgument;
            }

            CtCaptureV2_Recorder_t* recorder = FindRecorder(handle);
            if (recorder == nullptr)
            {
                *result = MakeStopResult(CtCaptureV2_ResultCode_InvalidHandle, RecorderState::Idle);
                return CtCaptureV2_ResultCode_InvalidHandle;
            }

            std::vector<CtCaptureV2_CallbackRegistrationHandle> registrationsToDelete;

            if (recorder->state == RecorderState::Idle)
            {
                {
                    std::lock_guard lock(RecorderRegistryMutex);
                    UnregisterAllCallbacksLocked(*recorder, registrationsToDelete);
                }

                *result = MakeStopResult(CtCaptureV2_ResultCode_AlreadyStopped, RecorderState::Idle);
                const int32_t failure = RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_AlreadyStopped,
                    "Stop",
                    u"Recorder is already stopped.");
                DeleteRegistrations(registrationsToDelete);
                return failure;
            }

            recorder->state = RecorderState::Idle;
            recorder->activeConfig.reset();
            recorder->runtimeGains.clear();
            recorder->mutedAudioSources.clear();
            {
                std::lock_guard lock(RecorderRegistryMutex);
                UnregisterAllCallbacksLocked(*recorder, registrationsToDelete);
            }

            *result = MakeStopResult(CtCaptureV2_ResultCode_Success, RecorderState::Idle);
            ClearLastError(*recorder);
            DeleteRegistrations(registrationsToDelete);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            if (result != nullptr)
            {
                *result = MakeStopResult(CtCaptureV2_ResultCode_NativeFailure, RecorderState::Idle);
            }

            return RecordNativeFailure(FindRecorder(handle), "Stop");
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_GetLastError(
        CtCaptureV2_RecorderHandle handle,
        CtCaptureV2_ErrorInfo* errorInfo,
        char16_t* messageBuffer,
        uint32_t messageBufferLength,
        uint32_t* requiredMessageLength) noexcept
    {
        try
        {
            CtCaptureV2_Recorder_t* recorder = FindRecorder(handle);
            if (recorder == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidHandle;
            }

            if (errorInfo == nullptr || requiredMessageLength == nullptr)
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_InvalidArgument,
                    "GetLastError",
                    u"Error info and required message length outputs are required.");
            }

            CtCaptureV2_InitErrorInfo(errorInfo);
            errorInfo->resultCode = recorder->lastError.resultCode;
            errorInfo->errorCode = recorder->lastError.errorCode;
            errorInfo->nativeStatus = recorder->lastError.nativeStatus;
            errorInfo->stage = recorder->lastError.stage;
            errorInfo->component = recorder->lastError.component.c_str();
            errorInfo->operation = recorder->lastError.operation.c_str();

            const uint32_t requiredLength = RequiredMessageLength(recorder->lastError.message);
            *requiredMessageLength = requiredLength;

            if (messageBuffer == nullptr || messageBufferLength < requiredLength)
            {
                return CtCaptureV2_ResultCode_BufferTooSmall;
            }

            CopyMessage(recorder->lastError.message, messageBuffer, messageBufferLength);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return CtCaptureV2_ResultCode_NativeFailure;
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_RegisterCallbacks(
        CtCaptureV2_RecorderHandle handle,
        const CtCaptureV2_CallbackConfig* config,
        CtCaptureV2_CallbackRegistrationHandle* outRegistration) noexcept
    {
        if (outRegistration == nullptr)
        {
            return CtCaptureV2_ResultCode_InvalidArgument;
        }

        *outRegistration = nullptr;

        try
        {
            if (config == nullptr || config->callback == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidArgument;
            }

            if (config->size != sizeof(CtCaptureV2_CallbackConfig))
            {
                return CtCaptureV2_ResultCode_InvalidArgument;
            }

            if (config->version != CtCaptureV2_DtoVersion)
            {
                return CtCaptureV2_ResultCode_UnsupportedVersion;
            }

            auto registration = std::make_unique<CtCaptureV2_CallbackRegistration_t>();
            registration->recorder = handle;
            registration->callback = config->callback;
            registration->userData = config->userData;
            registration->eventMask = config->eventMask;

            CtCaptureV2_RecorderHandle recorder = nullptr;
            {
                std::lock_guard lock(RecorderRegistryMutex);
                const auto recorderFound = RecorderRegistry.find(handle);
                if (recorderFound == RecorderRegistry.end())
                {
                    return CtCaptureV2_ResultCode_InvalidHandle;
                }

                CtCaptureV2_CallbackRegistrationHandle registrationHandle = registration.get();
                CallbackRegistrationRegistry.insert(registrationHandle);
                try
                {
                    (*recorderFound)->callbackRegistrations.push_back(registrationHandle);
                }
                catch (...)
                {
                    CallbackRegistrationRegistry.erase(registrationHandle);
                    throw;
                }

                *outRegistration = registration.release();
                recorder = *recorderFound;
            }

            ClearLastError(*recorder);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return CtCaptureV2_ResultCode_CallbackRegistrationFailed;
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_UnregisterCallbacks(
        CtCaptureV2_CallbackRegistrationHandle registration) noexcept
    {
        if (registration == nullptr)
        {
            return CtCaptureV2_ResultCode_Success;
        }

        try
        {
            {
                std::lock_guard lock(RecorderRegistryMutex);
                const auto registrationFound = CallbackRegistrationRegistry.find(registration);
                if (registrationFound == CallbackRegistrationRegistry.end())
                {
                    return CtCaptureV2_ResultCode_InvalidHandle;
                }

                const auto recorderFound = RecorderRegistry.find(registration->recorder);
                if (recorderFound != RecorderRegistry.end())
                {
                    RemoveRegistrationFromRecorder(**recorderFound, registration);
                }

                CallbackRegistrationRegistry.erase(registrationFound);
            }

            delete registration;
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return CtCaptureV2_ResultCode_NativeFailure;
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_TestTriggerEvent(
        CtCaptureV2_RecorderHandle handle,
        const CtCaptureV2_Event* eventData) noexcept
    {
        try
        {
            if (eventData == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidArgument;
            }

            if (eventData->size != sizeof(CtCaptureV2_Event))
            {
                return CtCaptureV2_ResultCode_InvalidArgument;
            }

            if (eventData->version != CtCaptureV2_DtoVersion)
            {
                return CtCaptureV2_ResultCode_UnsupportedVersion;
            }

            std::vector<CopiedCallback> callbacks;
            {
                std::lock_guard lock(RecorderRegistryMutex);
                const auto recorderFound = RecorderRegistry.find(handle);
                if (recorderFound == RecorderRegistry.end())
                {
                    return CtCaptureV2_ResultCode_InvalidHandle;
                }

                const CtCaptureV2_Recorder_t& recorder = **recorderFound;
                callbacks.reserve(recorder.callbackRegistrations.size());
                for (CtCaptureV2_CallbackRegistrationHandle registration : recorder.callbackRegistrations)
                {
                    if (CallbackRegistrationRegistry.find(registration) != CallbackRegistrationRegistry.end()
                        && IsEventTypeEnabled(registration->eventMask, eventData->eventType))
                    {
                        callbacks.push_back(CopiedCallback{
                            registration->callback,
                            registration->userData
                        });
                    }
                }
            }

            for (const CopiedCallback& callback : callbacks)
            {
                callback.callback(eventData, callback.userData);
            }

            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return CtCaptureV2_ResultCode_CallbackInvocationFailed;
        }
    }
}
