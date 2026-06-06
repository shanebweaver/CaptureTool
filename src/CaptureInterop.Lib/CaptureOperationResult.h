#pragma once
#include <Windows.h>
#include <cstdint>

enum class CaptureOperationStage : int32_t
{
    None = 0,
    VideoSourceStop = 1,
    AudioSourceStop = 2,
    SinkFinalize = 3,
    NativeException = 4,
    VideoFrameWrite = 5,
    AudioSampleWrite = 6
};

struct CaptureOperationResult
{
    HRESULT hr = S_OK;
    CaptureOperationStage stage = CaptureOperationStage::None;

    static CaptureOperationResult Success() noexcept
    {
        return {};
    }

    static CaptureOperationResult Failure(HRESULT error, CaptureOperationStage failureStage) noexcept
    {
        return { error, failureStage };
    }

    bool IsSuccess() const noexcept
    {
        return SUCCEEDED(hr);
    }
};
