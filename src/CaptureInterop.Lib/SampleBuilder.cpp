#include "pch.h"
#include "SampleBuilder.h"
#include <limits>

Result<wil::com_ptr<IMFSample>> SampleBuilder::CreateVideoSample(
    std::span<const uint8_t> data,
    int64_t timestamp,
    int64_t duration) const
{
    return CreateSampleFromData(data, timestamp, duration, "CreateVideoSample");
}

Result<wil::com_ptr<IMFSample>> SampleBuilder::CreateVideoSampleFromBuffer(
    uint32_t bufferSize,
    int64_t timestamp,
    int64_t duration,
    const std::function<Result<void>(std::span<uint8_t>)>& fillBuffer) const
{
    return CreateSampleFromBuffer(bufferSize, timestamp, duration, "CreateVideoSampleFromBuffer", fillBuffer);
}

Result<wil::com_ptr<IMFSample>> SampleBuilder::CreateAudioSample(
    std::span<const uint8_t> data,
    int64_t timestamp,
    int64_t duration) const
{
    return CreateSampleFromData(data, timestamp, duration, "CreateAudioSample");
}

Result<wil::com_ptr<IMFSample>> SampleBuilder::CreateSampleFromData(
    std::span<const uint8_t> data,
    int64_t timestamp,
    int64_t duration,
    const char* context) const
{
    if (data.empty())
    {
        return Result<wil::com_ptr<IMFSample>>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Data is empty", context));
    }

    if (data.size() > (std::numeric_limits<UINT32>::max)())
    {
        return Result<wil::com_ptr<IMFSample>>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Data size exceeds UINT32 limit", context));
    }

    UINT32 bufferSize = static_cast<UINT32>(data.size());
    return CreateSampleFromBuffer(
        bufferSize,
        timestamp,
        duration,
        context,
        [&data](std::span<uint8_t> buffer) -> Result<void> {
            memcpy(buffer.data(), data.data(), data.size());
            return Result<void>::Ok();
        });
}

Result<wil::com_ptr<IMFSample>> SampleBuilder::CreateSampleFromBuffer(
    uint32_t bufferSize,
    int64_t timestamp,
    int64_t duration,
    const char* context,
    const std::function<Result<void>(std::span<uint8_t>)>& fillBuffer) const
{
    if (bufferSize == 0 || !fillBuffer)
    {
        return Result<wil::com_ptr<IMFSample>>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Buffer size or fill callback is invalid", context));
    }

    wil::com_ptr<IMFMediaBuffer> buffer;
    HRESULT hr = MFCreateMemoryBuffer(bufferSize, buffer.put());
    if (FAILED(hr))
    {
        std::string errorContext = std::string(context) + ": MFCreateMemoryBuffer failed";
        return Result<wil::com_ptr<IMFSample>>::Error(
            ErrorInfo::FromHResult(hr, errorContext.c_str()));
    }

    BYTE* pBufferData = nullptr;
    DWORD maxLen = 0;
    DWORD curLen = 0;
    hr = buffer->Lock(&pBufferData, &maxLen, &curLen);
    if (FAILED(hr))
    {
        std::string errorContext = std::string(context) + ": Buffer Lock failed";
        return Result<wil::com_ptr<IMFSample>>::Error(
            ErrorInfo::FromHResult(hr, errorContext.c_str()));
    }

    auto fillResult = fillBuffer(std::span<uint8_t>(pBufferData, bufferSize));
    if (fillResult.IsError())
    {
        buffer->Unlock();
        return Result<wil::com_ptr<IMFSample>>::Error(fillResult.Error());
    }

    buffer->SetCurrentLength(bufferSize);
    buffer->Unlock();

    wil::com_ptr<IMFSample> sample;
    hr = MFCreateSample(sample.put());
    if (FAILED(hr))
    {
        std::string errorContext = std::string(context) + ": MFCreateSample failed";
        return Result<wil::com_ptr<IMFSample>>::Error(
            ErrorInfo::FromHResult(hr, errorContext.c_str()));
    }

    hr = sample->AddBuffer(buffer.get());
    if (FAILED(hr))
    {
        std::string errorContext = std::string(context) + ": AddBuffer failed";
        return Result<wil::com_ptr<IMFSample>>::Error(
            ErrorInfo::FromHResult(hr, errorContext.c_str()));
    }

    hr = sample->SetSampleTime(timestamp);
    if (FAILED(hr))
    {
        std::string errorContext = std::string(context) + ": SetSampleTime failed";
        return Result<wil::com_ptr<IMFSample>>::Error(
            ErrorInfo::FromHResult(hr, errorContext.c_str()));
    }

    hr = sample->SetSampleDuration(duration);
    if (FAILED(hr))
    {
        std::string errorContext = std::string(context) + ": SetSampleDuration failed";
        return Result<wil::com_ptr<IMFSample>>::Error(
            ErrorInfo::FromHResult(hr, errorContext.c_str()));
    }

    return Result<wil::com_ptr<IMFSample>>::Ok(std::move(sample));
}
