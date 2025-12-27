#include "pch.h"
#include "SampleBuilder.h"

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

    // Create media buffer
    UINT32 bufferSize = static_cast<UINT32>(data.size());
    wil::com_ptr<IMFMediaBuffer> buffer;
    HRESULT hr = MFCreateMemoryBuffer(bufferSize, buffer.put());
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFSample>>::Error(
            ErrorInfo::FromHResult(hr, (std::string(context) + ": MFCreateMemoryBuffer failed").c_str()));
    }

    // Lock buffer and copy data
    BYTE* pBufferData = nullptr;
    DWORD maxLen = 0;
    DWORD curLen = 0;
    hr = buffer->Lock(&pBufferData, &maxLen, &curLen);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFSample>>::Error(
            ErrorInfo::FromHResult(hr, (std::string(context) + ": Buffer Lock failed").c_str()));
    }

    memcpy(pBufferData, data.data(), bufferSize);
    buffer->SetCurrentLength(bufferSize);
    buffer->Unlock();

    // Create sample
    wil::com_ptr<IMFSample> sample;
    hr = MFCreateSample(sample.put());
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFSample>>::Error(
            ErrorInfo::FromHResult(hr, (std::string(context) + ": MFCreateSample failed").c_str()));
    }

    // Add buffer to sample
    hr = sample->AddBuffer(buffer.get());
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFSample>>::Error(
            ErrorInfo::FromHResult(hr, (std::string(context) + ": AddBuffer failed").c_str()));
    }

    // Set sample timing
    hr = sample->SetSampleTime(timestamp);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFSample>>::Error(
            ErrorInfo::FromHResult(hr, (std::string(context) + ": SetSampleTime failed").c_str()));
    }

    hr = sample->SetSampleDuration(duration);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFSample>>::Error(
            ErrorInfo::FromHResult(hr, (std::string(context) + ": SetSampleDuration failed").c_str()));
    }

    return Result<wil::com_ptr<IMFSample>>::Ok(std::move(sample));
}
