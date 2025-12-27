#include "pch.h"
#include "StreamConfigurationBuilder.h"

StreamConfigurationBuilder::AudioConfig StreamConfigurationBuilder::AudioConfig::FromWaveFormat(const WAVEFORMATEX& format)
{
    AudioConfig config{};
    config.sampleRate = format.nSamplesPerSec;
    config.channels = format.nChannels;
    config.bitsPerSample = format.wBitsPerSample;
    config.bitrate = DEFAULT_AAC_BITRATE;
    
    // Detect if this is a float format
    config.isFloatFormat = false;
    if (format.wFormatTag == WAVE_FORMAT_IEEE_FLOAT)
    {
        config.isFloatFormat = true;
    }
    else if (format.wFormatTag == WAVE_FORMAT_EXTENSIBLE)
    {
        const WAVEFORMATEXTENSIBLE* pFormatEx = reinterpret_cast<const WAVEFORMATEXTENSIBLE*>(&format);
        if (IsEqualGUID(pFormatEx->SubFormat, KSDATAFORMAT_SUBTYPE_IEEE_FLOAT))
        {
            config.isFloatFormat = true;
        }
    }
    
    return config;
}

Result<wil::com_ptr<IMFMediaType>> StreamConfigurationBuilder::CreateVideoOutputType(const VideoConfig& config) const
{
    wil::com_ptr<IMFMediaType> mediaType;
    HRESULT hr = MFCreateMediaType(mediaType.put());
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateVideoOutputType: MFCreateMediaType failed"));
    }

    // Set video media type to H.264
    hr = mediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateVideoOutputType: SetGUID major type failed"));
    }

    hr = mediaType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_H264);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateVideoOutputType: SetGUID subtype failed"));
    }

    hr = mediaType->SetUINT32(MF_MT_AVG_BITRATE, config.bitrate);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateVideoOutputType: SetUINT32 bitrate failed"));
    }

    hr = mediaType->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateVideoOutputType: SetUINT32 interlace mode failed"));
    }

    hr = MFSetAttributeSize(mediaType.get(), MF_MT_FRAME_SIZE, config.width, config.height);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateVideoOutputType: MFSetAttributeSize frame size failed"));
    }

    hr = MFSetAttributeRatio(mediaType.get(), MF_MT_FRAME_RATE, config.frameRate, 1);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateVideoOutputType: MFSetAttributeRatio frame rate failed"));
    }

    hr = MFSetAttributeRatio(mediaType.get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateVideoOutputType: MFSetAttributeRatio pixel aspect ratio failed"));
    }

    return Result<wil::com_ptr<IMFMediaType>>::Ok(std::move(mediaType));
}

Result<wil::com_ptr<IMFMediaType>> StreamConfigurationBuilder::CreateVideoInputType(const VideoConfig& config) const
{
    wil::com_ptr<IMFMediaType> mediaType;
    HRESULT hr = MFCreateMediaType(mediaType.put());
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateVideoInputType: MFCreateMediaType failed"));
    }

    hr = mediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateVideoInputType: SetGUID major type failed"));
    }

    hr = mediaType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_RGB32);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateVideoInputType: SetGUID subtype failed"));
    }

    hr = MFSetAttributeSize(mediaType.get(), MF_MT_FRAME_SIZE, config.width, config.height);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateVideoInputType: MFSetAttributeSize frame size failed"));
    }

    hr = MFSetAttributeRatio(mediaType.get(), MF_MT_FRAME_RATE, config.frameRate, 1);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateVideoInputType: MFSetAttributeRatio frame rate failed"));
    }

    hr = MFSetAttributeRatio(mediaType.get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateVideoInputType: MFSetAttributeRatio pixel aspect ratio failed"));
    }

    LONG defaultStride = static_cast<LONG>(config.width * 4);
    hr = mediaType->SetUINT32(MF_MT_DEFAULT_STRIDE, static_cast<UINT32>(defaultStride));
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateVideoInputType: SetUINT32 default stride failed"));
    }

    return Result<wil::com_ptr<IMFMediaType>>::Ok(std::move(mediaType));
}

Result<wil::com_ptr<IMFMediaType>> StreamConfigurationBuilder::CreateAudioOutputType(const AudioConfig& config) const
{
    wil::com_ptr<IMFMediaType> mediaType;
    HRESULT hr = MFCreateMediaType(mediaType.put());
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateAudioOutputType: MFCreateMediaType failed"));
    }

    hr = mediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateAudioOutputType: SetGUID major type failed"));
    }

    hr = mediaType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_AAC);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateAudioOutputType: SetGUID subtype failed"));
    }

    hr = mediaType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, config.sampleRate);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateAudioOutputType: SetUINT32 sample rate failed"));
    }

    hr = mediaType->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, config.channels);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateAudioOutputType: SetUINT32 channels failed"));
    }

    hr = mediaType->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, config.bitrate);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateAudioOutputType: SetUINT32 bitrate failed"));
    }

    // AAC output is always 16-bit, regardless of input format
    hr = mediaType->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateAudioOutputType: SetUINT32 bits per sample failed"));
    }

    return Result<wil::com_ptr<IMFMediaType>>::Ok(std::move(mediaType));
}

Result<wil::com_ptr<IMFMediaType>> StreamConfigurationBuilder::CreateAudioInputType(const AudioConfig& config) const
{
    wil::com_ptr<IMFMediaType> mediaType;
    HRESULT hr = MFCreateMediaType(mediaType.put());
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateAudioInputType: MFCreateMediaType failed"));
    }

    hr = mediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateAudioInputType: SetGUID major type failed"));
    }

    if (config.isFloatFormat)
    {
        hr = mediaType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_Float);
    }
    else
    {
        hr = mediaType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_PCM);
    }
    
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateAudioInputType: SetGUID subtype failed"));
    }

    hr = mediaType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, config.sampleRate);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateAudioInputType: SetUINT32 sample rate failed"));
    }

    hr = mediaType->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, config.channels);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateAudioInputType: SetUINT32 channels failed"));
    }

    hr = mediaType->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, config.bitsPerSample);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateAudioInputType: SetUINT32 bits per sample failed"));
    }

    // Calculate block alignment: (channels * bitsPerSample) / 8
    uint32_t blockAlign = (config.channels * config.bitsPerSample) / 8;
    hr = mediaType->SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, blockAlign);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateAudioInputType: SetUINT32 block alignment failed"));
    }

    // Calculate average bytes per second: sampleRate * blockAlign
    uint32_t avgBytesPerSec = config.sampleRate * blockAlign;
    hr = mediaType->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, avgBytesPerSec);
    if (FAILED(hr))
    {
        return Result<wil::com_ptr<IMFMediaType>>::Error(
            ErrorInfo::FromHResult(hr, "CreateAudioInputType: SetUINT32 avg bytes per second failed"));
    }

    return Result<wil::com_ptr<IMFMediaType>>::Ok(std::move(mediaType));
}
