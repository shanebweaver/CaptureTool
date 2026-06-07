#pragma once

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

extern "C"
{
    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_GetApiVersion(
        CtCaptureV2_ApiVersion* outVersion) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_CreateRecorder(
        CtCaptureV2_RecorderHandle* outHandle) noexcept;

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_DestroyRecorder(
        CtCaptureV2_RecorderHandle handle) noexcept;
}
