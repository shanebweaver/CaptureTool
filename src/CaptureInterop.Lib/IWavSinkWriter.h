#pragma once
#include <cstdint>
#include <span>
#include <mmreg.h>

class IWavSinkWriter
{
public:
    virtual ~IWavSinkWriter() = default;

    virtual bool Initialize(const wchar_t* outputPath, WAVEFORMATEX* audioFormat, long* outHr = nullptr) = 0;
    virtual long WriteAudioSample(std::span<const uint8_t> data, int64_t timestamp) = 0;
    virtual void Finalize() = 0;
};
