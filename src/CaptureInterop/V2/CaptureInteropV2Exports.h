#pragma once

#include <cstddef>
#include <cstdint>

#if defined(CTCAPTUREV2_BUILD)
#define CTCAPTUREV2_API __declspec(dllexport)
#else
#define CTCAPTUREV2_API __declspec(dllimport)
#endif

#define CTCAPTUREV2_CALL __stdcall

enum CtCaptureV2_ResultCode : int32_t
{
    CtCaptureV2_ResultCode_Success = 0,
    CtCaptureV2_ResultCode_InvalidArgument = 1,
    CtCaptureV2_ResultCode_InvalidHandle = 2,
    CtCaptureV2_ResultCode_InvalidState = 3,
    CtCaptureV2_ResultCode_UnsupportedVersion = 4,
    CtCaptureV2_ResultCode_UnsupportedOperation = 5,
    CtCaptureV2_ResultCode_ValidationFailed = 6,
    CtCaptureV2_ResultCode_NotFound = 7,
    CtCaptureV2_ResultCode_AlreadyStarted = 8,
    CtCaptureV2_ResultCode_AlreadyStopped = 9,
    CtCaptureV2_ResultCode_BufferTooSmall = 10,
    CtCaptureV2_ResultCode_NativeFailure = 11,
    CtCaptureV2_ResultCode_ExternalApiFailure = 12,
    CtCaptureV2_ResultCode_CallbackRegistrationFailed = 13,
    CtCaptureV2_ResultCode_CallbackInvocationFailed = 14
};

struct CtCaptureV2_ApiVersion
{
    uint32_t size;
    uint32_t version;
    uint32_t major;
    uint32_t minor;
    uint32_t patch;
    uint32_t reserved;
};

typedef struct CtCaptureV2_Recorder_t* CtCaptureV2_RecorderHandle;

enum CtCaptureV2_SourceKind : int32_t
{
    CtCaptureV2_SourceKind_Unknown = 0,
    CtCaptureV2_SourceKind_Desktop = 1,
    CtCaptureV2_SourceKind_SystemAudio = 5
};

enum CtCaptureV2_ContainerFormat : int32_t
{
    CtCaptureV2_ContainerFormat_Unknown = 0,
    CtCaptureV2_ContainerFormat_Mp4 = 1
};

enum CtCaptureV2_VideoCodec : int32_t
{
    CtCaptureV2_VideoCodec_None = 0,
    CtCaptureV2_VideoCodec_H264 = 1
};

enum CtCaptureV2_AudioCodec : int32_t
{
    CtCaptureV2_AudioCodec_None = 0,
    CtCaptureV2_AudioCodec_Aac = 1
};

enum CtCaptureV2_HdrPolicy : int32_t
{
    CtCaptureV2_HdrPolicy_Auto = 0,
    CtCaptureV2_HdrPolicy_Preserve = 1,
    CtCaptureV2_HdrPolicy_MapToSdr = 2,
    CtCaptureV2_HdrPolicy_MatchDisplay = 3,
    CtCaptureV2_HdrPolicy_ForceSdr = 4
};

struct CtCaptureV2_Rect
{
    int32_t x;
    int32_t y;
    int32_t width;
    int32_t height;
};

struct CtCaptureV2_SourceConfig
{
    uint32_t size;
    uint32_t version;
    uint32_t sourceId;
    int32_t sourceKind;
    CtCaptureV2_Rect captureRect;
    void* platformHandle;
    uint8_t enabled;
    uint8_t reserved0;
    uint16_t reserved1;
};

struct CtCaptureV2_VideoEncodingConfig
{
    uint32_t size;
    uint32_t version;
    int32_t codec;
    uint32_t bitrate;
    uint32_t frameRateNumerator;
    uint32_t frameRateDenominator;
    uint32_t gopLength;
    uint8_t hardwareAccelerationPreferred;
    uint8_t reserved0;
    uint16_t reserved1;
};

struct CtCaptureV2_AudioEncodingConfig
{
    uint32_t size;
    uint32_t version;
    int32_t codec;
    uint32_t bitrate;
    uint32_t sampleRate;
    uint16_t channels;
    uint16_t reserved;
};

struct CtCaptureV2_OutputConfig
{
    uint32_t size;
    uint32_t version;
    const char16_t* outputPath;
    int32_t containerFormat;
    CtCaptureV2_VideoEncodingConfig video;
    CtCaptureV2_AudioEncodingConfig audio;
};

struct CtCaptureV2_ToneMappingConfig
{
    uint32_t size;
    uint32_t version;
    int32_t hdrPolicy;
    float targetNits;
    uint8_t preserveMetadataWhenPossible;
    uint8_t reserved0;
    uint16_t reserved1;
};

struct CtCaptureV2_AudioGainConfig
{
    uint32_t size;
    uint32_t version;
    uint32_t sourceId;
    float gainDb;
    uint32_t reserved;
};

struct CtCaptureV2_ControlConfig
{
    uint32_t size;
    uint32_t version;
    uint8_t startMuted;
    uint8_t reserved0;
    uint16_t reserved1;
    const CtCaptureV2_AudioGainConfig* audioGains;
    uint32_t audioGainCount;
};

