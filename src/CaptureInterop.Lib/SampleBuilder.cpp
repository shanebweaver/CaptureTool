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

    if (data.size() > std::numeric_limits<UINT32>::max())
    {
        return Result<wil::com_ptr<IMFSample>>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Data size exceeds UINT32 limit", context));
    }

    UINT32 bufferSize = static_cast<UINT32>(data.size());
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

    memcpy(pBufferData, data.data(), bufferSize);
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
