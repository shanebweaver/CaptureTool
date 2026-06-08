#pragma once

#include "MonitorHdrInfo.h"
#include <dxgicommon.h>

namespace MonitorHdrColorSpaceMapper
{
    inline bool IsHdrColorSpace(DXGI_COLOR_SPACE_TYPE colorSpace)
    {
        switch (colorSpace)
        {
        case DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020:
        case DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_LEFT_P2020:
        case DXGI_COLOR_SPACE_RGB_STUDIO_G2084_NONE_P2020:
        case DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_TOPLEFT_P2020:
        case DXGI_COLOR_SPACE_YCBCR_STUDIO_GHLG_TOPLEFT_P2020:
        case DXGI_COLOR_SPACE_YCBCR_FULL_GHLG_TOPLEFT_P2020:
            return true;
        default:
            return false;
        }
    }

    inline bool IsKnownSdrColorSpace(DXGI_COLOR_SPACE_TYPE colorSpace)
    {
        switch (colorSpace)
        {
        case DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709:
        case DXGI_COLOR_SPACE_RGB_FULL_G10_NONE_P709:
        case DXGI_COLOR_SPACE_RGB_STUDIO_G22_NONE_P709:
        case DXGI_COLOR_SPACE_RGB_STUDIO_G22_NONE_P2020:
        case DXGI_COLOR_SPACE_YCBCR_FULL_G22_NONE_P709_X601:
        case DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P601:
        case DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P601:
        case DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P709:
        case DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P709:
        case DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_TOPLEFT_P2020:
        case DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P2020:
        case DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P2020:
        case DXGI_COLOR_SPACE_RGB_STUDIO_G24_NONE_P709:
        case DXGI_COLOR_SPACE_RGB_STUDIO_G24_NONE_P2020:
        case DXGI_COLOR_SPACE_YCBCR_STUDIO_G24_LEFT_P709:
        case DXGI_COLOR_SPACE_YCBCR_STUDIO_G24_LEFT_P2020:
        case DXGI_COLOR_SPACE_YCBCR_STUDIO_G24_TOPLEFT_P2020:
            return true;
        default:
            return false;
        }
    }

    inline MonitorHdrInfo FromColorSpace(DXGI_COLOR_SPACE_TYPE colorSpace)
    {
        const auto colorSpaceValue = static_cast<int32_t>(colorSpace);

        if (IsHdrColorSpace(colorSpace))
        {
            return MonitorHdrInfo::Hdr(true, colorSpaceValue);
        }

        if (IsKnownSdrColorSpace(colorSpace))
        {
            return MonitorHdrInfo::Sdr(true, colorSpaceValue);
        }

        return MonitorHdrInfo::Unknown(
            MonitorHdrFallbackReason::UnsupportedColorSpace,
            true,
            colorSpaceValue);
    }
}