struct CtCaptureV2_Config
{
    uint32_t size;
    uint32_t version;
    const CtCaptureV2_SourceConfig* sources;
    uint32_t sourceCount;
    CtCaptureV2_OutputConfig output;
    CtCaptureV2_ToneMappingConfig toneMapping;
    CtCaptureV2_ControlConfig controls;
    uint32_t reserved;
};

struct CtCaptureV2_StopResult
{
    uint32_t size;
    uint32_t version;
    int32_t resultCode;
    int32_t finalState;
    int32_t failureStage;
    uint32_t reserved;
    uint64_t droppedVideoFrames;
    uint64_t audioDiscontinuities;
    uint64_t lateSamples;
    uint64_t unsupportedCommands;
    uint64_t validationWarnings;
};

inline constexpr uint32_t CtCaptureV2_DtoVersion = 1;

inline void CtCaptureV2_InitSourceConfig(CtCaptureV2_SourceConfig* value) noexcept
{
    if (value != nullptr)
    {
        *value = {};
        value->size = sizeof(CtCaptureV2_SourceConfig);
        value->version = CtCaptureV2_DtoVersion;
    }
}

inline void CtCaptureV2_InitVideoEncodingConfig(CtCaptureV2_VideoEncodingConfig* value) noexcept
{
    if (value != nullptr)
    {
        *value = {};
        value->size = sizeof(CtCaptureV2_VideoEncodingConfig);
        value->version = CtCaptureV2_DtoVersion;
    }
}

inline void CtCaptureV2_InitAudioEncodingConfig(CtCaptureV2_AudioEncodingConfig* value) noexcept
{
    if (value != nullptr)
    {
        *value = {};
        value->size = sizeof(CtCaptureV2_AudioEncodingConfig);
        value->version = CtCaptureV2_DtoVersion;
    }
}

inline void CtCaptureV2_InitOutputConfig(CtCaptureV2_OutputConfig* value) noexcept
{
    if (value != nullptr)
    {
        *value = {};
        value->size = sizeof(CtCaptureV2_OutputConfig);
        value->version = CtCaptureV2_DtoVersion;
        CtCaptureV2_InitVideoEncodingConfig(&value->video);
        CtCaptureV2_InitAudioEncodingConfig(&value->audio);
    }
}

inline void CtCaptureV2_InitToneMappingConfig(CtCaptureV2_ToneMappingConfig* value) noexcept
{
    if (value != nullptr)
    {
        *value = {};
        value->size = sizeof(CtCaptureV2_ToneMappingConfig);
        value->version = CtCaptureV2_DtoVersion;
        value->hdrPolicy = CtCaptureV2_HdrPolicy_Auto;
    }
}

inline void CtCaptureV2_InitAudioGainConfig(CtCaptureV2_AudioGainConfig* value) noexcept
{
    if (value != nullptr)
    {
        *value = {};
        value->size = sizeof(CtCaptureV2_AudioGainConfig);
        value->version = CtCaptureV2_DtoVersion;
    }
}

inline void CtCaptureV2_InitControlConfig(CtCaptureV2_ControlConfig* value) noexcept
{
    if (value != nullptr)
    {
        *value = {};
        value->size = sizeof(CtCaptureV2_ControlConfig);
        value->version = CtCaptureV2_DtoVersion;
    }
}

inline void CtCaptureV2_InitConfig(CtCaptureV2_Config* value) noexcept
{
    if (value != nullptr)
    {
        *value = {};
        value->size = sizeof(CtCaptureV2_Config);
        value->version = CtCaptureV2_DtoVersion;
        CtCaptureV2_InitOutputConfig(&value->output);
        CtCaptureV2_InitToneMappingConfig(&value->toneMapping);
        CtCaptureV2_InitControlConfig(&value->controls);
    }
}

inline void CtCaptureV2_InitStopResult(CtCaptureV2_StopResult* value) noexcept
{
    if (value != nullptr)
    {
        *value = {};
        value->size = sizeof(CtCaptureV2_StopResult);
        value->version = CtCaptureV2_DtoVersion;
    }
}

extern "C"
{
    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_GetApiVersion(
        CtCaptureV2_ApiVersion* outVersion) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_CreateRecorder(
        CtCaptureV2_RecorderHandle* outHandle) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_DestroyRecorder(
        CtCaptureV2_RecorderHandle handle) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_Start(
        CtCaptureV2_RecorderHandle handle,
        const CtCaptureV2_Config* config) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_Pause(
        CtCaptureV2_RecorderHandle handle) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_Resume(
        CtCaptureV2_RecorderHandle handle) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_SetAudioMuted(
        CtCaptureV2_RecorderHandle handle,
        uint32_t sourceId,
        uint8_t muted) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_SetAudioGain(
        CtCaptureV2_RecorderHandle handle,
        uint32_t sourceId,
        float gainDb) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_Stop(
        CtCaptureV2_RecorderHandle handle,
        CtCaptureV2_StopResult* result) noexcept;
}
