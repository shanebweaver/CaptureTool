#pragma once

#include <Windows.h>
#include <cstdint>

enum class MonitorHdrMode
{
    Unknown = 0,
    Sdr = 1,
    Hdr = 2
};

enum class MonitorHdrFallbackReason
{
    None = 0,
    DetectorUnavailable = 1,
    OutputNotFound = 2,
    QueryFailed = 3,
    UnsupportedColorSpace = 4,
    Unknown = 5
};

struct MonitorHdrInfo
{
    bool detectionSucceeded;
    MonitorHdrMode mode;
    bool hasSourceColorSpace;
    int32_t sourceColorSpace;
    bool hasSdrWhiteLevelNits;
    float sdrWhiteLevelNits;
    MonitorHdrFallbackReason fallbackReason;
    HRESULT hr;

    static MonitorHdrInfo Sdr(
        bool hasColorSpace = false,
        int32_t colorSpace = 0,
        bool hasWhiteLevel = false,
        float whiteLevelNits = 0.0f)
    {
        return MonitorHdrInfo{
            true,
            MonitorHdrMode::Sdr,
            hasColorSpace,
            colorSpace,
            hasWhiteLevel,
            whiteLevelNits,
            MonitorHdrFallbackReason::None,
            S_OK };
    }

    static MonitorHdrInfo Hdr(
        bool hasColorSpace = false,
        int32_t colorSpace = 0,
        bool hasWhiteLevel = false,
        float whiteLevelNits = 0.0f)
    {
        return MonitorHdrInfo{
            true,
            MonitorHdrMode::Hdr,
            hasColorSpace,
            colorSpace,
            hasWhiteLevel,
            whiteLevelNits,
            MonitorHdrFallbackReason::None,
            S_OK };
    }

    static MonitorHdrInfo Unknown(
        MonitorHdrFallbackReason reason = MonitorHdrFallbackReason::Unknown,
        bool hasColorSpace = false,
        int32_t colorSpace = 0)
    {
        return MonitorHdrInfo{
            true,
            MonitorHdrMode::Unknown,
            hasColorSpace,
            colorSpace,
            false,
            0.0f,
            reason,
            S_OK };
    }

    static MonitorHdrInfo Failed(MonitorHdrFallbackReason reason, HRESULT failureHr)
    {
        return MonitorHdrInfo{
            false,
            MonitorHdrMode::Unknown,
            false,
            0,
            false,
            0.0f,
            reason,
            failureHr };
    }

    bool IsHdrActive() const
    {
        return detectionSucceeded && mode == MonitorHdrMode::Hdr;
    }

    bool ShouldUseToneMapper() const
    {
        return IsHdrActive();
    }
};
