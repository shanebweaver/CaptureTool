#define CTCAPTUREV2_BUILD
#include "CaptureInteropV2Exports.h"

namespace
{
    constexpr uint32_t ApiVersionDtoVersion = 1;
    constexpr uint32_t ApiVersionMajor = 2;
    constexpr uint32_t ApiVersionMinor = 0;
    constexpr uint32_t ApiVersionPatch = 0;
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
}
