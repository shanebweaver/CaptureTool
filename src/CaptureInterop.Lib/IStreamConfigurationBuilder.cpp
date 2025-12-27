#include "pch.h"
#include "IStreamConfigurationBuilder.h"
#include <mmreg.h>
#include <ks.h>
#include <ksmedia.h>

IStreamConfigurationBuilder::AudioConfig IStreamConfigurationBuilder::AudioConfig::FromWaveFormat(const WAVEFORMATEX& format)
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
