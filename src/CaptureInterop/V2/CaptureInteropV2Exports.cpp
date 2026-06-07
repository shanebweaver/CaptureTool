#define CTCAPTUREV2_BUILD
#include "CaptureInteropV2Exports.h"
#include "CaptureInteropV2Validation.h"

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
}

struct CtCaptureV2_Recorder_t
{
    RecorderState state{ RecorderState::Idle };
    std::optional<CopiedRecorderConfig> activeConfig;
    std::vector<CopiedAudioGainConfig> runtimeGains;
    std::vector<uint32_t> mutedAudioSources;
};

namespace
{
    constexpr uint32_t ApiVersionDtoVersion = 1;
    constexpr uint32_t ApiVersionMajor = 2;
    constexpr uint32_t ApiVersionMinor = 0;
    constexpr uint32_t ApiVersionPatch = 0;

    std::mutex RecorderRegistryMutex;
    std::unordered_set<CtCaptureV2_RecorderHandle> RecorderRegistry;

    CtCaptureV2_Recorder_t* FindRecorder(CtCaptureV2_RecorderHandle handle) noexcept
    {
        if (handle == nullptr)
        {
            return nullptr;
        }

        std::lock_guard lock(RecorderRegistryMutex);
        return RecorderRegistry.find(handle) == RecorderRegistry.end() ? nullptr : handle;
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
            {
                std::lock_guard lock(RecorderRegistryMutex);
                const auto found = RecorderRegistry.find(handle);
                if (found == RecorderRegistry.end())
                {
                    return CtCaptureV2_ResultCode_InvalidHandle;
                }

                RecorderRegistry.erase(found);
            }

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
                return CtCaptureV2_ResultCode_AlreadyStarted;
            }

            const int32_t validationResult = CaptureInterop::V2::Api::ValidateConfig(config);
            if (validationResult != CtCaptureV2_ResultCode_Success)
            {
                return validationResult;
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
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return CtCaptureV2_ResultCode_NativeFailure;
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
                return CtCaptureV2_ResultCode_InvalidState;
            }

            recorder->state = RecorderState::Paused;
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return CtCaptureV2_ResultCode_NativeFailure;
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
                return CtCaptureV2_ResultCode_InvalidState;
            }

            recorder->state = RecorderState::Recording;
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return CtCaptureV2_ResultCode_NativeFailure;
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
                return CtCaptureV2_ResultCode_InvalidArgument;
            }

            if (recorder->state != RecorderState::Recording && recorder->state != RecorderState::Paused)
            {
                return CtCaptureV2_ResultCode_InvalidState;
            }

            if (!HasArmedAudioSource(*recorder, sourceId))
            {
                return CtCaptureV2_ResultCode_NotFound;
            }

            SetRuntimeMuted(*recorder, sourceId, muted != 0);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return CtCaptureV2_ResultCode_NativeFailure;
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
                return CtCaptureV2_ResultCode_InvalidState;
            }

            if (gainDb < CaptureInterop::V2::Api::MinAudioGainDb
                || gainDb > CaptureInterop::V2::Api::MaxAudioGainDb)
            {
                return CtCaptureV2_ResultCode_ValidationFailed;
            }

            if (!HasArmedAudioSource(*recorder, sourceId))
            {
                return CtCaptureV2_ResultCode_NotFound;
            }

            SetRuntimeGain(*recorder, sourceId, gainDb);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return CtCaptureV2_ResultCode_NativeFailure;
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

            if (recorder->state == RecorderState::Idle)
            {
                *result = MakeStopResult(CtCaptureV2_ResultCode_AlreadyStopped, RecorderState::Idle);
                return CtCaptureV2_ResultCode_AlreadyStopped;
            }

            recorder->state = RecorderState::Idle;
            recorder->activeConfig.reset();
            recorder->runtimeGains.clear();
            recorder->mutedAudioSources.clear();
            *result = MakeStopResult(CtCaptureV2_ResultCode_Success, RecorderState::Idle);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            if (result != nullptr)
            {
                *result = MakeStopResult(CtCaptureV2_ResultCode_NativeFailure, RecorderState::Idle);
            }

            return CtCaptureV2_ResultCode_NativeFailure;
        }
    }
}
