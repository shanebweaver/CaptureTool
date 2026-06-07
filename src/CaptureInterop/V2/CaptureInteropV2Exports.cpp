#define CTCAPTUREV2_BUILD
#include "CaptureInteropV2Exports.h"

#include <memory>
#include <mutex>
#include <unordered_set>

struct CtCaptureV2_Recorder_t
{
    uint32_t state{ 0 };
};

namespace
{
    constexpr uint32_t ApiVersionDtoVersion = 1;
    constexpr uint32_t ApiVersionMajor = 2;
    constexpr uint32_t ApiVersionMinor = 0;
    constexpr uint32_t ApiVersionPatch = 0;

    std::mutex RecorderRegistryMutex;
    std::unordered_set<CtCaptureV2_RecorderHandle> RecorderRegistry;
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
}
